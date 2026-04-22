# 🚀 SCS Security Check — 同事快速上手指南

## 第一次使用

### 1. 複製專案到本機

```bash
git clone https://github.com/yourusername/SCS_SecurityCheck.git
cd SCS_SecurityCheck
```

### 2. VS Code 會自動載入 Skills

- 打開 VS Code
- 打開專案資料夾
- 重啟 Claude Code（或按 `Ctrl+Shift+P` 搜尋「Claude: Reload」）
- 4 個 skills 會自動出現在 Claude Code 的 skill 清單中

### 3. 立即可用的命令

在 **Claude Code Chat** 中輸入任何一個：

#### 🤖 AI 語義掃描（推薦）
```
執行 AI 資安掃描 on D:\MyProject\MySolution.sln
```
或
```
Run AI security audit on this project
```

#### 🔧 完整工具版掃描
```
對此專案執行 .NET SAST 弱點掃描
```

#### 📋 OWASP 安全審查
```
執行 OWASP Top 10 安全審查
```

#### 🛡️ Claude 設定檔掃描
```
掃描 Claude Code 設定安全性
```

---

## Skills 說明

| Skill | 能力 | 安裝工具需求 |
|-------|------|------------|
| **dotnet-ai-security-audit** | 語義分析、跨檔案邏輯推理、修補建議 | ❌ 無 |
| **dotnet-sast-pipeline** | Regex 模式匹配、Git 密鑰、CVE 掃描 | ✅ Docker |
| **security-review** | OWASP Top 10 逐項檢查 | ❌ 無 |
| **security-scan** | Claude Code 設定檔驗證 | ❌ 無 |

**建議工作流：** AI 掃描（快速） → 工具版掃描（深度） → 人工審查

---

## 開發環境設定

```bash
# 1. 復原 NuGet
dotnet restore

# 2. 建立資料庫
dotnet ef database update --startup-project src/SCS.SecurityCheck.Api

# 3. 啟動後端（Ctrl+C 停止）
dotnet run --project src/SCS.SecurityCheck.Api
# 會在 http://localhost:5245 啟動

# 4. 在 VS Code 中測試
# 開啟 SCS.SecurityCheck.Api.http，點擊藍色 "Send Request" 按鈕
```

---

## 更新 Skills（給開發者）

如果開發者執行了 `sync-skills-to-project.ps1` 並推送了新版本：

```bash
git pull
```

Claude Code 會自動偵測更新的 skills。無需重新安裝。

---

## 常見問題

### Q: 為什麼 Claude Code 找不到 skills？
**A:** 重啟 VS Code 或 Claude Code：
- 按 `Ctrl+Shift+P`
- 搜尋 "Reload"
- 選 "Claude: Reload Window"

### Q: 能在其他專案中使用這些 skills 嗎？
**A:** 可以！執行（需開發者權限）：
```powershell
Copy-Item -Recurse .\claude\skills\* "$env:USERPROFILE\.claude\skills\"
```

### Q: AI 掃描有什麼限制嗎？
**A:** 
- 單次掃描建議 ≤ 2000 個檔案
- 單檔案建議 ≤ 512 KB
- 需要有效的 Claude API key（若使用外部 AI provider）

### Q: 掃描結果是 Markdown 嗎？
**A:** 是的！可直接貼到 GitHub Issues、PR 描述或文檔中。支援表格、代碼區塊、引用。

---

## 快速排查

| 問題 | 解決方案 |
|------|--------|
| Skills 沒有自動加載 | 重啟 VS Code → `Ctrl+Shift+P` → Claude: Reload |
| Git 拉取後看不到新 skills | 刪除 `~/.claude/cache`，重啟 VS Code |
| AI 掃描超時 | 減少掃描檔案數或分割專案 |
| API 回應 401 | 檢查 API 伺服器是否運行；API 要求登入 |

---

## 進階：自訂掃描路徑

編輯 `appsettings.Development.json`：

```json
{
  "ScanAllowedPaths": [
    "D:\\MyProject",
    "D:\\AnotherFolder\\src"
  ]
}
```

只有白名單內的路徑才能被掃描。

---

## 技術棧

- **.NET 9.0 ASP.NET Core** — 後端 API
- **EF Core** — 資料持久化
- **Claude AI** — 語義漏洞分析
- **Semgrep** — 模式匹配工具
- **Gitleaks** — 密鑰洩漏檢測

---

## 聯繫

問題或建議？ → GitHub Issues

祝掃描順利！ 🎯
