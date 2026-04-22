# .NET SAST 工具安裝與設定指南

## 工具清單與優先順序

| 優先 | 工具 | 用途 | 授權 | 安裝難度 |
|------|------|------|------|---------|
| P0 | `dotnet list package --vulnerable` | NuGet 相依性漏洞 | 免費（內建） | 零安裝 |
| P0 | Semgrep OSS | 靜態程式碼分析 | LGPL-2.1 | 低 |
| P1 | SecurityCodeScan | Roslyn 安全分析器 | GPL-3.0 | 低 |
| P1 | Gitleaks | 密鑰洩漏掃描 | MIT | 低 |
| P2 | OWASP Dependency Check | CVE 相依性掃描 | Apache-2.0 | 中 |
| P2 | SonarScanner for .NET | 綜合品質+安全 | LGPL-3.0 | 中 |
| P3 | Snyk CLI | 商業等級漏洞DB | 免費/商業 | 低 |

---

## P0：dotnet list package --vulnerable（零安裝）

```powershell
# 掃描直接相依性
dotnet list package --vulnerable

# 掃描直接+間接相依性（含傳遞性）
dotnet list package --vulnerable --include-transitive

# 輸出 JSON 格式（供程式解析）
dotnet list package --vulnerable --include-transitive --format json > nuget-vulns.json
```

**需求**：.NET SDK 5.0+（建議 6.0+）

---

## P0：Semgrep OSS

### 安裝
```bash
# 方式 1：pip（跨平台）
pip install semgrep

# 方式 2：Homebrew（macOS/Linux）
brew install semgrep

# 方式 3：Docker
docker pull semgrep/semgrep:latest
```

### C# 掃描
```bash
# 使用官方 C# 安全規則集
semgrep --config "p/csharp" ./src

# OWASP Top 10 規則
semgrep --config "p/owasp-top-ten" ./src

# 多規則組合，輸出 JSON
semgrep \
  --config "p/csharp" \
  --config "p/owasp-top-ten" \
  --config "p/default" \
  --output semgrep-results.json \
  --json \
  --severity ERROR \
  --severity WARNING \
  ./src

# Docker 執行（不需本機安裝 Python）
docker run --rm \
  -v "${PWD}:/src" \
  semgrep/semgrep:latest \
  semgrep --config "p/csharp" --json --output /src/semgrep-results.json /src
```

### 自訂規則範例（`rules/custom-dotnet.yaml`）
```yaml
rules:
  - id: hardcoded-connection-string
    pattern: |
      string $VAR = "...Password=...";
    message: Hardcoded password detected in connection string
    languages: [csharp]
    severity: ERROR
    metadata:
      cwe: "CWE-798"
      owasp: "A07:2021"
```

---

## P1：SecurityCodeScan（Roslyn NuGet Analyzer）

### 安裝方式 1：NuGet 套件（推薦，CI/CD 自動掃描）
```xml
<!-- 加到 .csproj 或 Directory.Build.props -->
<ItemGroup>
  <PackageReference
    Include="SecurityCodeScan.VS2019"
    Version="5.6.7"
    PrivateAssets="all"
    IncludeAssets="runtime; build; native; contentfiles; analyzers" />
</ItemGroup>
```

### 安裝方式 2：獨立工具（掃描現有專案不修改）
```bash
dotnet tool install --global security-scan
security-scan /path/to/solution.sln --export=sarif --output=scs-results.sarif
```

### 重要設定（`SecurityCodeScan.config.yml`）
```yaml
TaintEntryPoints:
  - Namespace: MyApp.Controllers
    ClassName: "*Controller"
    Name: "*"
    Method: true

PasswordValidators:
  - Namespace: Microsoft.AspNetCore.Identity
    ClassName: UserManager
    Name: CheckPasswordAsync
```

---

## P1：Gitleaks（密鑰洩漏掃描）

### 安裝
```bash
# Windows（Chocolatey）
choco install gitleaks

# Linux/macOS
brew install gitleaks

# Docker
docker pull zricethezav/gitleaks:latest
```

### 掃描
```bash
# 掃描整個 Git 歷史（含已刪除的提交）
gitleaks detect --source . --report-format json --report-path gitleaks-report.json

# 僅掃描工作目錄（不含歷史）
gitleaks detect --source . --no-git --report-format json --report-path gitleaks-report.json

# 掃描特定提交範圍
gitleaks detect --log-opts="HEAD~10..HEAD" --report-path gitleaks-report.json
```

---

## P2：OWASP Dependency Check

### 安裝
```bash
# 方式 1：NuGet 工具
dotnet tool install --global dotnet-dependency-check

# 方式 2：下載獨立版本
# https://github.com/jeremylong/DependencyCheck/releases
# 解壓後執行：
./dependency-check.sh --project "MyApp" --scan ./src --format JSON --out ./reports

# 方式 3：Docker
docker run --rm \
  -v "${PWD}:/src" \
  -v "${PWD}/reports:/report" \
  owasp/dependency-check:latest \
  --scan /src \
  --format "JSON" \
  --out /report
```

---

## P2：SonarScanner for .NET

### 前置需求
1. SonarQube Server（本機/雲端）或 SonarCloud 帳號
2. .NET SDK

### 安裝
```bash
dotnet tool install --global dotnet-sonarscanner
```

### 執行
```bash
# 開始分析
dotnet sonarscanner begin \
  /k:"MyProject" \
  /d:sonar.host.url="http://localhost:9000" \
  /d:sonar.token="YOUR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="**/coverage.xml"

# 建置（必要步驟）
dotnet build

# 結束分析並上傳
dotnet sonarscanner end /d:sonar.token="YOUR_TOKEN"
```

---

## P3：Snyk CLI

### 安裝
```bash
npm install -g snyk
snyk auth  # 需要 Snyk 帳號
```

### 掃描
```bash
# 相依性漏洞
snyk test --file=MyApp.sln

# 程式碼靜態分析（需 Snyk Code 授權）
snyk code test ./src

# 輸出 JSON 報告
snyk test --json > snyk-results.json
```

---

## Visual Studio / Rider IDE 整合

### Visual Studio 安裝 SecurityCodeScan
1. `Extensions` → `Manage Extensions`
2. 搜尋 `Security Code Scan`
3. 安裝重啟 VS

### .editorconfig 整合安全規則
```ini
# 在 .editorconfig 設定安全分析器嚴重程度
[*.cs]
dotnet_diagnostic.SCS0001.severity = error   # SQL Injection
dotnet_diagnostic.SCS0002.severity = error   # LDAP Injection
dotnet_diagnostic.SCS0005.severity = error   # Weak Random
dotnet_diagnostic.SCS0006.severity = error   # Weak Hashing
dotnet_diagnostic.SCS0007.severity = error   # XML Injection
dotnet_diagnostic.SCS0018.severity = error   # Path Traversal
dotnet_diagnostic.SCS0026.severity = error   # Cookie Without HttpOnly
dotnet_diagnostic.SCS0027.severity = error   # Open Redirect
```

---

## CI/CD 整合範例（GitHub Actions）

```yaml
name: SAST Pipeline

on: [push, pull_request]

jobs:
  sast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Gitleaks 需要完整歷史

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'

      - name: NuGet Vulnerability Scan
        run: dotnet list package --vulnerable --include-transitive

      - name: Run Semgrep
        uses: semgrep/semgrep-action@v1
        with:
          config: "p/csharp p/owasp-top-ten"

      - name: Run Gitleaks
        uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Upload SARIF results
        uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: semgrep.sarif
```
