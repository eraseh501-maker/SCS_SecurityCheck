# SCS Security Check — .NET/C# 資安掃描平台

一個用 .NET 9.0 + ASP.NET Core 構建的資安漏洞掃描系統，提供工具版與 AI 語義版雙重掃描能力，對標 SonarQube 級別的發現精度。

---

## 快速開始

### 必要條件

- **.NET 9.0 SDK** (或以上)
- **SQL Server** (LocalDB 或遠端)
- **VS Code + Claude Code**（使用 AI 掃描時）

### 本機開發

```bash
# 1. 複製專案
git clone https://github.com/yourusername/SCS_SecurityCheck.git
cd SCS_SecurityCheck

# 2. 還原 NuGet 套件
dotnet restore

# 3. 更新資料庫
dotnet ef database update --startup-project src/SCS.SecurityCheck.Api

# 4. 啟動 API
dotnet run --project src/SCS.SecurityCheck.Api

# 5. 在 http://localhost:5245 存取 API
```

---

## 📦 Claude Code Skills（本機包含）

此專案包含 **4 個專業資安掃描 skills**，存放在 `.claude/skills/` 中：

| Skill | 說明 | 觸發方式 |
|-------|------|---------|
| `dotnet-sast-pipeline` | 完整工具版掃描（Semgrep + Gitleaks + SecurityCodeScan）| 「對此專案執行 .NET SAST 弱點掃描」|
| `dotnet-ai-security-audit` | **⭐ AI 語義掃描**（不需安裝工具）| 「執行 AI 資安掃描」|
| `security-review` | OWASP Top 10 逐項審查 | 「執行 OWASP 安全審查」|
| `security-scan` | AgentShield 設定檔弱點 | 「掃描 Claude Code 設定」|

### 安裝 Skills

**Option A — 自動載入（推薦）**

VS Code 會自動偵測本地 `.claude/skills/` 目錄，無需額外安裝。

**Option B — 複製到全域**

若想在其他專案中也使用這些 skills：

```powershell
Copy-Item -Recurse .\.claude\skills\* "$env:USERPROFILE\.claude\skills\"
```

### 使用 Skills

在 **VS Code Copilot Chat** 中輸入：

```
對 D:\MyProject 執行 AI 資安掃描
```

或

```
Run AI security audit on D:\MyProject\MySolution.sln
```

---

## 🔧 維護 Skills

### 同步更新（開發者用）

每次新增或更新全域 skills 後，執行：

```powershell
.\sync-skills-to-project.ps1
```

這會自動從 `~/.claude/skills` 複製最新版本到 `.claude/skills`，確保團隊同步。

### 提交到 Git

```bash
git add .claude/
git commit -m "chore: sync skills - add new security checks"
git push
```

同事 `git pull` 後會自動獲得最新 skills。

### 安全推送流程（必做）

每次推送到 GitHub 前，請先執行以下檢查，避免推到錯誤倉庫：

```bash
# 1. 確認目前 Git 根目錄必須是本專案
git rev-parse --show-toplevel

# 2. 確認遠端 URL
git remote -v

# 3. 確認當前分支
git branch --show-current
```

本專案已啟用 pre-push 安全確認機制（`.githooks/pre-push`），推送時會顯示：

- Remote 名稱
- Remote URL
- 分支名稱

你必須輸入 `YES` 才會繼續 push。

標準推送命令：

```bash
git push -u origin main
```

若看到 URL 不是 `https://github.com/eraseh501-maker/SCS_SecurityCheck.git`，請立刻中止並修正 remote。

---

## 📋 API 端點

### 執行安全掃描（需登入）

```http
POST /api/scans/run
Content-Type: application/json

{
  "projectPath": "D:\\MyProject\\MySolution.sln",
  "enableAiSuggestions": true,
  "aiProvider": "claude",
  "apiKey": "sk-ant-...",
  "maxFiles": 2000,
  "maxFileSizeKb": 512
}
```

**回應：** Markdown 格式的掃描報告，包含 Critical / High / Medium / Low 級別的發現。

---

## 🧪 測試

```bash
dotnet test
```

包含 unit tests（SecurityScannerService） 和 integration tests。

---

## 🛡️ 安全性

此專案本身是資安產品，所有輸入都經過驗證：

- ✅ **路徑白名單** — 掃描路徑限制在 `ScanAllowedPaths` 設定內
- ✅ **授權保護** — `/api/scans/run` 需登入（`.RequireAuthorization()`）
- ✅ **參數化查詢** — 無 SQL Injection（EF Core）
- ✅ **密碼安全** — 使用 `User.Identity.Name` 而非硬編碼

---

## 📊 掃描能力對比

| 能力 | 工具版（Semgrep）| AI 版（Claude） |
|------|----------------|-----------------|
| SQL Injection 檢測 | ✅ Regex pattern | ✅ **語義分析** |
| Git 密鑰掃描 | ✅ Gitleaks | ❌ |
| NuGet CVE | ✅ dotnet list package --vulnerable | ❌ |
| 跨檔案邏輯分析 | ⚠️ 有限 | ✅ **深度推理** |
| 無需工具安裝 | ❌ | ✅ |
| 修補建議 | ⚠️ 基本 | ✅ **詳細說明** |

**最佳實踐：** 兩者搭配使用，互補不足。

---

## 🤝 貢獻

1. 新增 skill 後執行 `.\sync-skills-to-project.ps1`
2. `git add .claude/` + `git commit` + `git push`
3. 同事會自動收到更新

---

## 📄 授權

MIT License

---

## 聯繫

如有問題，請在 GitHub Issues 提出。
