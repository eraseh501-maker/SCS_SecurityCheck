=== .NET / C# SAST 核心技能包 ===
日期：2026-04-22

【包含技能】
1. dotnet-sast-pipeline  — .NET/C# 完整弱點掃描流程
   使用工具：Semgrep、Gitleaks、SecurityCodeScan、dotnet list package --vulnerable
   偵測範疇：OWASP Top 10、SQL Injection、XSS、CSRF、密鑰洩漏、NuGet CVE

2. security-review  — OWASP Top 10 安全審查 checklist
   適用於：程式碼審查前的安全確認

3. security-scan  — AgentShield 設定檔弱點掃描
   適用於：VS Code Copilot / Claude 設定安全性檢查

【安裝步驟】
1. 解壓縮此 ZIP
2. 複製 skills\ 內的資料夾到你的 Claude/Copilot skills 目錄：
   Windows：C:\Users\<你的帳號>\.claude\skills\
   （若目錄不存在請自行建立）

3. 複製後結構應如下：
   C:\Users\<帳號>\.claude\skills\dotnet-sast-pipeline\SKILL.md
   C:\Users\<帳號>\.claude\skills\security-review\SKILL.md
   C:\Users\<帳號>\.claude\skills\security-scan\SKILL.md

4. 重新啟動 VS Code

【使用方式】
在 GitHub Copilot Chat 或 Claude 對話框中輸入：
  「請對 D:\MyProject\MySolution.sln 執行 .NET SAST 弱點掃描」
  「Run dotnet SAST scan on D:\MyProject\MySolution.sln」

掃描涵蓋：
  - SQL Injection（未使用 SqlParameter）
  - 硬編碼密碼 / API Key
  - 弱加密（MD5、SHA1、DES）
  - XSS / CSRF / Path Traversal
  - NuGet 相依性 CVE
  - Git 歷史密鑰洩漏（Gitleaks）
