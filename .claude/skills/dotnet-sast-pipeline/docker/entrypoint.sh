#!/bin/bash
# ─────────────────────────────────────────────────────────────
# .NET SAST Scanner — Container Entrypoint
# 在 Docker 容器內依序執行所有掃描工具
# ─────────────────────────────────────────────────────────────

set -euo pipefail

# ── 預設參數 ──
SCAN_DIR="/scan"
OUTPUT_DIR="/scan/sast-output"
TOOLS="${TOOLS:-all}"
SEVERITY="${SEVERITY:-medium}"
LOG_FILE=""

# ── 解析命令列參數（覆蓋環境變數）──
while [[ $# -gt 0 ]]; do
    case $1 in
        --tools)    TOOLS="$2";    shift 2 ;;
        --severity) SEVERITY="$2"; shift 2 ;;
        --output)   OUTPUT_DIR="$2"; shift 2 ;;
        *) echo "未知參數：$1"; shift ;;
    esac
done

# ── 初始化輸出目錄 ──
mkdir -p "$OUTPUT_DIR"
LOG_FILE="$OUTPUT_DIR/sast-run.log"
START_TIME=$(date +%s)

# ── 工具函式 ──
log() {
    local level="${1:-INFO}"
    local msg="${2:-}"
    local ts
    ts=$(date '+%Y-%m-%d %H:%M:%S')
    local color=""
    case "$level" in
        ERROR)   color="\033[0;31m" ;;
        WARNING) color="\033[0;33m" ;;
        SUCCESS) color="\033[0;32m" ;;
        *)       color="\033[0;36m" ;;
    esac
    echo -e "${color}[${ts}][${level}] ${msg}\033[0m"
    echo "[${ts}][${level}] ${msg}" >> "$LOG_FILE"
}

tool_enabled() {
    [[ "$TOOLS" == "all" ]] || echo "$TOOLS" | grep -qw "$1"
}

# ── 驗證掃描目錄 ──
if [[ ! -d "$SCAN_DIR" ]]; then
    log "ERROR" "掃描目錄不存在：$SCAN_DIR"
    log "ERROR" "請確認已正確掛載：-v \"/path/to/project:/scan\""
    exit 1
fi

log "INFO" "╔════════════════════════════════════════╗"
log "INFO" "║   .NET SAST Pipeline — Docker 掃描    ║"
log "INFO" "╚════════════════════════════════════════╝"
log "INFO" "掃描目標：$SCAN_DIR"
log "INFO" "輸出目錄：$OUTPUT_DIR"
log "INFO" "啟用工具：$TOOLS"
log "INFO" "嚴重程度：$SEVERITY"

# ── 初始化 summary.json ──
SUMMARY_FILE="$OUTPUT_DIR/summary.json"
cat > "$SUMMARY_FILE" <<EOF
{
  "run_at": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')",
  "target": "$SCAN_DIR",
  "tools": {}
}
EOF

update_summary() {
    local tool="$1"
    local status="$2"
    local extra="${3:-}"
    python3 -c "
import json, sys
with open('$SUMMARY_FILE') as f: d = json.load(f)
d['tools']['$tool'] = {'status': '$status'}
if '$extra':
    d['tools']['$tool'].update(json.loads('$extra'))
with open('$SUMMARY_FILE', 'w') as f: json.dump(d, f, indent=2)
" 2>/dev/null || true
}

# ════════════════════════════════════════════
# 工具 1：dotnet list package --vulnerable
# ════════════════════════════════════════════
if tool_enabled "nuget"; then
    log "INFO" "── [1/4] NuGet 相依性漏洞掃描 ──"
    NUGET_OUT="$OUTPUT_DIR/nuget-vulnerabilities.txt"
    NUGET_JSON="$OUTPUT_DIR/nuget-vulnerabilities.json"

    # 尋找 .sln 或 .csproj
    SLN_FILE=$(find "$SCAN_DIR" -maxdepth 3 -name "*.sln" | head -1)
    CSPROJ_FILE=$(find "$SCAN_DIR" -maxdepth 3 -name "*.csproj" | head -1)
    SCAN_TARGET="${SLN_FILE:-${CSPROJ_FILE:-$SCAN_DIR}}"

    if [[ -n "$SLN_FILE" ]] || [[ -n "$CSPROJ_FILE" ]]; then
        log "INFO" "掃描目標：$SCAN_TARGET"

        # 還原 NuGet 套件（需要網路，失敗不中止）
        dotnet restore "$SCAN_TARGET" --nologo -q 2>/dev/null || \
            log "WARNING" "dotnet restore 失敗，部分結果可能不完整"

        dotnet list "$SCAN_TARGET" package --vulnerable --include-transitive \
            2>&1 | tee "$NUGET_OUT"

        VULN_COUNT=$(grep -c "^[[:space:]]*>" "$NUGET_OUT" 2>/dev/null) || VULN_COUNT=0
        log "$([ "${VULN_COUNT}" -gt 0 ] && echo 'WARNING' || echo 'SUCCESS')" \
            "NuGet 掃描完成：發現 ${VULN_COUNT} 個漏洞套件"
        update_summary "nuget" "completed" "{\"vuln_count\": $VULN_COUNT}"
    else
        log "WARNING" "找不到 .sln 或 .csproj 檔案，跳過 NuGet 掃描"
        update_summary "nuget" "skipped" '{"reason": "no project file found"}'
    fi
fi

# ════════════════════════════════════════════
# 工具 2：Semgrep
# ════════════════════════════════════════════
if tool_enabled "semgrep"; then
    log "INFO" "── [2/4] Semgrep 靜態分析 ──"
    SEMGREP_OUT="$OUTPUT_DIR/semgrep-results.json"

    SEMGREP_ARGS=(
        "--config" "p/csharp"
        "--config" "p/owasp-top-ten"
        "--output" "$SEMGREP_OUT"
        "--json"
        "--quiet"
        "--no-git-ignore"
    )

    # 依嚴重程度篩選
    case "$SEVERITY" in
        critical) SEMGREP_ARGS+=("--severity" "ERROR") ;;
        high)     SEMGREP_ARGS+=("--severity" "ERROR" "--severity" "WARNING") ;;
        *)        ;; # medium/low：包含所有
    esac

    SEMGREP_ARGS+=("$SCAN_DIR")

    if semgrep "${SEMGREP_ARGS[@]}" 2>&1; then
        FIND_COUNT=0
        if [[ -f "$SEMGREP_OUT" ]]; then
            FIND_COUNT=$(python3 -c \
                "import json; d=json.load(open('$SEMGREP_OUT')); print(len(d.get('results',[])))" \
                2>/dev/null || echo "0")
        fi
        log "$([ "$FIND_COUNT" -gt 0 ] && echo 'WARNING' || echo 'SUCCESS')" \
            "Semgrep 完成：${FIND_COUNT} 個發現"
        update_summary "semgrep" "completed" "{\"finding_count\": $FIND_COUNT}"
    else
        log "ERROR" "Semgrep 執行失敗"
        update_summary "semgrep" "error" '{"error": "semgrep exited with error"}'
    fi
fi

# ════════════════════════════════════════════
# 工具 3：SecurityCodeScan
# ════════════════════════════════════════════
if tool_enabled "scs"; then
    log "INFO" "── [3/4] SecurityCodeScan 分析 ──"
    SCS_OUT="$OUTPUT_DIR/scs-results.sarif"

    SLN_FILE=$(find "$SCAN_DIR" -maxdepth 3 -name "*.sln" | head -1)
    CSPROJ_FILE=$(find "$SCAN_DIR" -maxdepth 3 -name "*.csproj" | head -1)
    SCS_TARGET="${SLN_FILE:-$CSPROJ_FILE}"

    if [[ -n "$SCS_TARGET" ]]; then
        if security-scan "$SCS_TARGET" --export=sarif --output="$SCS_OUT" 2>&1; then
            FIND_COUNT=0
            if [[ -f "$SCS_OUT" ]]; then
                FIND_COUNT=$(python3 -c \
                    "import json; d=json.load(open('$SCS_OUT')); \
                     print(sum(len(r.get('results',[])) for r in d.get('runs',[])))" \
                    2>/dev/null || echo "0")
            fi
            log "$([ "$FIND_COUNT" -gt 0 ] && echo 'WARNING' || echo 'SUCCESS')" \
                "SecurityCodeScan 完成：${FIND_COUNT} 個發現"
            update_summary "scs" "completed" "{\"finding_count\": $FIND_COUNT}"
        else
            log "WARNING" "SecurityCodeScan 執行遇到問題，結果可能不完整"
            update_summary "scs" "warning" '{"note": "partial results"}'
        fi
    else
        log "WARNING" "找不到 .sln 或 .csproj，跳過 SecurityCodeScan"
        update_summary "scs" "skipped" '{"reason": "no project file found"}'
    fi
fi

# ════════════════════════════════════════════
# 工具 4：Gitleaks
# ════════════════════════════════════════════
if tool_enabled "gitleaks"; then
    log "INFO" "── [4/4] Gitleaks 密鑰洩漏掃描 ──"
    GITLEAKS_OUT="$OUTPUT_DIR/gitleaks-report.json"

    # 判斷是否為 git repo
    if [[ -d "$SCAN_DIR/.git" ]]; then
        gitleaks detect \
            --source "$SCAN_DIR" \
            --report-format json \
            --report-path "$GITLEAKS_OUT" \
            --exit-code 0 \
            2>&1 | tail -5

        LEAK_COUNT=0
        if [[ -f "$GITLEAKS_OUT" ]]; then
            LEAK_COUNT=$(python3 -c \
                "import json; d=json.load(open('$GITLEAKS_OUT')); \
                 print(len(d) if isinstance(d,list) else 0)" \
                2>/dev/null || echo "0")
        fi
        log "$([ "$LEAK_COUNT" -gt 0 ] && echo 'WARNING' || echo 'SUCCESS')" \
            "Gitleaks 完成：${LEAK_COUNT} 個密鑰洩漏發現"
        update_summary "gitleaks" "completed" "{\"leak_count\": $LEAK_COUNT}"
    else
        # 非 git repo：掃描檔案系統（不含 git 歷史）
        log "WARNING" "非 git repository，改用檔案系統掃描模式"
        gitleaks detect \
            --source "$SCAN_DIR" \
            --no-git \
            --report-format json \
            --report-path "$GITLEAKS_OUT" \
            --exit-code 0 \
            2>&1 | tail -5

        LEAK_COUNT=$(python3 -c \
            "import json; d=json.load(open('$GITLEAKS_OUT')); \
             print(len(d) if isinstance(d,list) else 0)" \
            2>/dev/null || echo "0")
        log "$([ "$LEAK_COUNT" -gt 0 ] && echo 'WARNING' || echo 'SUCCESS')" \
            "Gitleaks（檔案模式）完成：${LEAK_COUNT} 個發現"
        update_summary "gitleaks" "completed" \
            "{\"leak_count\": $LEAK_COUNT, \"mode\": \"filesystem\"}"
    fi
fi

# ════════════════════════════════════════════
# 產生 Markdown 報告
# ════════════════════════════════════════════
log "INFO" "── 產生安全報告 ──"

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

# 更新 duration 到 summary
python3 -c "
import json
with open('$SUMMARY_FILE') as f: d = json.load(f)
d['duration_seconds'] = $DURATION
with open('$SUMMARY_FILE', 'w') as f: json.dump(d, f, indent=2)
" 2>/dev/null || true

if python3 /sast-tools/generate_report.py --input "$OUTPUT_DIR"; then
    REPORT_FILE="$OUTPUT_DIR/security-report.md"
    log "SUCCESS" "╔════════════════════════════════════════════════╗"
    log "SUCCESS" "║              掃描完成！                        ║"
    log "SUCCESS" "╚════════════════════════════════════════════════╝"
    log "INFO"    "耗時：${DURATION} 秒"
    log "INFO"    "報告位置：/scan/sast-output/security-report.md"
    log "INFO"    "在主機對應路徑查看報告（就是你掛載的專案目錄下的 sast-output/）"
else
    log "ERROR" "報告產生失敗，原始結果仍可在 $OUTPUT_DIR 查看"
    exit 1
fi
