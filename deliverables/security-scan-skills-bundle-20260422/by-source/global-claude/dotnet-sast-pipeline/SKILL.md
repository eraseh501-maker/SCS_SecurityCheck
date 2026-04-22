---
name: dotnet-sast-pipeline
description: >
  .NET / C# 專案完整 SAST（靜態應用程式安全測試）掃描流程。整合 dotnet list package --vulnerable、Semgrep、SecurityCodeScan、Gitleaks 等工具，自動掃描程式碼漏洞、相依性 CVE、密鑰洩漏，並產生含修補建議的 Markdown 安全報告。
  適用情境：(1) 對 .NET C# 大型商用專案進行安全審查；(2) PR/CI 前的安全閘門檢查；(3) 定期安全健康檢查；(4) 需要 OWASP Top 10 覆蓋率報告；(5) 任何「掃描 .NET 漏洞」「找出 C# 安全問題」「建立 dotnet 安全報告」的請求。
---

# .NET SAST Pipeline

## 快速開始（三步驟）

### 步驟 1：確認環境
```powershell
dotnet --version    # 需要 .NET SDK 6.0+
python --version    # 需要 Python 3.8+（用於報告產生）
```

### 步驟 2：執行掃描
```powershell
# 複製 scripts/run_sast.ps1 到專案目錄後執行
.\run_sast.ps1 -SolutionPath "C:\Path\To\YourProject"

# 指定特定工具
.\run_sast.ps1 -SolutionPath "C:\Path\To\MyApp.sln" -Tools "nuget,semgrep"

# 只報告 High 以上
.\run_sast.ps1 -SolutionPath "C:\Path\To\MyApp" -SeverityThreshold "high"
```

### 步驟 3：產生報告
```bash
python generate_report.py --input ./sast-output
# 報告輸出：./sast-output/security-report.md
```

---

## 工具組成

| 優先 | 工具 | 負責範疇 | 安裝 |
|------|------|---------|------|
| P0 | `dotnet list package --vulnerable` | NuGet 相依性 CVE | 內建，零安裝 |
| P0 | Semgrep OSS | OWASP Top 10 靜態分析 | `pip install semgrep` |
| P1 | SecurityCodeScan | Roslyn C# 特定漏洞 | `dotnet tool install -g security-scan` |
| P1 | Gitleaks | Git 歷史密鑰洩漏 | `choco install gitleaks` |

> **詳細安裝與 CI/CD 整合**：參閱 [references/tool-setup.md](references/tool-setup.md)

---

## 掃描工作流程

```
專案路徑
  ├─► dotnet list package --vulnerable  →  nuget-vulnerabilities.txt
  ├─► Semgrep (p/csharp + p/owasp-top-ten)  →  semgrep-results.json
  ├─► SecurityCodeScan (SARIF)  →  scs-results.sarif
  ├─► Gitleaks  →  gitleaks-report.json
  └─► generate_report.py  →  security-report.md  ← 最終交付物
```

---

## 漏洞類型覆蓋（OWASP Top 10）

| 類別 | 偵測工具 | C# 特定模式 |
|------|---------|------------|
| A01 存取控制缺失 | Semgrep, SCS | 缺少 `[Authorize]`、IDOR |
| A02 加密失敗 | Semgrep, SCS | MD5/SHA1/DES、弱 TLS |
| A03 Injection | Semgrep, SCS | SQL/LDAP/XPath Injection |
| A04 不安全設計 | SCS | BinaryFormatter 反序列化 |
| A05 錯誤設定 | Semgrep | 開發者例外頁、CORS 過寬 |
| A06 易受攻擊元件 | dotnet vulnerable | NuGet CVE |
| A07 認證失敗 | SCS | 硬編碼憑證、弱 Session |
| A09 記錄監控 | Semgrep | Log injection |
| A10 SSRF | Semgrep, SCS | HttpClient 用戶控制 URL |
| 密鑰洩漏 | Gitleaks | API Key、ConnectionString |
| Path Traversal | Semgrep, SCS | Path.Combine 未驗證 |
| XSS | Semgrep, SCS | Html.Raw、Response.Write |
| CSRF | SCS | 缺少 AntiForgeryToken |
| XXE | SCS | XmlDocument 未設定 |
| Open Redirect | SCS | Response.Redirect 未驗證 |

> **各漏洞詳細模式與修補範例**：參閱 [references/dotnet-vuln-patterns.md](references/dotnet-vuln-patterns.md)

---

## 不安裝工具的 AI 輔助掃描模式

當環境無法安裝工具時，AI 可直接分析原始碼：

1. 用 Glob 找到所有 `.cs` 檔案
2. 用 Grep 搜尋危險 pattern（見下方）
3. 對照 `dotnet-vuln-patterns.md` 確認漏洞
4. 輸出位置 + 修補建議

### 常用 Grep 指令
```bash
# SQL Injection
grep -rn "ExecuteSqlRaw\|FromSqlRaw\|SqlCommand" --include="*.cs" ./src

# 硬編碼密碼
grep -rni "password\s*=\s*\"" --include="*.cs" ./src

# 弱加密
grep -rn "MD5\.Create\|SHA1\.Create\|DES\.Create\|BinaryFormatter" --include="*.cs" ./src

# 危險反序列化
grep -rn "TypeNameHandling\.All\|TypeNameHandling\.Auto" --include="*.cs" ./src

# 缺少授權（需人工審查）
grep -rn "\[HttpPost\]\|\[HttpDelete\]\|\[HttpPut\]" --include="*.cs" ./src

# 密鑰洩漏
grep -rni "Password=\|ApiKey\s*=\s*\"\|secret\s*=\s*\"" --include="*.cs" ./src

# Path Traversal
grep -rn "Path\.Combine.*Request\|File\.ReadAllText.*param" --include="*.cs" ./src

# 不安全亂數
grep -rn "new Random()" --include="*.cs" ./src
```

---

## 腳本

| 腳本 | 功能 |
|------|------|
| [scripts/run_sast.ps1](scripts/run_sast.ps1) | Windows PowerShell 一鍵掃描，輸出到 `sast-output/` |
| [scripts/generate_report.py](scripts/generate_report.py) | 解析四個工具結果，產生 Markdown 安全報告 |

---

## 報告輸出說明

`security-report.md` 包含：
1. **執行摘要**：風險評級（Critical/High/Medium/Low/Pass）、各工具狀態
2. **詳細發現清單**：依嚴重程度排序，每條含工具來源、檔案位置、CWE、**具體修補建議**
3. **修補優先順序建議**：Sprint 規劃參考
4. **工具安裝快速參考**

報告範本：[assets/report-template.md](assets/report-template.md)

---

## 常見問題

**Q：部分工具未安裝，可以只跑已安裝的嗎？**
是的，`run_sast.ps1` 自動偵測並跳過未安裝工具，仍會產生可用報告。

**Q：如何整合到 GitHub Actions / Azure DevOps？**
參閱 [references/tool-setup.md](references/tool-setup.md) 的 CI/CD 章節。

**Q：Gitleaks 發現密鑰如何徹底清除？**
立即輪換洩漏的密鑰，再用 `git filter-repo` 或 BFG Repo-Cleaner 清除 git 歷史。
