# .NET / C# 常見漏洞模式速查表

## TOC
1. [SQL Injection](#1-sql-injection)
2. [XSS](#2-xss)
3. [CSRF](#3-csrf)
4. [不安全反序列化](#4-不安全反序列化)
5. [Path Traversal](#5-path-traversal)
6. [SSRF](#6-ssrf)
7. [XXE](#7-xxe)
8. [硬編碼憑證](#8-硬編碼憑證)
9. [弱加密](#9-弱加密)
10. [授權缺失](#10-授權缺失)
11. [Open Redirect](#11-open-redirect)
12. [Log Injection](#12-log-injection)
13. [不安全亂數](#13-不安全亂數)
14. [不安全 TLS 設定](#14-不安全-tls-設定)
15. [資訊洩漏](#15-資訊洩漏)
16. [SSRF 進階 — DNS Rebinding / UNC 路徑](#16-ssrf-進階--dns-rebinding--unc-路徑)
17. [大量指派 / Over-posting（Mass Assignment）](#17-大量指派--over-postingmass-assignment)
18. [Server-Side Template Injection](#18-server-side-template-injection)
19. [LDAP Injection](#19-ldap-injection)
20. [OS Command Injection](#20-os-command-injection)
21. [ZipSlip / 壓縮檔路徑穿越](#21-zipslip--壓縮檔路徑穿越)
22. [Race Condition / TOCTOU](#22-race-condition--toctou)
23. [JWT 驗證設定錯誤](#23-jwt-驗證設定錯誤)
24. [不安全的 CORS 設定](#24-不安全的-cors-設定)
25. [IDOR / Broken Object Level Authorization](#25-idor--broken-object-level-authorization)
26. [不安全的 XSLT 處理](#26-不安全的-xslt-處理)
27. [ViewState MAC 停用 / machineKey 外洩](#27-viewstate-mac-停用--machinekey-外洩)
28. [不安全的 Cookie 屬性](#28-不安全的-cookie-屬性)
29. [缺少 Rate Limiting / 暴力破解防護](#29-缺少-rate-limiting--暴力破解防護)
30. [不安全的反射與動態組件載入](#30-不安全的反射與動態組件載入)
31. [SignalR / WebSocket 授權缺失](#31-signalr--websocket-授權缺失)
32. [NuGet 供應鏈攻擊與 Typosquatting](#32-nuget-供應鏈攻擊與-typosquatting)
33. [不安全的檔案上傳（CWE-434）](#33-不安全的檔案上傳cwe-434)
34. [Regex ReDoS（正規式拒絕服務）](#34-regex-redos正規式拒絕服務)
35. [CRLF Injection / HTTP Response Splitting](#35-crlf-injection--http-response-splitting)

---

## 1. SQL Injection

### 漏洞模式（grep 關鍵字）
```
"SELECT.*\+.*", "ExecuteQuery\(.*\+", "FromSqlRaw\(.*\+", "SqlCommand.*string"
```

### 易受攻擊範例
```csharp
// ❌ 字串拼接
string sql = "SELECT * FROM Users WHERE Name = '" + username + "'";
var users = db.Database.ExecuteSqlRaw(sql);

// ❌ Dapper 不安全用法
conn.Query("SELECT * FROM Orders WHERE Id = " + id);

// ❌ EF Core raw query
db.Users.FromSqlRaw("SELECT * FROM Users WHERE Email = '" + email + "'");
```

### 修補方式
```csharp
// ✅ 參數化查詢
db.Users.FromSqlRaw("SELECT * FROM Users WHERE Email = {0}", email);

// ✅ Dapper 參數化
conn.Query("SELECT * FROM Orders WHERE Id = @Id", new { Id = id });

// ✅ EF Core LINQ（最佳）
db.Users.Where(u => u.Email == email).ToList();
```

---

## 2. XSS

### 漏洞模式（grep 關鍵字）
```
"Html.Raw\(", "@Html.Raw", "Response.Write\(", "innerHTML\s*=", "HtmlString\("
```

### 易受攻擊範例
```csharp
// ❌ Razor 繞過編碼
@Html.Raw(Model.UserComment)

// ❌ 直接輸出未編碼內容
Response.Write(Request.QueryString["name"]);
```

### 修補方式
```csharp
// ✅ Razor 自動編碼（預設行為）
@Model.UserComment

// ✅ 明確編碼
@Html.Encode(userInput)
// 或
WebUtility.HtmlEncode(userInput)
```

---

## 3. CSRF

### 漏洞模式（grep 關鍵字）
```
"\[HttpPost\]", "ValidateAntiForgeryToken", "AutoValidateAntiforgeryToken"
```

### 易受攻擊範例
```csharp
// ❌ POST 動作缺少 CSRF 保護
[HttpPost]
public IActionResult DeleteUser(int id) { ... }
```

### 修補方式
```csharp
// ✅ Action 層級
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult DeleteUser(int id) { ... }

// ✅ 全域層級（Startup.cs / Program.cs）
builder.Services.AddControllersWithViews(options => {
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
```

---

## 4. 不安全反序列化

### 漏洞模式（grep 關鍵字）
```
"BinaryFormatter", "TypeNameHandling\.All", "TypeNameHandling\.Auto",
"NetDataContractSerializer", "JavaScriptSerializer", "LosFormatter"
```

### 易受攻擊範例
```csharp
// ❌ BinaryFormatter（已於 .NET 5+ 標記 obsolete）
BinaryFormatter bf = new BinaryFormatter();
object obj = bf.Deserialize(stream);

// ❌ JSON.NET 不安全 TypeNameHandling
var settings = new JsonSerializerSettings {
    TypeNameHandling = TypeNameHandling.All
};
JsonConvert.DeserializeObject(json, settings);
```

### 修補方式
```csharp
// ✅ 改用 System.Text.Json（預設安全）
JsonSerializer.Deserialize<MyType>(json);

// ✅ 若必須用 JSON.NET，關閉 TypeNameHandling
var settings = new JsonSerializerSettings {
    TypeNameHandling = TypeNameHandling.None
};
```

---

## 5. Path Traversal

### 漏洞模式（grep 關鍵字）
```
"Path\.Combine.*Request", "File\.ReadAllText.*Request", "File\.Open.*user",
"Directory\.GetFiles.*param", "\.\./"
```

### 易受攻擊範例
```csharp
// ❌ 用戶輸入直接拼接路徑
string filePath = Path.Combine(baseDir, userInput);
return File.ReadAllBytes(filePath);
```

### 修補方式
```csharp
// ✅ 驗證最終路徑在允許目錄內
string safePath = Path.GetFullPath(Path.Combine(baseDir, userInput));
if (!safePath.StartsWith(Path.GetFullPath(baseDir))) {
    throw new UnauthorizedAccessException("Path traversal detected");
}
return File.ReadAllBytes(safePath);
```

---

## 6. SSRF

### 漏洞模式（grep 關鍵字）
```
"HttpClient.*Request\.", "WebRequest\.Create.*param", "new Uri.*user",
"DownloadString.*input", "GetAsync.*Request\["
```

### 易受攻擊範例
```csharp
// ❌ 用戶控制的 URL 直接請求
var response = await httpClient.GetAsync(Request.Query["url"]);
```

### 修補方式
```csharp
// ✅ 白名單驗證
var allowedHosts = new[] { "api.trusted.com", "cdn.example.com" };
var uri = new Uri(userUrl);
if (!allowedHosts.Contains(uri.Host)) throw new Exception("Host not allowed");
var response = await httpClient.GetAsync(uri);
```

---

## 7. XXE

### 漏洞模式（grep 關鍵字）
```
"XmlDocument", "XmlReader", "XDocument\.Load", "XmlTextReader",
"DtdProcessing\.Parse", "new XmlDocument\(\)"
```

### 易受攻擊範例
```csharp
// ❌ 預設允許外部實體
XmlDocument doc = new XmlDocument();
doc.Load(userStream);
```

### 修補方式
```csharp
// ✅ 停用 DTD 處理
XmlReaderSettings settings = new XmlReaderSettings {
    DtdProcessing = DtdProcessing.Prohibit,
    XmlResolver = null
};
XmlReader reader = XmlReader.Create(userStream, settings);
```

---

## 8. 硬編碼憑證

### 漏洞模式（grep 關鍵字）
```
"password\s*=\s*\"", "Password=.*\"", "ApiKey\s*=\s*\"",
"ConnectionString.*password", "secret\s*=\s*\"", "\"Bearer\s+[A-Za-z0-9]"
```

### 易受攻擊範例
```csharp
// ❌
private const string ApiKey = "sk-prod-abc123xyz";
var conn = "Server=prod;Database=App;User=sa;Password=Passw0rd!";
```

### 修補方式
```csharp
// ✅ 環境變數
var apiKey = Environment.GetEnvironmentVariable("API_KEY");

// ✅ ASP.NET Core 設定
var apiKey = configuration["ExternalApi:Key"];

// ✅ Azure Key Vault / AWS Secrets Manager
var secret = await secretClient.GetSecretAsync("my-secret");
```

---

## 9. 弱加密

### 漏洞模式（grep 關鍵字）
```
"MD5\.Create", "SHA1\.Create", "DES\.Create", "RC2\.Create",
"TripleDES.*key.*16", "RijndaelManaged", "ECB", "new RNGCryptoServiceProvider"
```

### 易受攻擊範例
```csharp
// ❌ MD5 / SHA1 用於密碼 hash
using var md5 = MD5.Create();
var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(password));

// ❌ DES 加密
var des = DES.Create();
```

### 修補方式
```csharp
// ✅ 密碼 hash 用 BCrypt 或 ASP.NET Identity
var hash = BCrypt.Net.BCrypt.HashPassword(password);

// ✅ 通用加密用 AES-256 + CBC/GCM
using var aes = Aes.Create();
aes.KeySize = 256;
aes.Mode = CipherMode.CBC;

// ✅ 亂數用 RandomNumberGenerator
var bytes = RandomNumberGenerator.GetBytes(32);
```

---

## 10. 授權缺失

### 漏洞模式（grep 關鍵字）
```
"\[HttpGet\](?!.*\[Authorize)", "\[HttpPost\](?!.*\[Authorize)",
"ControllerBase(?!.*\[Authorize)", "\[AllowAnonymous\]"
```

### 易受攻擊範例
```csharp
// ❌ 敏感 API 缺少授權
[ApiController]
public class AdminController : ControllerBase {
    [HttpDelete]
    public IActionResult DeleteAllUsers() { ... }
}
```

### 修補方式
```csharp
// ✅ Controller 層級
[Authorize(Roles = "Admin")]
[ApiController]
public class AdminController : ControllerBase { ... }

// ✅ 全域（Program.cs）
builder.Services.AddAuthorization(options => {
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
});
```

---

## 11. Open Redirect

### 漏洞模式（grep 關鍵字）
```
"Redirect\(.*Request\.", "Response\.Redirect.*param",
"returnUrl", "redirectUrl", "LocalRedirect"
```

### 易受攻擊範例
```csharp
// ❌ 直接重導向用戶提供的 URL
return Redirect(Request.Query["returnUrl"]);
```

### 修補方式
```csharp
// ✅ 驗證為本地 URL
if (Url.IsLocalUrl(returnUrl))
    return Redirect(returnUrl);
return RedirectToAction("Index", "Home");
```

---

## 12. Log Injection

### 漏洞模式（grep 關鍵字）
```
"_logger\.Log.*Request\.", "logger\.Info.*input",
"Console\.Write.*param", "log\.Debug.*user"
```

### 漏洞說明
用戶輸入若含 `\n`、`\r` 等換行字元，可偽造 log 記錄，誤導監控告警。

### 修補方式
```csharp
// ✅ 清理 log 輸入
var safeInput = userInput.Replace("\n", "\\n").Replace("\r", "\\r");
_logger.LogInformation("User searched: {Query}", safeInput);

// ✅ 結構化 logging（Serilog / NLog）會自動轉義
_logger.LogInformation("User searched: {Query}", userInput);
```

---

## 13. 不安全亂數

### 漏洞模式（grep 關鍵字）
```
"new Random\(\)", "Random\.Next", "Math\.random",
"Guid\.NewGuid.*token", "DateTime\.Now\.Ticks.*token"
```

### 易受攻擊範例
```csharp
// ❌ System.Random 非密碼學安全
var token = new Random().Next(100000, 999999).ToString();
```

### 修補方式
```csharp
// ✅ RandomNumberGenerator（.NET 6+）
var token = RandomNumberGenerator.GetHexString(32);

// ✅ 較舊版本
var bytes = new byte[32];
RandomNumberGenerator.Fill(bytes);
var token = Convert.ToBase64String(bytes);
```

---

## 14. 不安全 TLS 設定

### 漏洞模式（grep 關鍵字）
```
"ServerCertificateValidationCallback.*true",
"SecurityProtocol.*Ssl3", "SecurityProtocol.*Tls\b",
"checkCertificateRevocation.*false", "RemoteCertificateValidationCallback"
```

### 易受攻擊範例
```csharp
// ❌ 停用憑證驗證（常見於開發時遺留到生產）
ServicePointManager.ServerCertificateValidationCallback =
    (sender, cert, chain, errors) => true;
```

### 修補方式
```csharp
// ✅ 使用 HttpClientHandler 正確設定（測試環境用）
var handler = new HttpClientHandler {
    // 僅限開發環境的特定測試
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
// 生產環境：移除此覆寫，依賴系統信任鏈
```

---

## 15. 資訊洩漏

### 漏洞模式（grep 關鍵字）
```
"UseDeveloperExceptionPage\(\)", "app\.UseDeveloperExceptionPage",
"CustomErrors.*mode.*Off", "stack.*trace.*response",
"Exception.*Message.*return", "catch.*ex.*Content\("
```

### 易受攻擊範例
```csharp
// ❌ 在生產環境輸出詳細例外
catch (Exception ex) {
    return BadRequest(ex.ToString()); // 含 stack trace
}
```

### 修補方式
```csharp
// ✅ 生產環境只回傳通用錯誤
catch (Exception ex) {
    _logger.LogError(ex, "Internal error processing request");
    return StatusCode(500, "An internal error occurred.");
}

// ✅ Program.cs 環境判斷
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/Error");
```

---

---

## 16. SSRF 進階 — DNS Rebinding / UNC 路徑

> 延伸第 6 項，補充 .NET 特有的進階繞過路徑。

### 漏洞模式（grep 關鍵字）
```
"HttpClient.*GetAsync", "HttpClient.*SendAsync",
"WebRequest.Create", "WebClient.DownloadString",
"new Uri\(.*Request\.", "169\.254\.169\.254"
```

### 易受攻擊範例
```csharp
// ❌ 無任何白名單，攻擊者可打 metadata endpoint 或 SMB
[HttpGet("/fetch")]
public async Task<IActionResult> Fetch(string url)
{
    using var client = new HttpClient();
    var content = await client.GetStringAsync(url);
    return Content(content);
}
```

### 修補方式
```csharp
// ✅ 白名單 + 解析 DNS 後阻擋內部保留 IP
private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
{
    "api.partner.com", "cdn.trusted.net"
};

public async Task<IActionResult> Fetch(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return BadRequest();
    if (uri.Scheme != Uri.UriSchemeHttps) return BadRequest();
    if (!AllowedHosts.Contains(uri.Host)) return Forbid();

    var addresses = await Dns.GetHostAddressesAsync(uri.Host);
    if (addresses.Any(IsPrivateOrReserved)) return Forbid();

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    return Content(await client.GetStringAsync(uri));
}
```
搭配 `SocketsHttpHandler.ConnectCallback` 在實際連線前再次驗證，防止 DNS rebinding。

---

## 17. 大量指派 / Over-posting（Mass Assignment）

### 漏洞模式（grep 關鍵字）
```
"\[FromBody\].*Entity", "_context\.Update\(model\)",
"dbContext\.Entry.*SetValues", "TryUpdateModelAsync",
"\.Update\(user\)"
```

### 易受攻擊範例
```csharp
// ❌ EF Entity 直接當 DTO 接收，IsAdmin 可被攻擊者覆寫
public class User {
    public int Id { get; set; }
    public string Email { get; set; }
    public bool IsAdmin { get; set; }      // 敏感
    public string TenantId { get; set; }   // 敏感
}

[HttpPut("/users/{id}")]
public async Task<IActionResult> Update(int id, [FromBody] User user)
{
    _db.Users.Update(user);
    await _db.SaveChangesAsync();
    return Ok();
}
```

### 修補方式
```csharp
// ✅ 使用專屬 DTO，只暴露允許欄位
public record UserUpdateDto(string Email);

[HttpPut("/users/{id}")]
public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto)
{
    var entity = await _db.Users.FindAsync(id);
    if (entity == null) return NotFound();
    entity.Email = dto.Email;
    await _db.SaveChangesAsync();
    return Ok();
}
// 或對敏感欄位加 [BindNever] / [JsonIgnore]
```

---

## 18. Server-Side Template Injection

### 漏洞模式（grep 關鍵字）
```
"Razor\.Parse", "Razor\.Compile", "Engine\.Razor",
"RazorLight.*CompileRenderStringAsync",
"Scriban\.Template\.Parse", "DotLiquid\.Template\.Parse"
```

### 易受攻擊範例
```csharp
// ❌ 使用者輸入作為範本內容 → RCE
public string RenderEmail(string userTemplate, object model)
{
    // 攻擊者輸入 @{System.Diagnostics.Process.Start("cmd")} 即可執行命令
    return Engine.Razor.RunCompile(userTemplate, "key", null, model);
}
```

### 修補方式
```csharp
// ✅ 範本由開發者預先定義，使用者只能提供變數值
public string RenderEmail(string templateName, Dictionary<string, object> variables)
{
    var templateContent = LoadTrustedTemplate(templateName); // 從受信任路徑讀取
    return Engine.Razor.RunCompile(templateContent, templateName, null, variables);
}
// 若必須允許自訂範本，使用 sandboxed engine 並禁用 System.*、Assembly.*
```

---

## 19. LDAP Injection

### 漏洞模式（grep 關鍵字）
```
"DirectorySearcher", "DirectoryEntry",
"Filter\s*=", "PrincipalSearcher",
"sAMAccountName=.*\+"
```

### 易受攻擊範例
```csharp
// ❌ 字串拼接 LDAP filter，攻擊者輸入 *)(|(objectClass=*) 可列舉所有物件
public bool UserExists(string username)
{
    using var entry = new DirectoryEntry("LDAP://corp.local");
    using var searcher = new DirectorySearcher(entry)
    {
        Filter = $"(&(objectClass=user)(sAMAccountName={username}))"
    };
    return searcher.FindOne() != null;
}
```

### 修補方式
```csharp
// ✅ 轉義 LDAP 特殊字元
private static string EscapeLdap(string input)
{
    var sb = new StringBuilder();
    foreach (var c in input)
    {
        switch (c)
        {
            case '\\': sb.Append(@"\5c"); break;
            case '*':  sb.Append(@"\2a"); break;
            case '(':  sb.Append(@"\28"); break;
            case ')':  sb.Append(@"\29"); break;
            case '\0': sb.Append(@"\00"); break;
            default:   sb.Append(c); break;
        }
    }
    return sb.ToString();
}

searcher.Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdap(username)}))";
```

---

## 20. OS Command Injection

### 漏洞模式（grep 關鍵字）
```
"Process\.Start", "ProcessStartInfo",
"\.Arguments\s*=.*\+", "cmd\.exe /c",
"powershell\.exe -Command", "UseShellExecute = true"
```

### 易受攻擊範例
```csharp
// ❌ 字串拼接 Arguments，輸入 "a.jpg & calc.exe" 可執行任意命令
public void ConvertImage(string filename)
{
    var psi = new ProcessStartInfo
    {
        FileName = "cmd.exe",
        Arguments = $"/c magick convert {filename} out.png",
        UseShellExecute = false
    };
    Process.Start(psi);
}
```

### 修補方式
```csharp
// ✅ 使用 ArgumentList（.NET 6+），每個參數獨立，不會被 shell 拆解
var psi = new ProcessStartInfo("magick")
{
    UseShellExecute = false,
    RedirectStandardOutput = true,
};
psi.ArgumentList.Add("convert");
psi.ArgumentList.Add(filename);   // 白名單驗證：副檔名、路徑範圍
psi.ArgumentList.Add("out.png");
Process.Start(psi);
```

---

## 21. ZipSlip / 壓縮檔路徑穿越

### 漏洞模式（grep 關鍵字）
```
"ZipFile\.ExtractToDirectory", "ZipArchiveEntry\.ExtractToFile",
"SharpZipLib", "Ionic\.Zip",
"entry\.FullName", "entry\.Name"
```

### 易受攻擊範例
```csharp
// ❌ entry.FullName 可含 ../ 路徑，寫出目標目錄外的檔案
public void Extract(string zipPath, string destDir)
{
    using var archive = ZipFile.OpenRead(zipPath);
    foreach (var entry in archive.Entries)
    {
        var destPath = Path.Combine(destDir, entry.FullName);
        entry.ExtractToFile(destPath, overwrite: true);
    }
}
```

### 修補方式
```csharp
// ✅ 驗證最終路徑在目標目錄內
var destFullDir = Path.GetFullPath(destDir + Path.DirectorySeparatorChar);
foreach (var entry in archive.Entries)
{
    var destPath = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
    if (!destPath.StartsWith(destFullDir, StringComparison.Ordinal))
        throw new SecurityException($"ZipSlip detected: {entry.FullName}");
    entry.ExtractToFile(destPath, overwrite: true);
}
```

---

## 22. Race Condition / TOCTOU

### 漏洞模式（grep 關鍵字）
```
"File\.Exists.*\n.*File\.Open", "Directory\.Exists.*\n.*Directory\.Create",
"if.*CanAccess.*\n.*_db\.", "File\.Exists\(path\)"
```

### 易受攻擊範例
```csharp
// ❌ Check（Exists）與 Use（PhysicalFile）之間有時間差，可被 symlink 替換
public IActionResult Download(string file)
{
    var path = Path.Combine(_uploadDir, file);
    if (!System.IO.File.Exists(path)) return NotFound();
    // 攻擊者此刻把 path 換成 symlink 指向 /etc/passwd
    return PhysicalFile(path, "application/octet-stream");
}
```

### 修補方式
```csharp
// ✅ 一次性開檔，不再重用路徑
try
{
    var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    return File(fs, "application/octet-stream");
}
catch (FileNotFoundException) { return NotFound(); }
// 資料庫操作：用 SELECT FOR UPDATE / IsolationLevel.Serializable 包住授權+操作
```

---

## 23. JWT 驗證設定錯誤

### 漏洞模式（grep 關鍵字）
```
"ValidateIssuer = false", "ValidateAudience = false",
"ValidateLifetime = false", "RequireSignedTokens = false",
"Encoding\.UTF8\.GetBytes.*secret"
```

### 易受攻擊範例
```csharp
// ❌ 關閉所有驗證 + 弱 secret 硬編碼
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = false,
    ValidateAudience = false,
    ValidateLifetime = false,
    IssuerSigningKey = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes("secret123"))  // 太短 + 硬編碼
};
```

### 修補方式
```csharp
// ✅ 啟用所有驗證，key 從 Secret Manager 讀取
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = configuration["Jwt:Issuer"],
    ValidateAudience = true,
    ValidAudience = configuration["Jwt:Audience"],
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    RequireSignedTokens = true,
    RequireExpirationTime = true,
    ClockSkew = TimeSpan.FromMinutes(1),
    IssuerSigningKey = new SymmetricSecurityKey(
        Convert.FromBase64String(configuration["Jwt:SigningKey"])) // ≥ 256-bit
};
// 明確鎖定允許演算法
options.TokenValidationParameters.ValidAlgorithms = new[] { "HS256", "RS256" };
```

---

## 24. 不安全的 CORS 設定

### 漏洞模式（grep 關鍵字）
```
"AllowAnyOrigin", "SetIsOriginAllowed.*=>.*true",
"AllowCredentials", "AddCors"
```

### 易受攻擊範例
```csharp
// ❌ 任意 origin + 允許 cookie → 帳號接管
services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));
```

### 修補方式
```csharp
// ✅ 精確白名單 origin
services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("https://app.mycompany.com",
                  "https://admin.mycompany.com")
     .WithHeaders("Content-Type", "Authorization")
     .WithMethods("GET", "POST")
     .AllowCredentials()
     .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
```

---

## 25. IDOR / Broken Object Level Authorization

### 漏洞模式（grep 關鍵字）
```
"\.Find\(id\)", "\.FindAsync\(id\)",
"\.FirstOrDefault\(x => x\.Id == id\)",
"\[Authorize\].*\n.*FindAsync\(id\)"
```

### 易受攻擊範例
```csharp
// ❌ 只驗「已登入」，不驗「是否有權存取該筆資料」
[Authorize]
[HttpGet("/api/invoices/{id}")]
public async Task<IActionResult> Get(int id)
{
    var invoice = await _db.Invoices.FindAsync(id); // 任何登入者皆可讀任何 invoice
    if (invoice == null) return NotFound();
    return Ok(invoice);
}
```

### 修補方式
```csharp
// ✅ 查詢時加入 tenantId / userId 過濾
[Authorize]
[HttpGet("/api/invoices/{id}")]
public async Task<IActionResult> Get(int id)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var tenantId = User.FindFirstValue("tenant_id");

    var invoice = await _db.Invoices
        .Where(i => i.Id == id && i.TenantId == tenantId && i.OwnerId == userId)
        .FirstOrDefaultAsync();

    if (invoice == null) return NotFound(); // 與「無權存取」回相同 code，避免列舉
    return Ok(invoice);
}
```

---

## 26. 不安全的 XSLT 處理

### 漏洞模式（grep 關鍵字）
```
"XslCompiledTransform", "XsltSettings.*true",
"EnableScript", "EnableDocumentFunction",
"XmlUrlResolver"
```

### 易受攻擊範例
```csharp
// ❌ 開啟 XSLT script 支援 → 可在 XSLT 內執行 C# code（RCE）
var xslt = new XslCompiledTransform();
var settings = new XsltSettings(enableDocumentFunction: true, enableScript: true);
xslt.Load(xsltPath, settings, new XmlUrlResolver());
xslt.Transform(inputXml, outputXml);
```

### 修補方式
```csharp
// ✅ 使用預設安全設定，停用 Script + Document
var xslt = new XslCompiledTransform(enableDebug: false);
xslt.Load(xsltPath, XsltSettings.Default, XmlResolver.ThrowingResolver); // .NET 6+
xslt.Transform(inputXml, outputXml);
// XmlDocument 統一加 doc.XmlResolver = null;
```

---

## 27. ViewState MAC 停用 / machineKey 外洩

### 漏洞模式（grep 關鍵字）
```
"enableViewStateMac.*false", "ViewStateEncryptionMode.*Never",
"<machineKey", "LosFormatter", "ObjectStateFormatter"
```

### 易受攻擊範例
```xml
<!-- ❌ web.config 停用 ViewState MAC + machineKey 硬編碼（可被利用做 RCE） -->
<pages enableViewStateMac="false" viewStateEncryptionMode="Never" />
<machineKey validationKey="AA123..." decryptionKey="BB456..."
            validation="SHA1" decryption="AES" />
```

### 修補方式
- 絕不停用 `EnableViewStateMac`（.NET 4.5.2+ 已不可關）。
- `machineKey` 存 Azure Key Vault / DPAPI，定期輪替；不可出現在原始碼中。
- 掃描 git 歷史是否曾 commit machineKey：`git log -p -- web.config | grep machineKey`
- 盡速從 ASP.NET Web Forms 遷移到 ASP.NET Core。

---

## 28. 不安全的 Cookie 屬性

### 漏洞模式（grep 關鍵字）
```
"Response\.Cookies\.Append", "CookieOptions",
"HttpOnly = false", "Secure = false",
"SameSite.*None", "CookieSecurePolicy"
```

### 易受攻擊範例
```csharp
// ❌ 三個安全旗標全部未設定
Response.Cookies.Append("session_id", token, new CookieOptions
{
    Expires = DateTimeOffset.UtcNow.AddDays(30)
    // HttpOnly=false, Secure=false, SameSite=Unspecified → 預設不安全
});
```

### 修補方式
```csharp
// ✅ 明確設定所有安全屬性
Response.Cookies.Append("session_id", token, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Lax,
    IsEssential = true,
    Expires = DateTimeOffset.UtcNow.AddDays(30)
});

// ✅ 全站強制（Program.cs）
services.Configure<CookiePolicyOptions>(o =>
{
    o.Secure = CookieSecurePolicy.Always;
    o.MinimumSameSitePolicy = SameSiteMode.Lax;
    o.HttpOnly = HttpOnlyPolicy.Always;
});
app.UseCookiePolicy();
```

---

## 29. 缺少 Rate Limiting / 暴力破解防護

### 漏洞模式（grep 關鍵字）
```
"PasswordSignInAsync.*\n(?!.*RateLimit)",
"VerifyOtp", "PasswordReset",
"CheckPasswordSignInAsync",
"AddRateLimiter", "EnableRateLimiting"
```

### 易受攻擊範例
```csharp
// ❌ 無任何 rate limit，攻擊者可每秒數百次暴力破解
[HttpPost("/login")]
public async Task<IActionResult> Login(LoginDto dto)
{
    var result = await _signInManager.PasswordSignInAsync(
        dto.Username, dto.Password, false, false);
    return result.Succeeded ? Ok() : Unauthorized();
}
```

### 修補方式
```csharp
// ✅ .NET 7+ 內建 RateLimiter
services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
app.UseRateLimiter();

[HttpPost("/login"), EnableRateLimiting("login")]
public async Task<IActionResult> Login(LoginDto dto) { /* ... */ }
// 搭配 SignInOptions.MaxFailedAccessAttempts 帳號鎖定
```

---

## 30. 不安全的反射與動態組件載入

### 漏洞模式（grep 關鍵字）
```
"Assembly\.LoadFrom", "Assembly\.LoadFile",
"Type\.GetType\(.*Request\.", "Activator\.CreateInstance",
"AppDomain\.CurrentDomain\.Load"
```

### 易受攻擊範例
```csharp
// ❌ 使用者可控制 DLL 路徑與類型名稱 → RCE
[HttpPost("/plugin/run")]
public IActionResult RunPlugin(string assemblyPath, string typeName)
{
    var asm = Assembly.LoadFrom(assemblyPath);
    var type = asm.GetType(typeName);
    var instance = Activator.CreateInstance(type);
    return Ok(instance.ToString());
}
```

### 修補方式
```csharp
// ✅ 白名單介面，不接受任意 typeName
private static readonly Dictionary<string, Func<IPlugin>> AllowedPlugins = new()
{
    ["csv"]  = () => new CsvExporter(),
    ["xlsx"] = () => new XlsxExporter(),
};

public IActionResult Run(string key)
{
    if (!AllowedPlugins.TryGetValue(key, out var factory)) return NotFound();
    return Ok(factory().Execute());
}
// 若必須動態載入：外掛 DLL 必須存受控目錄、驗 Authenticode 簽章、載入到隔離 AssemblyLoadContext
```

---

## 31. SignalR / WebSocket 授權缺失

### 漏洞模式（grep 關鍵字）
```
": Hub", "Hub<", "Groups\.AddToGroupAsync",
"Clients\.All", "Clients\.Others",
"MapHub<.*>.*\n(?!.*RequireAuthorization)"
```

### 易受攻擊範例
```csharp
// ❌ Hub 缺少 [Authorize]，任何人可加入任意 room
public class ChatHub : Hub
{
    public async Task JoinRoom(string room)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, room); // 無驗證
    }
}
```

### 修補方式
```csharp
// ✅ Hub + method 層級雙重授權
[Authorize]
public class ChatHub : Hub
{
    public async Task JoinRoom(string roomId)
    {
        var userId = Context.UserIdentifier;
        if (!await _roomService.IsMemberAsync(userId, roomId))
            throw new HubException("Forbidden");
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
    }
}

// ✅ MapHub 路由層級
app.MapHub<ChatHub>("/chat").RequireAuthorization();
```

---

## 32. NuGet 供應鏈攻擊與 Typosquatting

### 漏洞模式（grep 關鍵字）
```
"<PackageReference.*Newton\.Json", "<PackageReference.*Serrilog",
"packages\.config", "install\.ps1", "init\.ps1",
"\.targets", "\.props"
```

### 易受攻擊範例
```xml
<!-- ❌ Typosquatting：拼字接近主流套件但來自惡意作者 -->
<PackageReference Include="Newton.Json" Version="13.0.1" />
<PackageReference Include="Serrilog" Version="3.0.0" />
```

### 修補方式
```xml
<!-- ✅ NuGet.config — 限制套件 prefix 只來自受信任 feed -->
<packageSourceMapping>
  <packageSource key="nuget.org">
    <package pattern="Microsoft.*" />
    <package pattern="System.*" />
    <package pattern="Newtonsoft.Json" />
  </packageSource>
  <packageSource key="internal">
    <package pattern="MyCompany.*" />
  </packageSource>
</packageSourceMapping>
```
```xml
<!-- ✅ 啟用簽章驗證 -->
<config>
  <add key="signatureValidationMode" value="require" />
</config>
```
CI 流程加入：`dotnet list package --vulnerable --include-transitive`，並整合 Dependabot / Snyk 掃描。

---

---

## 33. 不安全的檔案上傳（CWE-434）

### 漏洞模式（grep 關鍵字）
```
"IFormFile", "Request\.Form\.Files",
"\.FileName", "\.ContentType",
"Path\.GetExtension.*fileName", "SaveAs\("
```

### 易受攻擊範例
```csharp
// ❌ 只靠副檔名判斷類型、未驗 MIME、未隨機化檔名、存在 Web 根目錄
[HttpPost("/upload")]
public async Task<IActionResult> Upload(IFormFile file)
{
    var ext = Path.GetExtension(file.FileName); // 可被偽造
    if (ext != ".jpg" && ext != ".png") return BadRequest();

    var savePath = Path.Combine("wwwroot/uploads", file.FileName); // 可含路徑穿越
    using var stream = System.IO.File.Create(savePath);
    await file.CopyToAsync(stream);
    return Ok(savePath);
}
```
攻擊面：上傳 `shell.aspx`（副檔名雙重延伸 `shell.aspx.jpg`）、路徑穿越覆蓋系統檔、Content-Type 偽造。

### 修補方式
```csharp
private static readonly HashSet<string> AllowedExtensions =
    new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };

private static readonly Dictionary<string, byte[]> MagicBytes = new()
{
    { ".jpg",  new byte[] { 0xFF, 0xD8, 0xFF } },
    { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
    { ".png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
    { ".pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46 } },
};

[HttpPost("/upload")]
public async Task<IActionResult> Upload(IFormFile file)
{
    // 1. 大小限制
    if (file.Length > 10 * 1024 * 1024) return BadRequest("File too large");

    // 2. 副檔名白名單（取最後一個，防雙重延伸）
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!AllowedExtensions.Contains(ext)) return BadRequest("Extension not allowed");

    // 3. Magic bytes 驗證（真實內容類型）
    var header = new byte[8];
    await file.OpenReadStream().ReadAsync(header);
    if (!MagicBytes[ext].SequenceEqual(header.Take(MagicBytes[ext].Length)))
        return BadRequest("Content mismatch");

    // 4. 隨機化儲存名稱，存到非 Web 根目錄
    var storedName = $"{Guid.NewGuid():N}{ext}";
    var savePath = Path.Combine(_storageRoot, storedName); // _storageRoot 在 wwwroot 外
    using var stream = System.IO.File.Create(savePath);
    file.OpenReadStream().Seek(0, SeekOrigin.Begin);
    await file.CopyToAsync(stream);

    return Ok(storedName);
}
```

---

## 34. Regex ReDoS（正規式拒絕服務）

### 漏洞模式（grep 關鍵字）
```
"new Regex\(", "Regex\.Match\(", "Regex\.IsMatch\(",
"\(\.\*\)\+", "\(\.\+\)\+", "backtrack",
"RegexOptions\.None"（高複雜度 pattern 需注意）
```

### 易受攻擊範例
```csharp
// ❌ 指數級回溯：(a+)+ 類型 pattern 搭配惡意輸入
[HttpGet("/validate")]
public IActionResult Validate(string email)
{
    // 輸入 "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!" → CPU 100% 數秒
    var regex = new Regex(@"^([a-zA-Z0-9]+\.)*[a-zA-Z0-9]+@[a-zA-Z0-9]+\.[a-zA-Z]{2,}$");
    return regex.IsMatch(email) ? Ok() : BadRequest();
}
```

### 修補方式
```csharp
// ✅ 1. 設定 Timeout，讓 ReDoS 最多只佔用有限時間
var regex = new Regex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
    RegexOptions.None,
    matchTimeout: TimeSpan.FromMilliseconds(100)); // 超時拋 RegexMatchTimeoutException

try { return regex.IsMatch(email) ? Ok() : BadRequest(); }
catch (RegexMatchTimeoutException) { return StatusCode(408); }

// ✅ 2. .NET 7+ 改用 Regex source generator（編譯期分析，不允許高回溯 pattern）
[GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$",
    RegexOptions.None, matchTimeoutMilliseconds: 100)]
private static partial Regex EmailRegex();

// ✅ 3. 輸入長度限制（先擋，再跑 regex）
if (email.Length > 254) return BadRequest();
```
常見危險 pattern：`(a+)+`、`([a-z]+)*`、`(a|aa)+`，使用 [REXSA](https://github.com/nicowillis/rexsa) 或 Semgrep `p/redos` 規則預先偵測。

---

## 35. CRLF Injection / HTTP Response Splitting

### 漏洞模式（grep 關鍵字）
```
"Response\.Headers\[.*\]\s*=.*Request\.",
"Response\.Headers\.Append.*param",
"HttpContext\.Response\.Headers.*user",
"Set-Cookie.*\+.*input", "Location.*\+.*Request\."
```

### 易受攻擊範例
```csharp
// ❌ 將用戶輸入直接寫入 HTTP header，攻擊者可注入 \r\n 分割回應
[HttpGet("/redirect")]
public IActionResult Redirect(string lang)
{
    // 輸入 "en\r\nSet-Cookie: admin=true" → 插入假 header
    Response.Headers["X-Lang"] = lang;
    return Ok();
}
```

### 修補方式
```csharp
// ✅ 移除 CR / LF 字元，ASP.NET Core 5+ 自動阻擋但仍建議明確清理
private static string SanitizeHeaderValue(string value)
    => value.Replace("\r", "").Replace("\n", "");

Response.Headers["X-Lang"] = SanitizeHeaderValue(lang);

// ✅ 如果是重導向，用 Url.IsLocalUrl + LocalRedirect
if (!Url.IsLocalUrl(returnUrl)) return BadRequest();
return LocalRedirect(returnUrl);

// ✅ ASP.NET Core 5+ 對以下 header 會自動驗證並拋 ArgumentException：
//    - Response.Redirect()
//    - Response.Headers["Location"]
// 但自訂 header（X-*）不在自動保護範圍，仍需手動清理
```

---

## Semgrep 快速掃描指令（C# 規則集）

```bash
# 安裝 Semgrep
pip install semgrep

# 使用 OWASP C# 規則集掃描
semgrep --config "p/csharp" --config "p/owasp-top-ten" \
        --output results.json --json ./src

# 使用 Semgrep Registry 的 C# 安全規則
semgrep --config "p/default" --lang csharp ./src
```
