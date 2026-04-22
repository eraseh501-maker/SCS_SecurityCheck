<#
.SYNOPSIS
    將 Docker 掃描工具打包成一個 ZIP，方便寄給同事
.DESCRIPTION
    打包內容：
      - Dockerfile（映像定義）
      - entrypoint.sh（容器執行邏輯）
      - scan.ps1（一鍵掃描腳本）
      - docker-compose.yml（進階使用）
      - generate_report.py（報告產生器）
      - HOWTO.txt（同事快速上手說明）
.EXAMPLE
    .\pack-delivery.ps1
    # 產生：dotnet-sast-docker-v1.0.zip
#>

param(
    [string]$OutputName = "dotnet-sast-docker-v1.0",
    [string]$OutputDir  = "."
)

$ErrorActionPreference = "Stop"
$SCRIPT_DIR  = Split-Path -Parent $MyInvocation.MyCommand.Path
$SKILL_ROOT  = Split-Path -Parent $SCRIPT_DIR   # dotnet-sast-pipeline/
$TempDir     = Join-Path $env:TEMP "dotnet-sast-pack-$(Get-Random)"
$PackageDir  = Join-Path $TempDir $OutputName

Write-Host "正在打包 SAST Docker 工具..." -ForegroundColor Cyan

# 建立暫存目錄
New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null

# ── 複製必要檔案 ──
$filesToCopy = @(
    @{ Src = Join-Path $SCRIPT_DIR "Dockerfile";        Dst = "Dockerfile" },
    @{ Src = Join-Path $SCRIPT_DIR "entrypoint.sh";     Dst = "entrypoint.sh" },
    @{ Src = Join-Path $SCRIPT_DIR "scan.ps1";          Dst = "scan.ps1" },
    @{ Src = Join-Path $SCRIPT_DIR "docker-compose.yml";Dst = "docker-compose.yml" },
    @{ Src = Join-Path $SCRIPT_DIR ".dockerignore";     Dst = ".dockerignore" },
    @{ Src = Join-Path $SCRIPT_DIR "HOWTO.txt";         Dst = "HOWTO.txt" },
    @{ Src = Join-Path $SKILL_ROOT "scripts\generate_report.py"; Dst = "scripts\generate_report.py" }
)

New-Item -ItemType Directory -Path (Join-Path $PackageDir "scripts") -Force | Out-Null

foreach ($f in $filesToCopy) {
    if (Test-Path $f.Src) {
        Copy-Item $f.Src (Join-Path $PackageDir $f.Dst) -Force
        Write-Host "  ✅ 已加入：$($f.Dst)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  找不到：$($f.Src)" -ForegroundColor Yellow
    }
}

# HOWTO.txt 已包含在 $filesToCopy 清單中，由上方迴圈一併複製

# ── 壓縮 ──
$ZipPath = Join-Path (Resolve-Path $OutputDir) "$OutputName.zip"

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Compress-Archive -Path "$PackageDir\*" -DestinationPath $ZipPath -Force

# ── 清理暫存 ──
Remove-Item $TempDir -Recurse -Force

# ── 完成 ──
$size = [math]::Round((Get-Item $ZipPath).Length / 1KB, 1)
Write-Host ""
Write-Host "╔══════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║           打包完成！                      ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════╝" -ForegroundColor Green
Write-Host "  📦 輸出檔案：$ZipPath" -ForegroundColor White
Write-Host "  📏 檔案大小：${size} KB" -ForegroundColor White
Write-Host ""
Write-Host "  寄給同事後，他只需要：" -ForegroundColor Cyan
Write-Host "  1. 安裝 Docker Desktop" -ForegroundColor White
Write-Host "  2. 解壓縮 ZIP" -ForegroundColor White
Write-Host "  3. 執行：.\scan.ps1 -ProjectPath '專案路徑' -Build" -ForegroundColor White
