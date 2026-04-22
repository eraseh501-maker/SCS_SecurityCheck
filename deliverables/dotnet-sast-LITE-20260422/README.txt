=== .NET / C# SAST 核心技能包（輕量版，不含 Docker image）===
日期：2026-04-22

【包含技能】
  skills\dotnet-sast-pipeline  - 完整弱點掃描流程腳本與說明
  skills\security-review       - OWASP Top 10 安全審查 checklist
  skills\security-scan         - AgentShield 設定檔弱點掃描

【安裝步驟】
1. 解壓縮此 ZIP
2. 將 skills\ 裡的三個資料夾複製到：
   C:\Users\<你的帳號>\.claude\skills\
3. 重新啟動 VS Code

【使用方式】
在 GitHub Copilot Chat 輸入：
  「請對 D:\MyProject\MySolution.sln 執行 .NET SAST 弱點掃描」

掃描涵蓋：SQL Injection、XSS、CSRF、硬編碼密鑰、NuGet CVE、Gitleaks

【注意】此輕量版不含 Docker image；掃描工具需自行安裝：
  pip install semgrep
  dotnet tool install -g security-scan
  choco install gitleaks
