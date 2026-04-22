<#
.SYNOPSIS
    .NET SAST Scanner — 一鍵掃描腳本（同事使用版）
.DESCRIPTION
    用 Docker 執行 SAST 安全掃描，不需要在本機安裝任何掃描工具。
    掃描結果會輸出到你的專案目錄下的 sast-output\ 資料夾。
.PARAMETER ProjectPath
    要掃描的 .NET 專案目錄（預設：當前目錄）
.PARAMETER Tools
    要執行的工具：all | nuget | semgrep | scs | gitleaks（預設：all）
.PARAMETER Severity
    最低報告嚴重程度：medium | high | critical（預設：medium）
.PARAMETER Build
    強制重新建置 Docker 映像（預設：否）
.EXAMPLE
    # 掃描當前目錄
    .\scan.ps1

    # 掃描指定專案
    .\scan.ps1 -ProjectPath "C:\Projects\MyCommercialApp"

    # 只掃描 NuGet 相依性漏洞
    .\scan.ps1 -ProjectPath "C:\Projects\MyApp" -Tools "nuget"

    # 只報告 High 以上的漏洞
    .\scan.ps1 -ProjectPath "C:\Projects\MyApp" -Severity "high"

    # 第一次使用（重新建置映像）
    .\scan.ps1 -ProjectPath "C:\Projects\MyApp" -Build
#>

param(
    [string]$ProjectPath = ".",
    [ValidateSet("all","nuget","semgrep","scs","gitleaks")]
    [string]$Tools = "all",
    [ValidateSet("medium","high","critical")]
    [string]$Severity = "medium",
    [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 切換控制台為 UTF-8，避免中文與方框字元顯示亂碼
$null = chcp 65001

$IMAGE_NAME = "dotnet-sast-scanner:latest"
$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── 顏色輸出函式 ──
function Write-Color {
    param([string]$Text, [ConsoleColor]$Color = "Cyan")
    Write-Host $Text -ForegroundColor $Color
}

function Write-Step  { Write-Color "`n▶ $args" "Cyan" }
function Write-Ok    { Write-Color "  ✅ $args" "Green" }
function Write-Warn  { Write-Color "  ⚠️  $args" "Yellow" }
function Write-Fail  { Write-Color "  ❌ $args" "Red" }

Write-Color @"

╔══════════════════════════════════════════════════╗
║       .NET SAST Scanner — Docker 版              ║
║  掃描 C# 專案漏洞，報告輸出到 sast-output\       ║
╚══════════════════════════════════════════════════╝
"@ "Cyan"

# ── 步驟 1：確認 Docker 已安裝並執行中 ──
Write-Step "確認 Docker 環境..."

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Fail "找不到 Docker！請先安裝 Docker Desktop："
    Write-Host "  https://www.docker.com/products/docker-desktop/" -ForegroundColor Blue
    exit 1
}

try {
    docker info --format "{{.ServerVersion}}" 2>&1 | Out-Null
    Write-Ok "Docker 執行中"
} catch {
    Write-Fail "Docker Desktop 未執行，請先啟動它"
    exit 1
}

# ── 步驟 2：取得 Docker 映像（三段式優先順序）──
# 優先順序：① 已存在映像 → ② 載入 tar.gz → ③ 從 Dockerfile 建置
Write-Step "Preparing Docker image..."

$imageExists  = (docker images -q $IMAGE_NAME 2>$null)
$TarGzPath    = Join-Path $SCRIPT_DIR "dotnet-sast-scanner.tar.gz"
$TarPath      = Join-Path $SCRIPT_DIR "dotnet-sast-scanner.tar"

if ($imageExists -and -not $Build) {
    # ① 映像已存在，直接使用
    Write-Ok "Image ready: $IMAGE_NAME"

} elseif (-not $Build -and (Test-Path $TarGzPath)) {
    # ② 找到 tar.gz，載入（比重新 build 快很多，不需網路）
    Write-Host "  Loading image from tar.gz (no internet required)..." -ForegroundColor Cyan
    Write-Host "  File: $TarGzPath" -ForegroundColor Gray

    $tarSize = [math]::Round((Get-Item $TarGzPath).Length / 1MB, 0)
    Write-Host "  Size: ${tarSize} MB — this may take 1-3 minutes..." -ForegroundColor Gray

    docker load -i $TarGzPath
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "docker load failed. Try rebuilding: .\scan.ps1 -Build"
        exit 1
    }
    Write-Ok "Image loaded successfully: $IMAGE_NAME"

} elseif (-not $Build -and (Test-Path $TarPath)) {
    # ③ 找到未壓縮 tar
    Write-Host "  Loading image from tar (no internet required)..." -ForegroundColor Cyan
    docker load -i $TarPath
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "docker load failed. Try rebuilding: .\scan.ps1 -Build"
        exit 1
    }
    Write-Ok "Image loaded: $IMAGE_NAME"

} else {
    # ④ 從 Dockerfile 建置（需要網路，首次約 5-10 分鐘）
    if ($Build) {
        Write-Host "  Force rebuild requested..." -ForegroundColor Yellow
    } else {
        Write-Host "  No image or tar found. Building from Dockerfile (needs internet)..." -ForegroundColor Yellow
    }

    $dockerfileDir = $SCRIPT_DIR
    if (-not (Test-Path (Join-Path $dockerfileDir "Dockerfile"))) {
        Write-Fail "Dockerfile not found in: $dockerfileDir"
        exit 1
    }

    $buildContext = Split-Path -Parent $SCRIPT_DIR
    Write-Host "  Building... please wait..." -ForegroundColor Gray

    docker build `
        -t $IMAGE_NAME `
        -f (Join-Path $dockerfileDir "Dockerfile") `
        $buildContext

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build failed. Check Docker settings and internet connection."
        exit 1
    }
    Write-Ok "Image built: $IMAGE_NAME"
}

# ── 步驟 3：解析專案路徑 ──
Write-Step "確認掃描目標..."

$AbsPath = Resolve-Path $ProjectPath -ErrorAction SilentlyContinue
if (-not $AbsPath) {
    Write-Fail "目錄不存在：$ProjectPath"
    exit 1
}
$AbsPath = $AbsPath.Path
Write-Ok "掃描目標：$AbsPath"

# 確認有 .cs 或 .sln 檔案
$hasCsFiles = (Get-ChildItem $AbsPath -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | Select-Object -First 1)
if (-not $hasCsFiles) {
    Write-Warn "在此目錄找不到 .cs 檔案，確認這是 .NET 專案目錄？"
}

# ── 步驟 4：執行掃描 ──
Write-Step "開始掃描（工具：$Tools，嚴重程度：$Severity）..."
Write-Host "  原始碼不會離開你的電腦，掃描完全在本機 Docker 容器內執行" -ForegroundColor DarkGreen
Write-Host ""

$StartTime = Get-Date

docker run `
    --rm `
    --name dotnet-sast-scan `
    -v "${AbsPath}:/scan" `
    -e "TOOLS=$Tools" `
    -e "SEVERITY=$Severity" `
    $IMAGE_NAME

$ExitCode = $LASTEXITCODE
$Duration = [int]((Get-Date) - $StartTime).TotalSeconds

# ── 步驟 5：顯示結果 ──
Write-Host ""
if ($ExitCode -eq 0) {
    $ReportPath = Join-Path $AbsPath "sast-output\security-report.md"
    Write-Color @"

╔══════════════════════════════════════════════════╗
║                 掃描完成！                        ║
╚══════════════════════════════════════════════════╝
"@ "Green"
    Write-Ok "耗時：$Duration 秒"
    Write-Ok "報告位置：$ReportPath"
    Write-Host ""

    if (Test-Path $ReportPath) {
        # 顯示報告摘要（前 30 行）
        Write-Color "── 報告預覽 ──" "Yellow"
        Get-Content $ReportPath -Encoding UTF8 | Select-Object -First 30 | ForEach-Object {
            Write-Host "  $_"
        }
        Write-Host "  ..."
        Write-Host ""
        Write-Color "  用 Markdown 閱讀器或 VS Code 開啟完整報告：" "Cyan"
        Write-Host "  code `"$ReportPath`"" -ForegroundColor White
    }
} else {
    Write-Fail "掃描過程發生錯誤（exit code：$ExitCode）"
    Write-Host "  請查看 $AbsPath\sast-output\sast-run.log 了解詳情" -ForegroundColor Yellow
    exit $ExitCode
}
