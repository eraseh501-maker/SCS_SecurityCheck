#Requires -Version 5.1
<#
.SYNOPSIS
  同步全域 Claude Code skills 到專案本地 .claude/skills
  
.DESCRIPTION
  每次更新全域 skills 後，執行此腳本自動複製到專案目錄
  所有 skills 都會被加入 git 版本控制，確保團隊同步
  
.EXAMPLE
  PS> .\sync-skills-to-project.ps1
  
.NOTES
  - 全域 skills 位置: $env:USERPROFILE\.claude\skills
  - 專案 skills 位置: .\.claude\skills (相對於此腳本)
  - 建議每次 push 前執行此腳本
#>

param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string[]]$SkillNames = @(
        "dotnet-sast-pipeline",
        "dotnet-ai-security-audit",
        "security-review",
        "security-scan"
    ),
    [switch]$Verify
)

$ErrorActionPreference = "Stop"

$globalSkillsPath = "$env:USERPROFILE\.claude\skills"
$projectSkillsPath = "$ProjectPath\.claude\skills"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  Claude Code Skills 同步工具" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📁 全域 skills: $globalSkillsPath"
Write-Host "📁 專案 skills: $projectSkillsPath"
Write-Host ""

# 確保目錄存在
if (!(Test-Path $projectSkillsPath)) {
    New-Item -ItemType Directory -Path $projectSkillsPath -Force | Out-Null
    Write-Host "✨ 已建立專案 skills 目錄" -ForegroundColor Green
}

$syncedCount = 0
$skippedCount = 0

# 複製 skills
foreach ($skill in $SkillNames) {
    $src = "$globalSkillsPath\$skill"
    $dst = "$projectSkillsPath\$skill"
    
    if (Test-Path $src) {
        # 移除舊版本
        if (Test-Path $dst) {
            Remove-Item $dst -Recurse -Force | Out-Null
        }
        
        # 複製
        Copy-Item $src $dst -Recurse -Force | Out-Null
        Write-Host "✅ 同步: $skill" -ForegroundColor Green
        $syncedCount++
    } else {
        Write-Host "⚠️  跳過: $skill (不存在於全域)" -ForegroundColor Yellow
        $skippedCount++
    }
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  同步結果" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "✅ 同步成功: $syncedCount"
Write-Host "⚠️  跳過: $skippedCount"
Write-Host ""

# 驗證模式：檢查每個 skill 是否完整
if ($Verify) {
    Write-Host "🔍 驗證 skills 完整性..." -ForegroundColor Cyan
    foreach ($skill in $SkillNames) {
        $skillPath = "$projectSkillsPath\$skill"
        if (Test-Path $skillPath) {
            $skillMd = "$skillPath\SKILL.md"
            if (Test-Path $skillMd) {
                $size = (Get-Item $skillMd).Length
                Write-Host "  ✅ $skill - $(([math]::Round($size/1KB, 1))) KB" -ForegroundColor Green
            } else {
                Write-Host "  ❌ $skill - 缺少 SKILL.md" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""
Write-Host "💡 下一步："
Write-Host "  1. 檢查變更: git status"
Write-Host "  2. 提交: git add .claude/ && git commit -m 'chore: sync skills'"
Write-Host "  3. 推送: git push"
Write-Host ""
Write-Host "同事只需執行 git pull 即可自動獲得最新 skills" -ForegroundColor Cyan
