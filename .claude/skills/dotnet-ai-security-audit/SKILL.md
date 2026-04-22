---
name: dotnet-ai-security-audit
description: >
  Pure AI-powered .NET/C# security vulnerability scanner. Uses Claude's code reasoning
  to detect OWASP Top 10 issues without installing any external tools.
  Requires only Claude Code with an Opus-class model and API credits.
  Detects: SQL Injection, XSS, CSRF, hardcoded secrets, weak crypto,
  missing authorization, path traversal, dangerous deserialization, and more.
trigger: "AI audit|ai security scan|ai 資安掃描|ai 弱點掃描|ai 安全審查|不裝工具掃描|claude scan"
---

# .NET / C# AI-Powered Security Audit

## 何時使用此 Skill

- 環境無法安裝 Semgrep / Gitleaks 等外部工具
- 想要 AI 對程式碼邏輯做深度語義分析（非單純 pattern match）
- 需要修補建議與說明，而不只是行號

**前提：** 僅需 Claude Code（使用 Opus 級模型效果最佳）

---

## 掃描流程（AI 自動執行）

```
1. 掃描專案所有 .cs 檔案（排除 bin/ obj/ 測試專案）
2. 逐一讀取並分析，對照下方弱點清單
3. 輸出結構化報告：嚴重程度 / 位置 / 問題描述 / 修補範例
```

---

## 自動觸發語句（以下任一均可啟動）

```
對 D:\MyProject 執行 AI 資安掃描
Run AI security audit on D:\MyProject\MySolution.sln
不安裝工具，用 AI 掃描這個 .NET 專案的弱點
Claude 幫我看看這個 C# 專案有沒有安全漏洞
```

---

## AI 掃描目標弱點（OWASP Top 10 + C# 常見問題）

### A03 — Injection

**SQL Injection（最高優先）**

偵測模式：
- `SqlCommand` 使用字串拼接而非 `SqlParameter`
- `ExecuteSqlRaw` / `FromSqlRaw` 夾帶使用者輸入
- `string.Format(` 或 `$"` 出現在 SQL 相關字串
- Entity Framework `FromSqlRaw` 未用參數化

壞的範例：
```csharp
// VULNERABLE
var cmd = new SqlCommand("SELECT * FROM Users WHERE Id = " + userId, conn);
context.Users.FromSqlRaw("SELECT * FROM Users WHERE Name = '" + name + "'");
```

好的範例：
```csharp
// SAFE
var cmd = new SqlCommand("SELECT * FROM Users WHERE Id = @id", conn);
cmd.Parameters.AddWithValue("@id", userId);
context.Users.FromSqlRaw("SELECT * FROM Users WHERE Name = {0}", name);
```

---

**LDAP / XPath / Command Injection**

偵測模式：
- `Process.Start` 夾帶使用者輸入
- `Directory.GetFiles` 使用未驗證路徑

---

### A02 — 加密失敗

偵測模式：
- `MD5.Create()` / `SHA1.Create()` 用於密碼雜湊
- `DESCryptoServiceProvider` / `RC2CryptoServiceProvider`
- `new Random()` 用於安全相關用途（應用 `RandomNumberGenerator`）
- `Convert.ToBase64String` 當作加密

---

### A01 — 存取控制缺失

偵測模式：
- Controller Action 有 `[HttpPost]` / `[HttpDelete]` / `[HttpPut]` 但無 `[Authorize]`
- 角色判斷用字串比對但未正規化（大小寫問題）
- 手動 JWT 解析而非使用 `[Authorize]` middleware

---

### A07 — 認證與工作階段失敗

偵測模式：
- 硬編碼密碼：`password = "..."` / `Password=xxx` 在原始碼
- 硬編碼 API Key：`ApiKey = "..."` / `Authorization: Bearer hardcoded`
- `HttpOnly = false` Cookie 設定
- Session timeout 未設定

---

### A04 — 不安全設計

偵測模式：
- `BinaryFormatter` 使用（已廢棄且危險）
- `TypeNameHandling.All` 或 `TypeNameHandling.Auto`（Json.NET）
- `XmlDocument` 未停用 DTD（XXE 攻擊）：
  ```csharp
  // VULNERABLE（預設允許 DTD）
  var doc = new XmlDocument();
  doc.Load(input);
  
  // SAFE
  var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
  ```

---

### A05 — 錯誤設定

偵測模式：
- `app.UseDeveloperExceptionPage()` 未限定在 Development 環境
- CORS `AllowAnyOrigin()` + `AllowCredentials()` 同時使用
- `appsettings.json` 含真實連線字串密碼（非 Secret Manager）

---

### A10 — SSRF

偵測模式：
- `HttpClient.GetAsync(userInput)` 無域名白名單
- `WebClient.DownloadString(url)` 直接用使用者輸入

---

### Path Traversal

偵測模式：
- `File.ReadAllText(Path.Combine(basePath, userInput))` 未驗證
- `Server.MapPath(Request["path"])`

---

### XSS（Razor 特定）

偵測模式：
- `@Html.Raw(userInput)`
- `Response.Write(input)` 未編碼

---

### A09 — Log Injection

偵測模式：
- `logger.LogInformation($"User: {request.Username}")` — 使用者輸入直接進 log

---

## 執行步驟（AI 自動完成）

```
Step 1: 列出專案所有 .cs 檔案
        (排除 bin/, obj/, Migrations/, *.Designer.cs)

Step 2: 逐檔讀取，對照上方清單分析
        - 每個弱點記錄：檔名、行號、弱點類型、程式碼片段

Step 3: 輸出報告（Markdown 格式）
        格式：
        | 嚴重度 | 類型 | 檔案 | 行號 | 說明 | 修補方式 |
        
Step 4: 提供優先修補順序建議
```

---

## 報告輸出格式

```markdown
## AI 資安掃描報告
**專案：** MySolution
**掃描時間：** YYYY-MM-DD
**模型：** Claude Opus

### 摘要
| 嚴重度 | 數量 |
|--------|------|
| 🔴 Critical | N |
| 🟠 High | N |
| 🟡 Medium | N |
| 🟢 Low | N |

### 詳細發現

#### 🔴 [Critical] SQL Injection
- **位置：** `src/Services/UserService.cs` Line 42
- **程式碼：**
  ```csharp
  var cmd = new SqlCommand("SELECT * FROM Users WHERE Id = " + userId);
  ```
- **風險：** 攻擊者可注入任意 SQL，取得 / 修改 / 刪除資料庫資料
- **修補：**
  ```csharp
  var cmd = new SqlCommand("SELECT * FROM Users WHERE Id = @id", conn);
  cmd.Parameters.AddWithValue("@id", userId);
  ```
- **CWE：** CWE-89
```

---

## 注意事項

1. AI 分析為**語義級別**掃描，可辨識跨行、多行的漏洞邏輯
2. 可能有**誤報**（False Positive）— 需人工確認業務邏輯
3. 大型專案（>200 個 .cs 檔）建議分模組掃描以保持品質
4. **不會執行程式碼**，純讀取分析，對生產環境安全

---

## 與工具版的差異

| 項目 | AI 版（此 skill）| 工具版（dotnet-sast-pipeline）|
|------|----------------|-------------------------------|
| 安裝需求 | 僅 Claude Code | Semgrep + Gitleaks + security-scan |
| 語義理解 | ✅ 可跨行分析邏輯 | ⚠️ Pattern match 為主 |
| 速度 | 中（依檔案數量）| 快 |
| Git 歷史掃描 | ❌ | ✅（Gitleaks）|
| NuGet CVE | ❌ | ✅（dotnet list package --vulnerable）|
| 建議使用情境 | 快速審查、無工具環境 | CI/CD、完整掃描 |

**最佳實踐：兩者搭配使用**
