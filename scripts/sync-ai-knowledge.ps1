# 將 AI-Knowledge 母版的全域開發守則同步到本專案
# 執行方式：powershell -ExecutionPolicy Bypass -File scripts\sync-ai-knowledge.ps1
#
# 正確的更新流程：
#   1. 先改母版：D:\Vincent\AI-Knowledge\global\90_全域AI開發守則.md
#   2. 再跑本腳本同步回來：
#      powershell -ExecutionPolicy Bypass -File scripts\sync-ai-knowledge.ps1

[CmdletBinding()]
param(
    [string]$KnowledgeRoot = "D:\Vincent\AI-Knowledge"
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$syncScript  = Join-Path $KnowledgeRoot "scripts\Sync-ProjectLessons.ps1"

if (-not (Test-Path -LiteralPath $syncScript)) {
    throw "Sync script not found: $syncScript`n請確認 AI-Knowledge 位於 $KnowledgeRoot"
}

& $syncScript -TargetProjectPath $projectRoot -KnowledgeRoot $KnowledgeRoot
Write-Host ""
Write-Host "同步完成。docs\90_全域AI開發守則.md 已更新為最新母版內容。"
