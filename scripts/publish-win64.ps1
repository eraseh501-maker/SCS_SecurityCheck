<#
.SYNOPSIS
  將 SCS.SecurityCheck.Api 打包為 Windows x64 自包含單一 EXE
.DESCRIPTION
  產生位於 publish\win-x64\SCS.SecurityCheck.Api.exe 的可攜帶執行檔，
  不需要目標機器安裝 .NET Runtime。
.PARAMETER Clean
  發布前先清除舊的 publish 資料夾
#>
param(
  [switch]$Clean
)

$ErrorActionPreference = "Stop"
$root    = (Resolve-Path "$PSScriptRoot\..").Path
$project = Join-Path $root "src\SCS.SecurityCheck.Api\SCS.SecurityCheck.Api.csproj"
$outDir  = Join-Path $root "publish\win-x64"

if ($Clean -and (Test-Path $outDir)) {
  Write-Host "清除舊的輸出目錄..." -ForegroundColor Yellow
  Remove-Item $outDir -Recurse -Force
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SCS 安全掃描工具 - 單一 EXE 發布" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "目標平台 : win-x64 (self-contained)" -ForegroundColor White
Write-Host "輸出目錄 : $outDir" -ForegroundColor White
Write-Host ""

$sw = [Diagnostics.Stopwatch]::StartNew()

dotnet publish $project `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishTrimmed=false `
  --output $outDir `
  --verbosity minimal

if ($LASTEXITCODE -ne 0) {
  Write-Host ""
  Write-Error "❌ 發布失敗 (exit code $LASTEXITCODE)"
  exit $LASTEXITCODE
}

$sw.Stop()

$exePath = Join-Path $outDir "SCS.SecurityCheck.Api.exe"
if (Test-Path $exePath) {
  $sizeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
  Write-Host ""
  Write-Host "✅ 發布成功！" -ForegroundColor Green
  Write-Host "   檔案: $exePath" -ForegroundColor Green
  Write-Host "   大小: $sizeMB MB" -ForegroundColor Green
  Write-Host "   耗時: $([math]::Round($sw.Elapsed.TotalSeconds, 1)) 秒" -ForegroundColor Green
  Write-Host ""
  Write-Host "使用方式：" -ForegroundColor Cyan
  Write-Host "  1. 複製 publish\win-x64\ 整個資料夾到目標機器" -ForegroundColor White
  Write-Host "  2. 執行 SCS.SecurityCheck.Api.exe" -ForegroundColor White
  Write-Host "  3. 開啟瀏覽器前往 http://localhost:5000" -ForegroundColor White
  Write-Host ""
  Write-Host "自訂連接埠：" -ForegroundColor Cyan
  Write-Host "  set ASPNETCORE_URLS=http://localhost:8080" -ForegroundColor White
  Write-Host "  SCS.SecurityCheck.Api.exe" -ForegroundColor White
} else {
  Write-Error "找不到輸出 EXE，請檢查上方建置訊息。"
  exit 1
}
