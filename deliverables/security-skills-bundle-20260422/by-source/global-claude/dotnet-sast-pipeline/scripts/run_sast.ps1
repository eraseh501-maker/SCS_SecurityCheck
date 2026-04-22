<#
.SYNOPSIS
    .NET SAST 一鍵掃描腳本（Windows PowerShell）
.DESCRIPTION
    依序執行：NuGet 漏洞掃描、Semgrep、SecurityCodeScan、Gitleaks
    所有結果輸出到 sast-output/ 目錄
.PARAMETER SolutionPath
    .sln 檔案或專案根目錄路徑
.PARAMETER OutputDir
    掃描結果輸出目錄（預設 ./sast-output）
.PARAMETER Tools
    要執行的工具清單，逗號分隔
    可選值：nuget,semgrep,scs,gitleaks,all（預設 all）
.PARAMETER SeverityThreshold
    最低報告嚴重程度：critical|high|medium|low（預設 medium）
.EXAMPLE
    .\run_sast.ps1 -SolutionPath "C:\Projects\MyApp" -Tools "nuget,semgrep"
    .\run_sast.ps1 -SolutionPath "C:\Projects\MyApp.sln" -SeverityThreshold "high"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SolutionPath,

    [string]$OutputDir = ".\sast-output",

    [string]$Tools = "all",

    [ValidateSet("critical","high","medium","low")]
    [string]$SeverityThreshold = "medium"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# ─────────────────────────────────────────────
# 初始化
# ─────────────────────────────────────────────
$StartTime = Get-Date
$OutputDir = Resolve-Path -ErrorAction SilentlyContinue $OutputDir ?? $OutputDir
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$LogFile = Join-Path $OutputDir "sast-run.log"
$SummaryFile = Join-Path $OutputDir "summary.json"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $line = "[$timestamp][$Level] $Message"
    Write-Host $line -ForegroundColor $(switch($Level) {
        "ERROR"   { "Red" }
        "WARNING" { "Yellow" }
        "SUCCESS" { "Green" }
        default   { "Cyan" }
    })
    Add-Content $LogFile $line
}

function Test-CommandExists {
    param([string]$Command)
    return ($null -ne (Get-Command $Command -ErrorAction SilentlyContinue))
}

# ─────────────────────────────────────────────
# 解析工具清單
# ─────────────────────────────────────────────
$ToolList = if ($Tools -eq "all") {
    @("nuget","semgrep","scs","gitleaks")
} else {
    $Tools -split "," | ForEach-Object { $_.Trim().ToLower() }
}

Write-Log "=== .NET SAST Pipeline 開始 ===" "INFO"
Write-Log "目標路徑：$SolutionPath"
Write-Log "輸出目錄：$OutputDir"
Write-Log "執行工具：$($ToolList -join ', ')"
Write-Log "嚴重程度閾值：$SeverityThreshold"

$Results = @{
    run_at    = $StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
    target    = $SolutionPath
    tools     = @{}
}

# ─────────────────────────────────────────────
# 工具 1：dotnet list package --vulnerable
# ─────────────────────────────────────────────
if ($ToolList -contains "nuget") {
    Write-Log "── [1/4] NuGet 相依性漏洞掃描 ──"
    $NugetOut = Join-Path $OutputDir "nuget-vulnerabilities.json"

    if (Test-CommandExists "dotnet") {
        try {
            # 取得所有 .csproj 或使用 .sln
            if ($SolutionPath -match "\.sln$") {
                $scanTarget = $SolutionPath
            } else {
                $scanTarget = $SolutionPath
            }

            # 執行掃描並擷取輸出
            $nugetOutput = & dotnet list $scanTarget package --vulnerable --include-transitive 2>&1
            $nugetText = $nugetOutput | Out-String

            # 計算漏洞數（簡易計算含 ">" 的行）
            $vulnLines = $nugetOutput | Where-Object { $_ -match "^\s+>" }
            $vulnCount = ($vulnLines | Measure-Object).Count

            # 輸出原始文字
            $nugetText | Set-Content (Join-Path $OutputDir "nuget-vulnerabilities.txt") -Encoding UTF8

            # 建構 JSON 摘要
            $nugetResult = @{
                status     = "completed"
                vuln_count = $vulnCount
                output_file = "nuget-vulnerabilities.txt"
                findings   = @($vulnLines | ForEach-Object {
                    if ($_ -match ">\s+(\S+)\s+(\S+)\s+.*Severity:\s+(\w+)") {
                        @{ package=$Matches[1]; version=$Matches[2]; severity=$Matches[3] }
                    }
                } | Where-Object { $_ })
            }

            $Results.tools["nuget"] = $nugetResult
            Write-Log "NuGet 掃描完成，發現 $vulnCount 個漏洞套件" $(if($vulnCount -gt 0){"WARNING"}else{"SUCCESS"})
        } catch {
            Write-Log "NuGet 掃描失敗：$_" "ERROR"
            $Results.tools["nuget"] = @{ status="error"; error=$_.ToString() }
        }
    } else {
        Write-Log "dotnet CLI 未安裝，跳過 NuGet 掃描" "WARNING"
        $Results.tools["nuget"] = @{ status="skipped"; reason="dotnet not found" }
    }
}

# ─────────────────────────────────────────────
# 工具 2：Semgrep
# ─────────────────────────────────────────────
if ($ToolList -contains "semgrep") {
    Write-Log "── [2/4] Semgrep 靜態分析 ──"
    $SemgrepOut = Join-Path $OutputDir "semgrep-results.json"

    if (Test-CommandExists "semgrep") {
        try {
            $semgrepArgs = @(
                "--config", "p/csharp",
                "--config", "p/owasp-top-ten",
                "--output", $SemgrepOut,
                "--json",
                "--quiet"
            )

            # 嚴重程度篩選
            switch ($SeverityThreshold) {
                "critical" { $semgrepArgs += @("--severity", "ERROR") }
                "high"     { $semgrepArgs += @("--severity", "ERROR", "--severity", "WARNING") }
                default    { } # 包含 INFO
            }

            $semgrepArgs += $SolutionPath

            Write-Log "執行：semgrep $($semgrepArgs -join ' ')"
            & semgrep @semgrepArgs 2>&1 | Out-Null

            if (Test-Path $SemgrepOut) {
                $semgrepData = Get-Content $SemgrepOut | ConvertFrom-Json
                $findingCount = ($semgrepData.results | Measure-Object).Count
                $errorCount   = ($semgrepData.errors  | Measure-Object).Count

                $Results.tools["semgrep"] = @{
                    status        = "completed"
                    finding_count = $findingCount
                    error_count   = $errorCount
                    output_file   = "semgrep-results.json"
                }
                Write-Log "Semgrep 完成：$findingCount 個發現，$errorCount 個解析錯誤" $(if($findingCount -gt 0){"WARNING"}else{"SUCCESS"})
            } else {
                Write-Log "Semgrep 未產生輸出檔案" "WARNING"
                $Results.tools["semgrep"] = @{ status="no_output" }
            }
        } catch {
            Write-Log "Semgrep 執行失敗：$_" "ERROR"
            $Results.tools["semgrep"] = @{ status="error"; error=$_.ToString() }
        }
    } else {
        Write-Log "Semgrep 未安裝（pip install semgrep），跳過" "WARNING"
        $Results.tools["semgrep"] = @{
            status = "skipped"
            reason = "semgrep not installed"
            install = "pip install semgrep"
        }
    }
}

# ─────────────────────────────────────────────
# 工具 3：SecurityCodeScan CLI
# ─────────────────────────────────────────────
if ($ToolList -contains "scs") {
    Write-Log "── [3/4] SecurityCodeScan 分析 ──"
    $ScsOut = Join-Path $OutputDir "scs-results.sarif"

    if (Test-CommandExists "security-scan") {
        try {
            # 找到 .sln 或第一個 .csproj
            $scanFile = if ($SolutionPath -match "\.(sln|csproj)$") {
                $SolutionPath
            } else {
                Get-ChildItem $SolutionPath -Filter "*.sln" -Recurse | Select-Object -First 1 -ExpandProperty FullName
            }

            if ($scanFile) {
                & security-scan $scanFile --export=sarif --output=$ScsOut 2>&1 | Out-Null
                if (Test-Path $ScsOut) {
                    $sarifData = Get-Content $ScsOut | ConvertFrom-Json
                    $findings  = $sarifData.runs[0].results
                    $findCount = ($findings | Measure-Object).Count
                    $Results.tools["scs"] = @{
                        status        = "completed"
                        finding_count = $findCount
                        output_file   = "scs-results.sarif"
                    }
                    Write-Log "SecurityCodeScan 完成：$findCount 個發現" $(if($findCount -gt 0){"WARNING"}else{"SUCCESS"})
                }
            } else {
                Write-Log "找不到 .sln 或 .csproj 檔案" "WARNING"
                $Results.tools["scs"] = @{ status="skipped"; reason="no solution file found" }
            }
        } catch {
            Write-Log "SecurityCodeScan 執行失敗：$_" "ERROR"
            $Results.tools["scs"] = @{ status="error"; error=$_.ToString() }
        }
    } else {
        Write-Log "SecurityCodeScan 未安裝（dotnet tool install -g security-scan），跳過" "WARNING"
        $Results.tools["scs"] = @{
            status  = "skipped"
            reason  = "security-scan not installed"
            install = "dotnet tool install --global security-scan"
        }
    }
}

# ─────────────────────────────────────────────
# 工具 4：Gitleaks（密鑰洩漏掃描）
# ─────────────────────────────────────────────
if ($ToolList -contains "gitleaks") {
    Write-Log "── [4/4] Gitleaks 密鑰洩漏掃描 ──"
    $GitleaksOut = Join-Path $OutputDir "gitleaks-report.json"

    if (Test-CommandExists "gitleaks") {
        try {
            & gitleaks detect `
                --source $SolutionPath `
                --report-format json `
                --report-path $GitleaksOut `
                --exit-code 0 `
                2>&1 | Out-Null

            if (Test-Path $GitleaksOut) {
                $leakData = Get-Content $GitleaksOut | ConvertFrom-Json
                $leakCount = ($leakData | Measure-Object).Count
                $Results.tools["gitleaks"] = @{
                    status     = "completed"
                    leak_count = $leakCount
                    output_file = "gitleaks-report.json"
                }
                Write-Log "Gitleaks 完成：$leakCount 個密鑰洩漏發現" $(if($leakCount -gt 0){"WARNING"}else{"SUCCESS"})
            }
        } catch {
            Write-Log "Gitleaks 執行失敗：$_" "ERROR"
            $Results.tools["gitleaks"] = @{ status="error"; error=$_.ToString() }
        }
    } else {
        Write-Log "Gitleaks 未安裝（choco install gitleaks），跳過" "WARNING"
        $Results.tools["gitleaks"] = @{
            status  = "skipped"
            reason  = "gitleaks not installed"
            install = "choco install gitleaks  # or: winget install Gitleaks.Gitleaks"
        }
    }
}

# ─────────────────────────────────────────────
# 輸出總結
# ─────────────────────────────────────────────
$EndTime = Get-Date
$Results["duration_seconds"] = [int]($EndTime - $StartTime).TotalSeconds

$Results | ConvertTo-Json -Depth 10 | Set-Content $SummaryFile -Encoding UTF8

Write-Log ""
Write-Log "=== 掃描完成 ===" "SUCCESS"
Write-Log "耗時：$($Results.duration_seconds) 秒"
Write-Log "結果目錄：$OutputDir"
Write-Log "摘要檔案：$SummaryFile"

# 顯示各工具狀態
foreach ($tool in $Results.tools.Keys) {
    $t = $Results.tools[$tool]
    $statusIcon = switch ($t.status) {
        "completed" { "✅" }
        "skipped"   { "⏭️" }
        "error"     { "❌" }
        default     { "❓" }
    }
    Write-Host "  $statusIcon $tool : $($t.status)" -ForegroundColor $(
        switch ($t.status) {
            "completed" { "Green" }
            "skipped"   { "Yellow" }
            "error"     { "Red" }
            default     { "Gray" }
        }
    )
}

Write-Log ""
Write-Log "下一步：執行 python generate_report.py --input $OutputDir 產生完整報告"
