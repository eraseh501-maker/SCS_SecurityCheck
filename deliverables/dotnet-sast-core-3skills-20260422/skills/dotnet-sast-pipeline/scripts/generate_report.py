#!/usr/bin/env python3
"""
.NET SAST 結果彙整與報告產生器

使用方式：
    python generate_report.py --input ./sast-output
    python generate_report.py --input ./sast-output --output ./security-report.md
    python generate_report.py --input ./sast-output --format html
"""

import argparse
import json
import os
import sys
from datetime import datetime
from pathlib import Path
from typing import Any

# ─────────────────────────────────────────────
# 嚴重程度對照
# ─────────────────────────────────────────────
SEVERITY_ORDER = {"critical": 0, "error": 1, "high": 2, "warning": 3, "medium": 4, "low": 5, "info": 6, "note": 7}
SEVERITY_EMOJI = {
    "critical": "🔴", "error": "🔴",
    "high": "🟠", "warning": "🟡",
    "medium": "🟡", "low": "🔵",
    "info": "⚪", "note": "⚪",
}
SEVERITY_LABEL = {
    "critical": "Critical", "error": "High",
    "high": "High", "warning": "Medium",
    "medium": "Medium", "low": "Low",
    "info": "Info", "note": "Info",
}

def sev_rank(s: str) -> int:
    return SEVERITY_ORDER.get(s.lower(), 99)


# ─────────────────────────────────────────────
# 解析各工具輸出
# ─────────────────────────────────────────────

def parse_nuget(output_dir: Path) -> list[dict]:
    findings = []
    txt_file = output_dir / "nuget-vulnerabilities.txt"
    if not txt_file.exists():
        return findings

    with open(txt_file, encoding="utf-8") as f:
        current_project = "Unknown"
        for line in f:
            line = line.rstrip()
            # 偵測專案名稱行
            if "Project" in line and ":" in line:
                current_project = line.split(":")[-1].strip()
            # 偵測漏洞行（以 > 開頭）
            elif line.strip().startswith(">"):
                parts = line.strip().lstrip(">").split()
                if len(parts) >= 2:
                    severity = "medium"
                    advisory_url = ""
                    for p in parts:
                        if p.lower() in SEVERITY_ORDER:
                            severity = p.lower()
                        if "https://" in p:
                            advisory_url = p
                    findings.append({
                        "tool": "nuget",
                        "severity": severity,
                        "title": f"Vulnerable NuGet package: {parts[0]} {parts[1]}",
                        "description": f"Package {parts[0]} version {parts[1]} has known vulnerabilities.",
                        "location": current_project,
                        "line": None,
                        "cwe": "",
                        "reference": advisory_url,
                        "remediation": f"Upgrade {parts[0]} to a patched version. Run `dotnet list package --vulnerable` after upgrade to verify.",
                    })
    return findings


def parse_semgrep(output_dir: Path) -> list[dict]:
    findings = []
    result_file = output_dir / "semgrep-results.json"
    if not result_file.exists():
        return findings

    with open(result_file, encoding="utf-8") as f:
        data = json.load(f)

    for r in data.get("results", []):
        severity_raw = r.get("extra", {}).get("severity", "WARNING").lower()
        metadata = r.get("extra", {}).get("metadata", {})
        cwe_list = metadata.get("cwe", [])
        cwe = cwe_list[0] if cwe_list else ""

        findings.append({
            "tool": "semgrep",
            "severity": severity_raw,
            "title": r.get("check_id", "Unknown Rule"),
            "description": r.get("extra", {}).get("message", ""),
            "location": r.get("path", ""),
            "line": r.get("start", {}).get("line"),
            "cwe": cwe,
            "reference": metadata.get("references", [None])[0] if metadata.get("references") else "",
            "remediation": metadata.get("fix", metadata.get("message", "")),
            "code_snippet": r.get("extra", {}).get("lines", "").strip(),
        })
    return findings


def parse_scs_sarif(output_dir: Path) -> list[dict]:
    findings = []
    sarif_file = output_dir / "scs-results.sarif"
    if not sarif_file.exists():
        return findings

    with open(sarif_file, encoding="utf-8") as f:
        data = json.load(f)

    # 建立 rule id -> 詳細資訊的對照表
    rules_map: dict[str, Any] = {}
    for run in data.get("runs", []):
        for rule in run.get("tool", {}).get("driver", {}).get("rules", []):
            rules_map[rule["id"]] = rule

    for run in data.get("runs", []):
        for result in run.get("results", []):
            rule_id = result.get("ruleId", "Unknown")
            rule_info = rules_map.get(rule_id, {})
            severity_raw = result.get("level", "warning").lower()

            locations = result.get("locations", [{}])
            loc = locations[0].get("physicalLocation", {}) if locations else {}
            file_path = loc.get("artifactLocation", {}).get("uri", "")
            line_num = loc.get("region", {}).get("startLine")

            findings.append({
                "tool": "scs",
                "severity": severity_raw,
                "title": f"SCS: {rule_id}",
                "description": result.get("message", {}).get("text", ""),
                "location": file_path,
                "line": line_num,
                "cwe": rule_info.get("properties", {}).get("tags", [""])[0] if rule_info else "",
                "reference": "",
                "remediation": rule_info.get("help", {}).get("text", "") if rule_info else "",
            })
    return findings


def parse_gitleaks(output_dir: Path) -> list[dict]:
    findings = []
    report_file = output_dir / "gitleaks-report.json"
    if not report_file.exists():
        return findings

    with open(report_file, encoding="utf-8") as f:
        try:
            data = json.load(f)
        except json.JSONDecodeError:
            return findings

    if not isinstance(data, list):
        return findings

    for leak in data:
        findings.append({
            "tool": "gitleaks",
            "severity": "critical",
            "title": f"Secret Detected: {leak.get('RuleID', 'Unknown')}",
            "description": f"Possible secret '{leak.get('Description', '')}' found at {leak.get('File', '')}:{leak.get('StartLine', '')}",
            "location": leak.get("File", ""),
            "line": leak.get("StartLine"),
            "cwe": "CWE-798",
            "reference": "",
            "remediation": (
                "1. Immediately revoke and rotate the exposed secret.\n"
                "2. Remove the secret from source code.\n"
                "3. Use environment variables or a secrets manager (Azure Key Vault / AWS Secrets Manager).\n"
                "4. If in git history, use `git filter-repo` or BFG Repo-Cleaner to purge."
            ),
            "commit": leak.get("Commit", ""),
            "author": leak.get("Author", ""),
        })
    return findings


# ─────────────────────────────────────────────
# 報告產生
# ─────────────────────────────────────────────

def generate_markdown_report(
    all_findings: list[dict],
    summary_data: dict,
    output_file: Path,
) -> None:
    now = datetime.now().strftime("%Y-%m-%d %H:%M")

    # 統計
    by_severity: dict[str, list] = {}
    by_tool: dict[str, int] = {}
    for f in all_findings:
        sev = SEVERITY_LABEL.get(f["severity"].lower(), f["severity"].capitalize())
        by_severity.setdefault(sev, []).append(f)
        by_tool[f["tool"]] = by_tool.get(f["tool"], 0) + 1

    critical = len(by_severity.get("Critical", []))
    high     = len(by_severity.get("High", []))
    medium   = len(by_severity.get("Medium", []))
    low      = len(by_severity.get("Low", []))
    total    = len(all_findings)

    # 風險評級
    if critical > 0:
        risk_level = "🔴 **CRITICAL RISK** — 立即處理"
    elif high > 0:
        risk_level = "🟠 **HIGH RISK** — 優先修補"
    elif medium > 0:
        risk_level = "🟡 **MEDIUM RISK** — 計劃修補"
    elif low > 0:
        risk_level = "🔵 **LOW RISK** — 可納入下個版本"
    else:
        risk_level = "✅ **PASS** — 未發現已知漏洞"

    lines = [
        "# .NET SAST 安全掃描報告",
        "",
        f"> 產生時間：{now}  ",
        f"> 掃描目標：`{summary_data.get('target', 'N/A')}`  ",
        f"> 掃描耗時：{summary_data.get('duration_seconds', 0)} 秒  ",
        f"> 總體風險：{risk_level}",
        "",
        "---",
        "",
        "## 執行摘要",
        "",
        "| 嚴重程度 | 數量 |",
        "|----------|------|",
        f"| 🔴 Critical | {critical} |",
        f"| 🟠 High     | {high} |",
        f"| 🟡 Medium   | {medium} |",
        f"| 🔵 Low      | {low} |",
        f"| **合計**    | **{total}** |",
        "",
        "### 工具執行狀態",
        "",
        "| 工具 | 狀態 | 發現數 |",
        "|------|------|--------|",
    ]

    tool_status = summary_data.get("tools", {})
    tool_display = {
        "nuget":    "dotnet list package --vulnerable",
        "semgrep":  "Semgrep OSS",
        "scs":      "SecurityCodeScan",
        "gitleaks": "Gitleaks",
    }
    for tool_key, display_name in tool_display.items():
        t = tool_status.get(tool_key, {})
        status = t.get("status", "not_run")
        count = by_tool.get(tool_key, 0)
        status_icon = {"completed":"✅","skipped":"⏭️","error":"❌","not_run":"⬜"}.get(status,"❓")
        if status == "skipped":
            install_hint = f" _(安裝：`{t.get('install','')}`_)"
            lines.append(f"| {display_name} | {status_icon} 未安裝{install_hint} | - |")
        else:
            lines.append(f"| {display_name} | {status_icon} {status} | {count} |")

    lines += ["", "---", "", "## 詳細發現清單", ""]

    # 按嚴重程度輸出
    severity_order = ["Critical","High","Medium","Low","Info"]
    for sev in severity_order:
        sev_findings = by_severity.get(sev, [])
        if not sev_findings:
            continue
        emoji = SEVERITY_EMOJI.get(sev.lower(), "⚪")
        lines += [
            f"### {emoji} {sev}（{len(sev_findings)} 項）",
            "",
        ]
        for i, f in enumerate(sev_findings, 1):
            loc = f.get("location", "")
            line_num = f.get("line")
            location_str = f"`{loc}:{line_num}`" if line_num else f"`{loc}`"

            lines += [
                f"#### {i}. {f['title']}",
                "",
                f"- **工具**：{f['tool']}",
                f"- **位置**：{location_str}",
            ]
            if f.get("cwe"):
                lines.append(f"- **CWE**：{f['cwe']}")
            if f.get("description"):
                lines += ["", f"> {f['description']}", ""]
            if f.get("code_snippet"):
                lines += ["```csharp", f['code_snippet'], "```", ""]
            if f.get("remediation"):
                lines += [
                    "**修補建議：**",
                    "",
                    f.get("remediation", ""),
                    "",
                ]
            if f.get("reference"):
                lines += [f"**參考**：{f['reference']}", ""]
            if f.get("commit"):
                lines += [f"**Git Commit**：`{f['commit']}`（作者：{f.get('author','')}）", ""]
            lines.append("---")
            lines.append("")

    # 總結建議
    lines += [
        "## 修補優先順序建議",
        "",
        "1. **立即處理（本次 Sprint）**：所有 Critical + High 等級漏洞",
        "   - 特別是密鑰洩漏（Gitleaks）、SQL Injection、不安全反序列化",
        "2. **短期計劃（下個 Sprint）**：Medium 等級漏洞",
        "   - 弱加密演算法、缺少 CSRF 保護、Open Redirect",
        "3. **長期改善**：Low / Info 等級",
        "   - Log Injection、TLS 設定優化、過時套件升級",
        "",
        "## 工具安裝快速參考",
        "",
        "```powershell",
        "# Semgrep",
        "pip install semgrep",
        "",
        "# SecurityCodeScan CLI",
        "dotnet tool install --global security-scan",
        "",
        "# Gitleaks（Windows）",
        "choco install gitleaks",
        "# 或",
        "winget install Gitleaks.Gitleaks",
        "```",
        "",
        "---",
        f"*報告由 dotnet-sast-pipeline 自動產生於 {now}*",
    ]

    output_file.write_text("\n".join(lines), encoding="utf-8")
    print(f"✅ 報告已輸出：{output_file}")
    print(f"   Critical: {critical} | High: {high} | Medium: {medium} | Low: {low} | Total: {total}")


# ─────────────────────────────────────────────
# 主程式
# ─────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description=".NET SAST 結果彙整報告產生器")
    parser.add_argument("--input",  "-i", required=True, help="sast-output 目錄路徑")
    parser.add_argument("--output", "-o", default="", help="輸出報告路徑（預設：<input>/security-report.md）")
    parser.add_argument("--format", "-f", choices=["markdown","md"], default="markdown")
    args = parser.parse_args()

    input_dir = Path(args.input)
    if not input_dir.exists():
        print(f"❌ 輸入目錄不存在：{input_dir}", file=sys.stderr)
        sys.exit(1)

    output_file = Path(args.output) if args.output else (input_dir / "security-report.md")

    # 載入 summary.json
    summary_file = input_dir / "summary.json"
    summary_data: dict = {}
    if summary_file.exists():
        with open(summary_file, encoding="utf-8") as f:
            summary_data = json.load(f)

    # 解析所有工具結果
    all_findings: list[dict] = []
    all_findings += parse_nuget(input_dir)
    all_findings += parse_semgrep(input_dir)
    all_findings += parse_scs_sarif(input_dir)
    all_findings += parse_gitleaks(input_dir)

    # 依嚴重程度排序
    all_findings.sort(key=lambda f: sev_rank(f.get("severity", "info")))

    print(f"📋 共解析到 {len(all_findings)} 個發現")
    generate_markdown_report(all_findings, summary_data, output_file)


if __name__ == "__main__":
    main()
