param([switch]$Clean)
$ErrorActionPreference = "Stop"
$root    = (Resolve-Path "$PSScriptRoot\..").Path
$project = Join-Path $root "src\SCS.SecurityCheck.Api\SCS.SecurityCheck.Api.csproj"
$outDir  = Join-Path $root "publish\win-x64"

if ($Clean -and (Test-Path $outDir)) {
    Remove-Item $outDir -Recurse -Force
    Write-Host "Cleaned: $outDir"
}

Write-Host "Publishing SCS Security Scanner -> win-x64 single-file EXE..."

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

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exePath = Join-Path $outDir "SCS.SecurityCheck.Api.exe"
if (Test-Path $exePath) {
    $sizeMB = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
    Write-Host ""
    Write-Host "[OK] Published: $exePath  ($sizeMB MB)"
    Write-Host "Usage: run SCS.SecurityCheck.Api.exe, then open http://localhost:5000"
} else {
    Write-Error "EXE not found after publish."
    exit 1
}
