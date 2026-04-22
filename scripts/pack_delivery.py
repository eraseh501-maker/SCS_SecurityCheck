"""
pack_delivery.py — 產生無 Docker SAST 掃描工具包
輸出: 桌面 SCS_SecurityCheck_nodocker_YYYYMMDD.zip
守則: PowerShell 5.1 含中文需 UTF-8 BOM
"""
import zipfile
from pathlib import Path
from datetime import datetime

DESKTOP = Path("C:/Users/ChenVincent/Desktop")
TIMESTAMP = datetime.now().strftime("%Y%m%d")
OUT_ZIP = DESKTOP / f"SCS_SecurityCheck_nodocker_{TIMESTAMP}.zip"

BOM = b"\xef\xbb\xbf"

# ─────────────────────────────────────────────────────────────────────
# scan.ps1
#   修正重點 (v4):
#   1. 每筆 SAST finding 附繁中「建議修復方式」(內建 SCS/CA 規則知識庫)
#   2. HTML [1] Security Findings 改三欄 (Finding / Rule / 建議修復方式)
#   3. TXT 報告每筆 finding 後加縮排「修復建議」行；TXT 改為 UTF-8 with BOM
#   (v3) 原專案完全 READ-ONLY：robocopy 到 %TEMP%\scs-scan-<timestamp> 後對副本掃描
#   (v3) 掃完自動刪除整個 temp 目錄，不留殘留
#   (v2) 強制 dotnet 英文 UI + console UTF-8
#   (v2) 過濾器只抓 SAST 規則 (SCS/CA/SA/SEC####)，MSB/NETSDK 獨立區塊
# ─────────────────────────────────────────────────────────────────────
SCAN_PS1 = r"""
param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$OutputDir = ".\sast-output",
    [switch]$IncludeBuildIssues
)

$ErrorActionPreference = "Continue"

# ── Encoding fix: force UTF-8 and English dotnet output ──────────────
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
    chcp 65001 | Out-Null
} catch { }
$env:DOTNET_CLI_UI_LANGUAGE = "en"
$env:VSLANG = "1033"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

function Write-Banner {
    Write-Host ""
    Write-Host ("=" * 62) -ForegroundColor Cyan
    Write-Host "  .NET SAST Scanner -- Local Version (No Docker) v4" -ForegroundColor Cyan
    Write-Host "  Works with VS2019 / VS2022 / VS Code / any SDK" -ForegroundColor Cyan
    Write-Host ("=" * 62) -ForegroundColor Cyan
    Write-Host ""
}
function Write-Step([string]$m) { Write-Host "`n> $m" -ForegroundColor Cyan }
function Write-OK([string]$m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Write-Warn([string]$m) { Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Write-Fail([string]$m) { Write-Host "  [FAIL] $m" -ForegroundColor Red }

# ── Rule advice knowledge base (rule id -> 建議修復方式, 繁體中文) ──────
$RuleAdvice = @{
    # SecurityCodeScan (SCS)
    "SCS0001" = "命令注入：勿直接拼接使用者輸入為命令列。改用 ProcessStartInfo + ArgumentList 逐一加入參數，或使用允許清單驗證輸入。"
    "SCS0002" = "SQL 注入 (LINQ)：避免動態拼接字串，改用 EF Core 參數化查詢或 FromSqlInterpolated。"
    "SCS0003" = "XPath 注入：改用 XPathExpression.Compile 搭配 XsltArgumentList 或 XPathNavigator.Evaluate 參數化，不要字串拼接。"
    "SCS0004" = "憑證驗證被關閉：勿讓 ServerCertificateCustomValidationCallback 回傳 true，改用預設驗證或明確 pinning 白名單。"
    "SCS0005" = "弱亂數：System.Random 不適合安全用途。改用 System.Security.Cryptography.RandomNumberGenerator.Create()。"
    "SCS0006" = "弱雜湊：MD5 / SHA1 已破解。密碼請用 Rfc2898DeriveBytes (PBKDF2, >=100k iterations) 或 Argon2；一般雜湊用 SHA-256/384/512。"
    "SCS0007" = "XXE 風險：設定 XmlReaderSettings.DtdProcessing = DtdProcessing.Prohibit 與 XmlResolver = null。"
    "SCS0008" = "Cookie 未強制 SSL：設定 cookie.Secure = true 或在 CookieOptions 指定 Secure = true。"
    "SCS0009" = "Cookie 未設 HttpOnly：設定 cookie.HttpOnly = true，避免被 JavaScript 讀取。"
    "SCS0010" = "弱加密演算法：勿使用 DES / 3DES / RC2 / RC4。改用 AES-GCM 或 AES-CBC + HMAC。"
    "SCS0011" = "CBC 模式未驗證：CBC 必須搭配 HMAC 驗證完整性，或改用 AES-GCM 等 AEAD 模式。"
    "SCS0012" = "ECB 模式：ECB 會洩漏明文模式，改用 CBC / CTR / GCM。"
    "SCS0013" = "弱加密演算法：勿使用 RC2 / RC4 / DES，改 AES-256-GCM。"
    "SCS0014" = "SQL 注入 (Raw SQL)：使用 SqlCommand.Parameters.AddWithValue 或 EF Core FromSqlInterpolated 參數化查詢。"
    "SCS0015" = "硬編碼密碼 / 金鑰：將機敏資訊移到環境變數、appsettings.{env}.json、User Secrets 或 Azure Key Vault / AWS Secrets Manager。"
    "SCS0016" = "CSRF 風險：在 controller 加 [ValidateAntiForgeryToken]，或在 ASP.NET Core 全域啟用 AddAntiforgery + AutoValidateAntiforgeryTokenAttribute。"
    "SCS0017" = "Request Validation 被關閉：不要設定 validateRequest=false；用 HTML encoder / Razor 自動 encoding 防 XSS。"
    "SCS0018" = "路徑穿越 (Path Traversal)：用 Path.GetFullPath 後驗證結果落在允許的根目錄之下，不要直接把使用者輸入拼入路徑。"
    "SCS0019" = "輸出未編碼：對 HTML 輸出使用 HtmlEncoder.Default；Razor 的 @ 已自動 encode，勿用 @Html.Raw 輸出使用者資料。"
    "SCS0020" = "SQL 注入 (OleDb)：使用 OleDbCommand.Parameters 參數化，不要拼接字串。"
    "SCS0021" = "CSRF Cookie 不安全：啟用 Secure + SameSite=Lax/Strict，並設定 AntiForgery.Cookie 屬性。"
    "SCS0022" = "Cookie 未強制 SSL：同 SCS0008。"
    "SCS0023" = "ViewState 未加密：在 Web.config 設定 <pages viewStateEncryptionMode='Always'>。"
    "SCS0024" = "Controller 缺 [ValidateAntiForgeryToken]：為所有寫入類型 action 加 attribute，或使用全域 filter。"
    "SCS0025" = "LDAP 注入：對 DN/filter 特殊字元做 escape，或使用 Novell.Directory.Ldap.SafeString。"
    "SCS0026" = "LDAP 注入：同 SCS0025。"
    "SCS0027" = "Open Redirect：驗證跳轉 URL 為本站白名單或相對路徑，勿直接跳轉使用者傳入的 URL。"
    "SCS0028" = "不安全反序列化：勿用 BinaryFormatter / SoapFormatter / NetDataContractSerializer / LosFormatter。改用 System.Text.Json 或設定 KnownTypes 限制的 DataContractSerializer。"
    "SCS0029" = "XSS (字串串接)：使用 Razor 自動編碼 @variable 或 HtmlEncoder.Default.Encode；勿 Html.Raw 輸出未驗證資料。"
    "SCS0030" = "Request Validation 被關閉：同 SCS0017。"
    "SCS0031" = "Open Redirect：同 SCS0027。"
    "SCS0032" = "密碼政策不足：最小長度建議 >= 8 至 12 字元，並要求混合字元類別。"
    "SCS0033" = "密碼政策缺『需含數字』。"
    "SCS0034" = "密碼政策缺『需含非字母字元』。"
    "SCS0035" = "密碼政策缺『需含大寫字母』。"

    # Microsoft.CodeAnalysis.NetAnalyzers (CA) — Security
    "CA2100" = "SQL 注入：改用參數化查詢 (SqlCommand.Parameters.Add / Dapper / EF Core)。"
    "CA3001" = "SQL 注入 (Review)：檢查此 SQL 組成邏輯，移除字串拼接改為參數化。"
    "CA3003" = "路徑注入：驗證檔案路徑是否在允許的根目錄 (Path.GetFullPath 比對)。"
    "CA3006" = "Process 命令注入：勿拼接使用者輸入到 ProcessStartInfo.Arguments，改用 ArgumentList。"
    "CA3075" = "不安全的 XML 處理：設定 DtdProcessing = Prohibit、XmlResolver = null。"
    "CA3076" = "不安全的 DTD 處理：停用 DTD (DtdProcessing = Prohibit)。"
    "CA3077" = "XmlDocument 不安全：設定 XmlResolver = null 或改用 XmlReader。"
    "CA3147" = "缺 [ValidateAntiForgeryToken]：為狀態變更 action 加 token 驗證。"
    "CA5350" = "弱加密 (3DES/MD5/SHA1)：改用 AES-256-GCM 與 SHA-256+。"
    "CA5351" = "已破解加密 (DES/MD5/RC2)：停止使用，改 AES/SHA-256。"
    "CA5358" = "不安全 Cipher 模式 (ECB/OFB/CFB)：改用 CBC + HMAC 或 GCM。"
    "CA5359" = "憑證驗證被關閉：移除自訂 validator 或改做嚴格驗證。"
    "CA5360" = "反序列化呼叫危險方法：限制可反序列化型別或換安全序列化格式。"
    "CA5361" = "停用 SChannel 強加密：移除 AppContext.SetSwitch('Switch.System.Net.DontEnableSchUseStrongCrypto')。"
    "CA5362" = "反序列化物件圖有循環參考：檢查序列化模型是否包含循環參考。"
    "CA5363" = "Request Validation 被停用：重新啟用；改用 HTML Encoder 處理輸出。"
    "CA5364" = "使用過時 TLS (SSL3/TLS1.0/TLS1.1)：改強制 TLS 1.2 或 1.3。"
    "CA5365" = "停用 HTTP header check：移除 UseUnsafeHeaderParsing 設定。"
    "CA5366" = "DataSet.ReadXml 應使用 XmlReader 以避免 XXE。"
    "CA5367" = "含 pointer 欄位的型別不應被序列化。"
    "CA5368" = "Page 子類別應設定 ViewStateUserKey 以防 CSRF。"
    "CA5369" = "XmlSerializer.Deserialize 應搭配 XmlReader。"
    "CA5370" = "XmlValidatingReader 應使用 XmlReader。"
    "CA5371" = "XmlSchema.Read 應使用 XmlReader。"
    "CA5372" = "XPathDocument 應使用 XmlReader 構造。"
    "CA5373" = "勿用過時金鑰衍生函式 (PasswordDeriveBytes)：改用 Rfc2898DeriveBytes (PBKDF2) 並 >= 100000 iterations。"
    "CA5374" = "勿用 XslTransform，改用 XslCompiledTransform。"
    "CA5375" = "勿使用帳戶等級 SAS，改用資源層級 SAS。"
    "CA5376" = "SAS 應指定 HttpsOnly protocol。"
    "CA5377" = "使用 container 層級存取政策，勿直接給 Blob 層級 SAS。"
    "CA5378" = "勿關閉 ServicePointManager 強加密設定。"
    "CA5379" = "KDF 演算法強度不足，改 PBKDF2 (SHA-256+) 或 Argon2。"
    "CA5380" = "勿新增憑證到根憑證庫。"
    "CA5381" = "確認憑證不會被加入根憑證庫。"
    "CA5382" = "ASP.NET Core Cookie 須設 Secure=true。"
    "CA5383" = "確認 ASP.NET Core Cookie 使用安全屬性。"
    "CA5384" = "勿使用 DSA 簽章演算法，改 RSA (>= 2048) 或 ECDSA。"
    "CA5385" = "RSA 金鑰長度應 >= 2048 bit，建議 3072+。"
    "CA5386" = "勿硬編碼 SecurityProtocolType，使用系統預設 (SystemDefault)。"
    "CA5387" = "PBKDF2 iterations 不足，建議 >= 100000。"
    "CA5388" = "確保 KDF 迭代次數足夠 (>= 100000)。"
    "CA5389" = "Zip Slip 風險：解壓縮前驗證 entry 路徑位於目標資料夾內 (Path.GetFullPath 比對)。"
    "CA5390" = "勿硬編碼加密金鑰：金鑰應從 Key Vault / DPAPI / 環境變數取得。"
    "CA5391" = "ASP.NET Core MVC 啟用 antiforgery token (AddAntiforgery + AutoValidateAntiforgeryTokenAttribute)。"
    "CA5392" = "P/Invoke 應使用 DefaultDllImportSearchPaths attribute。"
    "CA5393" = "DllImportSearchPath 值不安全，改用 System32 或 SafeDirectories。"
    "CA5394" = "不安全亂數：加密用途改用 RandomNumberGenerator，而非 System.Random。"
    "CA5395" = "Action method 缺 HttpVerb attribute (HttpGet/HttpPost 等)。"
    "CA5396" = "HttpCookie 設定 HttpOnly = true。"
    "CA5397" = "勿使用過時 SslProtocols (Ssl2/Ssl3/Tls/Tls11)。"
    "CA5398" = "勿硬編碼 SslProtocols，使用 SslProtocols.None 交由系統決定。"
    "CA5399" = "HttpClient ClientCertificateOptions 若設 Manual，必須手動提供憑證；請確認設計是否正確。"
    "CA5400" = "HttpClient 必須啟用憑證撤銷檢查 (CheckCertificateRevocationList = true)。"
}

function Get-RuleAdvice([string]$line) {
    if ($line -match "\b(SCS\d{3,4}|CA\d{4}|SA\d{4}|SEC\d{4})\b") {
        $ruleId = $matches[1]
        if ($RuleAdvice.ContainsKey($ruleId)) {
            return @{ RuleId = $ruleId; Advice = $RuleAdvice[$ruleId] }
        }
        return @{ RuleId = $ruleId; Advice = "(尚未收錄此規則的修復建議；請搜尋官方文件：$ruleId)" }
    }
    return @{ RuleId = ""; Advice = "" }
}

Write-Banner

# 1. .NET SDK check
Write-Step "Checking .NET SDK..."
$dotnetVer = & dotnet --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail ".NET SDK not found. Install from: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
}
Write-OK ".NET SDK $dotnetVer"

# 2. Project path check
Write-Step "Validating project path..."
$ProjectPath = $ProjectPath.TrimEnd('\').TrimEnd('/')
if (-not (Test-Path $ProjectPath)) {
    Write-Fail "Path not found: $ProjectPath"
    exit 1
}

$slnFiles  = Get-ChildItem -Path $ProjectPath -Filter "*.sln"   -Recurse -Depth 3 -ErrorAction SilentlyContinue
$projFiles = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -Recurse -Depth 5 -ErrorAction SilentlyContinue

if ($slnFiles.Count -gt 0) {
    $targetFile = $slnFiles[0].FullName
    Write-OK "Solution: $($slnFiles[0].Name)"
} elseif ($projFiles.Count -gt 0) {
    $targetFile = $projFiles[0].FullName
    Write-OK "Project: $($projFiles[0].Name)"
} else {
    Write-Fail "No .sln or .csproj found under: $ProjectPath"
    exit 1
}
$targetDir = Split-Path -Parent $targetFile

# 3. Output directory
$OutPath = Join-Path $ScriptDir $OutputDir
if (-not (Test-Path $OutPath)) { New-Item -ItemType Directory -Path $OutPath | Out-Null }
Write-OK "Reports will be saved to: $OutPath"

# 4. Copy project to isolated temp workspace (original project stays READ-ONLY)
Write-Step "Copying project to isolated temp workspace (original will NOT be modified)..."
$AbsProjectPath = (Resolve-Path $ProjectPath).Path
$TempRoot = Join-Path $env:TEMP "scs-scan-$Timestamp"
if (Test-Path $TempRoot) { Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

# robocopy: exclude build output and VCS metadata so copy is fast and clean
$rcArgs = @(
    $AbsProjectPath, $TempRoot,
    "/E", "/NFL", "/NDL", "/NJH", "/NJS", "/NC", "/NS", "/NP",
    "/XD", "bin", "obj", ".vs", ".git", "node_modules", "packages", "TestResults",
    "/XF", "*.suo", "*.user"
)
& robocopy @rcArgs | Out-Null
# robocopy exit codes 0-7 are success; 8+ are real failures
if ($LASTEXITCODE -ge 8) {
    Write-Fail "Failed to copy project to temp workspace (robocopy exit=$LASTEXITCODE)"
    exit 1
}
Write-OK "Project copied to: $TempRoot"

# Remap the scan target to the copy. From here on, $ScanTarget / $ScanDir
# point to the temp copy; the original project is never touched again.
$relative   = $targetFile.Substring($AbsProjectPath.Length).TrimStart('\','/')
$ScanTarget = Join-Path $TempRoot $relative
$ScanDir    = Split-Path -Parent $ScanTarget

# 5. Inject analyzers via Directory.Build.props (ON THE COPY ONLY)
Write-Step "Injecting Roslyn security analyzers into the temp copy..."
$PropsFile = Join-Path $ScanDir "Directory.Build.props"

$PropsContent = @"
<Project>
  <ItemGroup>
    <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <PropertyGroup>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningLevel>5</WarningLevel>
    <RunAnalyzersDuringBuild>true</RunAnalyzersDuringBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  </PropertyGroup>
</Project>
"@

[System.IO.File]::WriteAllText($PropsFile, $PropsContent, [System.Text.Encoding]::UTF8)
Write-OK "Analyzers injected into copy (original project's Directory.Build.props untouched)"

# 6. Restore (on the COPY)
Write-Step "Restoring NuGet packages on temp copy (first run needs internet)..."
$null = & dotnet restore "$ScanTarget" 2>&1
Write-OK "Restore done"

# 7. Build with analyzers (on the COPY)
Write-Step "Running static analysis (1-5 min)..."
$buildLog = & dotnet build "$ScanTarget" --no-incremental `
    "-p:TreatWarningsAsErrors=false" `
    "-p:RunAnalyzersDuringBuild=true" `
    "-p:EnableNETAnalyzers=true" `
    "-p:AnalysisMode=AllEnabledByDefault" 2>&1
$buildExitCode = $LASTEXITCODE

# ── Classify results ──────────────────────────────────────────────
# SAST findings: only real analyzer rules (SCS####, CAxxxx, SAxxxx)
$sastLines = $buildLog | Where-Object {
    $_ -match "\b(SCS\d{3,4}|CA\d{4}|SA\d{4}|SEC\d{4})\b"
}

# Build environment issues (MSB, NETSDK, NU — not security)
$buildIssues = $buildLog | Where-Object {
    $_ -match "\b(MSB\d{3,5}|NETSDK\d+|NU\d{4})\b"
}

# Overall status
$sastCount  = @($sastLines).Count
$buildCount = @($buildIssues).Count
Write-OK "Static analysis done (exit: $buildExitCode)"
Write-OK "SAST findings: $sastCount  |  Build env warnings: $buildCount"

if ($sastCount -eq 0) {
    Write-Warn "No SAST findings detected. Possible reasons:"
    Write-Warn "  (a) The project is clean (congrats!)"
    Write-Warn "  (b) Analyzers did not run — check build log for 'analyzer' errors"
    Write-Warn "  (c) Project targets .NET Framework and uses packages.config (need Directory.Build.props merged differently)"
}

# 8. NuGet vulnerability scan (on the COPY)
Write-Step "Scanning NuGet vulnerabilities..."
$vulnLog    = & dotnet list "$ScanTarget" package --vulnerable --include-transitive 2>&1
$vulnIssues = $vulnLog | Where-Object {
    $_ -match "Severity|has the following vulnerable" -or $_ -match ">\s+\S+\s+\d"
}
Write-OK "NuGet scan done"

# 9. Cleanup: remove the entire temp copy. Original project was never modified.
Write-Step "Cleaning up temp workspace..."
Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $TempRoot) {
    Write-Warn "Temp workspace partially remained: $TempRoot (safe to delete manually)"
} else {
    Write-OK "Temp workspace removed: $TempRoot"
}
Write-OK "Original project at $AbsProjectPath was NOT modified by this scan."

# 9. Generate reports
Write-Step "Generating reports..."

$ReportTxt  = Join-Path $OutPath "sast-report-$Timestamp.txt"
$ReportHtml = Join-Path $OutPath "sast-report-$Timestamp.html"

# --- TXT report ---
$txtLines = @(
    ".NET SAST Scan Report",
    "======================",
    "Date     : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "Target   : $ProjectPath",
    "SDK      : $dotnetVer",
    "Analyzer : SecurityCodeScan.VS2019 5.6.7 + NetAnalyzers 9.0.0",
    "Note     : 'VS2019' is just the package brand name; compatible with all VS versions.",
    "",
    "================================================================",
    " [1] SECURITY FINDINGS (SCS / CA / SA rules)",
    "================================================================"
)
if ($sastCount -gt 0) {
    foreach ($line in $sastLines) {
        $txtLines += $line
        $info = Get-RuleAdvice $line
        if ($info.Advice) {
            $txtLines += "    >> 修復建議 [$($info.RuleId)]: $($info.Advice)"
            $txtLines += ""
        }
    }
} else {
    $txtLines += "  No SAST findings.  (Project clean, or analyzers did not run.)"
}

if ($IncludeBuildIssues -or $buildCount -gt 0) {
    $txtLines += @("", "================================================================",
                       " [2] BUILD ENVIRONMENT WARNINGS (MSB / NETSDK / NU — NOT security)",
                       "     These are build/dependency issues, not vulnerabilities.",
                       "================================================================")
    if ($buildCount -gt 0) { $txtLines += $buildIssues } else { $txtLines += "  None." }
}

$txtLines += @("", "================================================================",
                   " [3] NUGET PACKAGE VULNERABILITIES",
                   "================================================================")
if (@($vulnIssues).Count -gt 0) { $txtLines += $vulnIssues } else { $txtLines += "  No known vulnerable packages." }

[System.IO.File]::WriteAllLines($ReportTxt, $txtLines, [System.Text.UTF8Encoding]::new($true))

# --- HTML helpers ---
function HtmlEscape([string]$s) {
    return ($s -replace "&","&amp;" -replace "<","&lt;" -replace ">","&gt;")
}

function Build-TableRows([string[]]$lines, [string]$emptyMsg) {
    if (-not $lines -or @($lines).Count -eq 0) {
        return "<tr class='ok'><td>$emptyMsg</td></tr>"
    }
    $out = ""
    foreach ($ln in $lines) {
        $esc = HtmlEscape $ln
        $cls = if ($ln -match ": error |Critical|High") { "err" }
               elseif ($ln -match ": warning |Moderate|Medium") { "warn" }
               else { "info" }
        $out += "<tr class='$cls'><td>$esc</td></tr>`n"
    }
    return $out
}

function Build-SastRows([string[]]$lines, [string]$emptyMsg) {
    if (-not $lines -or @($lines).Count -eq 0) {
        return "<tr class='ok'><td colspan='3'>$emptyMsg</td></tr>"
    }
    $out = ""
    foreach ($ln in $lines) {
        $info = Get-RuleAdvice $ln
        $esc  = HtmlEscape $ln
        $rule = HtmlEscape $info.RuleId
        $fix  = HtmlEscape $info.Advice
        $cls  = if ($ln -match ": error |Critical|High") { "err" }
                elseif ($ln -match ": warning |Moderate|Medium") { "warn" }
                else { "info" }
        $out += "<tr class='$cls'><td class='finding'>$esc</td><td class='rule'>$rule</td><td class='fix'>$fix</td></tr>`n"
    }
    return $out
}

$sastRows  = Build-SastRows  $sastLines   "No SAST findings (project clean, or analyzers not triggered)"
$buildRows = Build-TableRows $buildIssues "No build environment warnings"
$vulnRows  = Build-TableRows $vulnIssues  "No known vulnerable NuGet packages"

$badge = if ($sastCount -eq 0 -and $buildExitCode -eq 0) { "<span class='ok-badge'>Clean</span>" }
         elseif ($sastCount -gt 0) { "<span class='warn-badge'>$sastCount SAST findings</span>" }
         else { "<span class='warn-badge'>Build Warnings</span>" }

$html = @"
<!DOCTYPE html><html lang="en">
<head><meta charset="UTF-8">
<title>.NET SAST Report $Timestamp</title>
<style>
 body{font-family:'Segoe UI',sans-serif;margin:24px;background:#f4f6f9;color:#222}
 h1{color:#1a5276;border-bottom:3px solid #1a5276;padding-bottom:8px}
 h2{color:#1f618d;margin-top:28px}
 h2.buildsec{color:#7d6608}
 .meta{background:#d6eaf8;padding:10px 16px;border-radius:6px;margin-bottom:20px;line-height:1.9}
 .note{background:#fffce6;padding:8px 14px;border-left:4px solid #f1c40f;margin:10px 0;font-size:13px}
 table{width:100%;border-collapse:collapse;background:#fff;box-shadow:0 1px 4px rgba(0,0,0,.1);margin-bottom:20px}
 th{background:#1a5276;color:#fff;padding:10px;text-align:left}
 td{padding:7px 12px;border-bottom:1px solid #eee;font-family:Consolas,monospace;font-size:13px;word-break:break-all;vertical-align:top}
 td.rule{font-weight:bold;text-align:center;white-space:nowrap}
 td.fix{font-family:'Microsoft JhengHei','Segoe UI',sans-serif;font-size:13px;color:#1a3a5f;line-height:1.55;word-break:normal;white-space:normal}
 tr.err  td{background:#fdecea;color:#c0392b}
 tr.err  td.fix{background:#fdecea;color:#7b241c}
 tr.warn td{background:#fef9e7;color:#7d6608}
 tr.warn td.fix{background:#fef9e7;color:#6e4b1a}
 tr.info td{background:#f8f9fa;color:#555}
 tr.info td.fix{background:#f8f9fa;color:#1a3a5f}
 tr.ok   td{background:#eafaf1;color:#1e8449}
 .ok-badge{background:#27ae60;color:#fff;padding:2px 9px;border-radius:10px;font-size:12px}
 .warn-badge{background:#e67e22;color:#fff;padding:2px 9px;border-radius:10px;font-size:12px}
</style></head>
<body>
<h1>.NET SAST Security Scan Report</h1>
<div class="meta">
  <b>Date:</b> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')<br>
  <b>Target:</b> $ProjectPath<br>
  <b>SDK:</b> $dotnetVer<br>
  <b>Analyzer:</b> SecurityCodeScan 5.6.7 + NetAnalyzers 9.0.0<br>
  <b>Status:</b> $badge
</div>
<div class="note">
  <b>Note:</b> "SecurityCodeScan.VS2019" is a package brand name — it works with <b>all</b> Visual Studio versions
  (VS2019 / VS2022) and pure <code>dotnet build</code>. This scan's result is independent of which VS you use.
</div>

<h2>[1] Security Findings (SCS / CA / SA rules)</h2>
<table>
  <tr><th style='width:55%'>Finding (file / line / rule / message)</th><th style='width:8%'>Rule</th><th style='width:37%'>建議修復方式</th></tr>
$sastRows
</table>

<h2 class='buildsec'>[2] Build Environment Warnings (MSB / NETSDK — NOT security)</h2>
<p style="color:#666;font-size:13px">These are build-configuration warnings (missing references, binding redirects, etc.),
<b>not</b> security vulnerabilities. They are shown here for completeness but do not indicate code-level security risk.</p>
<table><tr><th>Build warning</th></tr>
$buildRows
</table>

<h2>[3] NuGet Package Vulnerabilities</h2>
<table><tr><th>Finding</th></tr>
$vulnRows
</table>
</body></html>
"@

[System.IO.File]::WriteAllText($ReportHtml, $html, [System.Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host ("=" * 62) -ForegroundColor Green
Write-Host "  Scan complete!" -ForegroundColor Green
Write-Host "  SAST findings         : $sastCount" -ForegroundColor Green
Write-Host "  Build env warnings    : $buildCount" -ForegroundColor Green
Write-Host "  TXT  : $ReportTxt" -ForegroundColor Green
Write-Host "  HTML : $ReportHtml" -ForegroundColor Green
Write-Host ("=" * 62) -ForegroundColor Green
Write-Host ""
Start-Process $ReportHtml
""".lstrip("\n")

# ─────────────────────────────────────────────────────────────────────
# README.txt
# ─────────────────────────────────────────────────────────────────────
README = """\
.NET SAST Scanner - No Docker Version (v4)
============================================

Changes in v4
-------------
- Each SAST finding is now paired with a Traditional-Chinese remediation
  hint ("建議修復方式") drawn from a built-in knowledge base covering
  the full SCS0001-SCS0035 and CA2100 / CA3xxx / CA53xx-CA54xx rules.
- HTML report: the [1] Security Findings table is now 3 columns
  (Finding / Rule / 建議修復方式).
- TXT report: each finding is followed by an indented "修復建議" line.
- TXT report now written as UTF-8 *with* BOM so Notepad displays the
  Chinese hints correctly.
- Rules not in the knowledge base still appear but with a fallback note
  pointing to the official docs.

Changes in v3 (IMPORTANT)
-------------------------
- Your ORIGINAL PROJECT IS NOW 100% READ-ONLY.
  v2 wrote Directory.Build.props into your project directory and ran
  `dotnet restore`, which polluted obj/, bin/, and packages.lock.json —
  so after scanning, the project could fail to rebuild in Visual Studio.
- v3 first copies your project to an isolated temp workspace
  (%TEMP%\scs-scan-<timestamp>), injects the analyzers THERE, builds THERE,
  and deletes the whole temp folder at the end. The original project is
  never modified. You can safely run the scan on an active working tree.
- bin/, obj/, .vs/, .git/, node_modules/, packages/, TestResults/ are
  excluded from the copy to keep it fast.

Changes in v2
-------------
- Forced English output from dotnet (prevents garbled Chinese in reports).
- Filter tightened: only real SAST rules (SCS/CA/SA) appear under "Security".
- Build environment warnings (MSB/NETSDK) are now in a SEPARATE section,
  not mixed into security findings.
- Clarified: "SecurityCodeScan.VS2019" works with ALL VS versions (2019/2022).

Requirements
------------
- Windows 10 / 11
- .NET SDK 6.0 or later (9.0 recommended)  -> https://dotnet.microsoft.com/download
- PowerShell 5.1 or above (built-in on Windows 10/11)
- Internet access on first run (NuGet restore)

Quick Start
-----------
1. Open PowerShell
2. cd to this folder
3. Run:

     .\\scan.ps1 -ProjectPath "D:\\YourProject\\"

4. HTML report opens in browser automatically.
   All reports are saved to:  .\\sast-output\\

FAQ
---

Q: Does "SecurityCodeScan.VS2019" only work with Visual Studio 2019?
A: NO. That's just the NuGet package brand name. The analyzer is a pure
   Roslyn component and works with:
     - Visual Studio 2019
     - Visual Studio 2022
     - VS Code / Rider
     - Plain "dotnet build" from CLI
   You do NOT need Visual Studio installed at all.

Q: My report shows lots of MSB3884 / MSB3245 / MSB3836 warnings. Is my code insecure?
A: NO. Those are MSBuild build-environment warnings (missing ruleset files,
   missing references, binding redirect issues). They're grouped under the
   "Build Environment Warnings" section in v2, not "Security Findings".
   Real SAST findings have IDs like SCS0001, CA2100, SA5394.

Q: Report shows 0 SAST findings. Is that correct?
A: Either:
   (a) Your project is genuinely clean -> great.
   (b) The analyzers did not run. This happens when the project uses
       old packages.config (classic .NET Framework) instead of PackageReference.
       In that case the Directory.Build.props injection may not apply.
       -> Workaround: Migrate to PackageReference, or use SonarQube/Roslynator.

Q: Chinese characters look like "?????" in output.
A: Fixed in v2. The scanner now forces dotnet to emit English and sets
   the console to UTF-8.

Q: Does this scanner modify my project files, obj/, bin/, or packages.lock.json?
A: No. Starting in v3 the scanner copies your project to
   %TEMP%\scs-scan-<timestamp>, runs restore/build there, and deletes the
   whole temp folder on completion. Your original project tree, obj/, bin/,
   and lock files are never touched. You can scan an actively-developed
   project without breaking its build.

Q: I already ran the old v2 and my project will not rebuild. How do I recover?
A: In your project root:
     1. Delete obj\ and bin\ in every affected project.
     2. If a packages.lock.json appeared that you did not commit, delete it.
     3. If Directory.Build.props was left in the root (or was changed), restore
        it from git (`git checkout -- Directory.Build.props`) or delete it if
        it never existed before.
     4. Run `dotnet restore` then rebuild.
   v3 will not cause this problem again.

Troubleshooting
---------------
- "dotnet not found"
    Install .NET SDK, then reopen PowerShell.

- Script blocked by execution policy
    Run: Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
    Then retry.

- NuGet restore proxy/firewall error
    Ensure https://api.nuget.org/v3/index.json is reachable.
"""

# ─────────────────────────────────────────────────────────────────────
# Build ZIP
# ─────────────────────────────────────────────────────────────────────
with zipfile.ZipFile(OUT_ZIP, "w", zipfile.ZIP_DEFLATED) as zf:
    ps1_bytes = BOM + SCAN_PS1.encode("utf-8")
    zf.writestr("scan.ps1", ps1_bytes)
    print(f"  + scan.ps1   ({len(ps1_bytes):,} bytes)")

    readme_bytes = README.encode("utf-8")
    zf.writestr("README.txt", readme_bytes)
    print(f"  + README.txt ({len(readme_bytes):,} bytes)")

# Verify BOM + scan for garbled strings (consecutive ?? in string literals)
with zipfile.ZipFile(OUT_ZIP) as zf:
    ps1_data = zf.read("scan.ps1")
    bom_ok = ps1_data[:3] == BOM

# Gangled text scan on scan.ps1 content
import re
garbled = []
for i, line in enumerate(SCAN_PS1.splitlines(), 1):
    stripped = line.lstrip()
    if stripped.startswith("#") or stripped.startswith("//"):
        continue
    # Look for string literals containing consecutive ??
    if re.search(r"""['"`][^'"`\n]*\?\?[^'"`\n]*['"`]""", line):
        garbled.append((i, line.strip()[:80]))

print(f"\n封包完成: {OUT_ZIP}")
print(f"大小: {OUT_ZIP.stat().st_size / 1024:.1f} KB")
print(f"BOM 驗證: {'PASS' if bom_ok else 'FAIL!'}")
print(f"亂碼掃描: {'PASS (0 個)' if not garbled else f'FAIL ({len(garbled)} 個)'}")
if garbled:
    for ln, content in garbled:
        print(f"  line {ln}: {content}")

print(f"\n同事用法: .\\scan.ps1 -ProjectPath \"D:\\SCSHR\\7.5\\modules\\backend\\AIS.Business.RCR\"")
