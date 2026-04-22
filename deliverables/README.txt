=============================================================
  .NET / C# 資安弱點掃描技能包 v2 — LITE Edition
  打包日期：2026-04-22
  適用：Claude Code（建議 Opus 級模型效果最佳）
=============================================================

【包含技能】
  1. dotnet-sast-pipeline    — 工具版完整 SAST（Semgrep + Gitleaks + SecurityCodeScan）
  2. security-review         — OWASP Top 10 安全審查 Checklist
  3. security-scan           — AgentShield 設定檔弱點偵測
  4. dotnet-ai-security-audit★ — AI 純推理版（不需安裝任何外部工具！）

★ 技能 4 說明：
  只要電腦有安裝 Claude Code 並有 API 額度（建議 Opus 級），
  不需 Semgrep / Gitleaks / dotnet tool，即可直接讀取 C# 原始碼
  進行語義級資安掃描，包含 SQL Injection、XSS、硬編碼密碼等。

=============================================================
【安裝步驟】
=============================================================

步驟 1：確認 Claude Code 已安裝
  npm install -g @anthropic-ai/claude-code

步驟 2：複製技能資料夾到 Claude Code 全域技能目錄
  Windows：
    複製 skills\* 資料夾 → C:\Users\<你的帳號>\.claude\skills\

  範例（PowerShell）：
    Copy-Item -Recurse skills\dotnet-sast-pipeline      "C:\Users\ChenVincent\.claude\skills\"
    Copy-Item -Recurse skills\security-review           "C:\Users\ChenVincent\.claude\skills\"
    Copy-Item -Recurse skills\security-scan             "C:\Users\ChenVincent\.claude\skills\"
    Copy-Item -Recurse skills\dotnet-ai-security-audit  "C:\Users\ChenVincent\.claude\skills\"

步驟 3：重啟 VS Code

=============================================================
【使用方式】
=============================================================

【方式 A — AI 純推理掃描（不裝工具、立即可用）】
  在 VS Code Copilot Chat 輸入：
    「對 D:\MyProject 執行 AI 資安掃描」
    「Run AI security audit on D:\MyProject\MySolution.sln」
    「Claude 幫我看看這個 C# 專案有沒有安全漏洞」

【方式 B — 工具版完整掃描（需先安裝工具）】
  先安裝工具：
    pip install semgrep
    dotnet tool install --global security-scan
    winget install gitleaks
  
  然後在 VS Code Copilot Chat 輸入：
    「對 D:\MyProject 執行 .NET SAST 弱點掃描」

=============================================================
【AI 掃描 vs 工具掃描 比較】
=============================================================
  項目                    AI 版                工具版
  安裝需求                僅 Claude Code       Semgrep + 3 工具
  SQL Injection 偵測      ✅（語義分析）        ✅（Pattern match）
  Git 歷史洩漏掃描        ❌                    ✅（Gitleaks）
  NuGet CVE 查詢          ❌                    ✅（dotnet list）
  跨行邏輯分析            ✅（優勢）            ⚠️
  建議                    快速審查 / CI前       完整 CI/CD 掃描

最佳實踐：兩種方式搭配使用，互補不足。

=============================================================
