using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace SCS.SecurityCheck.Api.Services.SecurityScan;

public sealed class SecurityScannerService(
    IAiSuggestionService aiSuggestionService,
    IConfiguration configuration)
{
    private static readonly IReadOnlyList<RuleDefinition> Rules = BuildRules();

    public async Task<ScanResult> ScanAsync(
        ScanRequest request,
        CancellationToken cancellationToken,
        IProgress<ScanProgressInfo>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            throw new ScanValidationException("PROJECT_PATH_REQUIRED", "projectPath 為必填。\n");
        }

        var normalizedPath = Path.GetFullPath(request.ProjectPath);

        // 路徑白名單驗證：防止任意路徑讀取（CWE-22）
        var allowedBasePaths = configuration.GetSection("ScanAllowedPaths").Get<string[]>() ?? [];
        if (allowedBasePaths.Length > 0)
        {
            var isAllowed = allowedBasePaths.Any(basePath =>
                normalizedPath.StartsWith(
                    Path.GetFullPath(basePath),
                    StringComparison.OrdinalIgnoreCase));
            if (!isAllowed)
            {
                throw new ScanValidationException(
                    "PATH_NOT_ALLOWED",
                    "掃描路徑不在伺服器允許的範圍內，請聯絡管理員設定 ScanAllowedPaths。\n");
            }
        }

        if (!Directory.Exists(normalizedPath))
        {
            throw new ScanValidationException("PROJECT_PATH_NOT_FOUND", "指定的專案路徑不存在。\n");
        }

        var candidateFiles = Directory
            .EnumerateFiles(normalizedPath, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Take(request.MaxFiles)
            .ToArray();

        if (candidateFiles.Length == 0)
        {
            throw new ScanValidationException("NO_SOURCE_FILES", "在指定路徑下找不到可掃描的 C# 檔案。\n");
        }

        var findings = new List<ScanFinding>();
        var skippedFiles = new List<string>();

        var processedFiles = 0;
        foreach (var filePath in candidateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);
            processedFiles++;
            progress?.Report(new ScanProgressInfo(
                processedFiles,
                candidateFiles.Length,
                Path.GetRelativePath(normalizedPath, filePath)));

            if (fileInfo.Length > request.MaxFileSizeKb * 1024L)
            {
                skippedFiles.Add($"{filePath} (超過 {request.MaxFileSizeKb}KB)");
                continue;
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            foreach (var rule in Rules)
            {
                foreach (Match match in rule.Pattern.Matches(content))
                {
                    if (IsOnCommentOrTestLine(content, match.Index))
                        continue;

                    var line = CountLine(content, match.Index);
                    findings.Add(new ScanFinding(
                        rule.Code,
                        rule.Title,
                        rule.Severity,
                        Path.GetRelativePath(normalizedPath, filePath),
                        line,
                        ExtractEvidence(content, match.Index),
                        rule.Recommendation,
                        rule.CvssScore));
                }
            }
        }

        findings = findings
            .OrderBy(f => SeverityRank(f.Severity))
            .ThenBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line)
            .GroupBy(f => new { f.RuleCode, f.FilePath, f.Line })
            .Select(g => g.First())
            .ToList();

        var summary = new ScanSummary(
            findings.Count(f => f.Severity == "Critical"),
            findings.Count(f => f.Severity == "High"),
            findings.Count(f => f.Severity == "Medium"),
            findings.Count(f => f.Severity == "Low"),
            candidateFiles.Length,
            findings.Count);

        var initialResult = new ScanResult(summary, findings, string.Empty, Array.Empty<string>(), skippedFiles);

        IReadOnlyList<string> aiSuggestions = Array.Empty<string>();
        if (request.EnableAiSuggestions && !string.IsNullOrWhiteSpace(request.ApiKey))
        {
            var requested = await aiSuggestionService.GetAdditionalSuggestionsAsync(request, initialResult, cancellationToken);
            aiSuggestions = FilterAdditionalSuggestions(requested, findings.Select(f => f.Recommendation));
        }

        var markdown = BuildMarkdown(normalizedPath, summary, findings, aiSuggestions, skippedFiles);
        return initialResult with { MarkdownReport = markdown, AiAdditionalSuggestions = aiSuggestions };
    }

    private static IReadOnlyList<string> FilterAdditionalSuggestions(IEnumerable<string> aiSuggestions, IEnumerable<string> baseSuggestions)
    {
        var existing = baseSuggestions
            .Select(NormalizeSuggestion)
            .Where(text => text.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        return aiSuggestions
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Where(x => !existing.Contains(NormalizeSuggestion(x)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeSuggestion(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        return Regex.Replace(lowered, "[^\\p{L}\\p{N}]", string.Empty);
    }

    private static string BuildMarkdown(
        string rootPath,
        ScanSummary summary,
        IReadOnlyList<ScanFinding> findings,
        IReadOnlyList<string> aiSuggestions,
        IReadOnlyList<string> skippedFiles)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# C# 專案弱點掃描報告");
        builder.AppendLine();
        builder.AppendLine("## 掃描摘要");
        builder.AppendLine($"- 掃描時間: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"- 專案路徑: {rootPath}");
        builder.AppendLine($"- 掃描檔案數: {summary.TotalFilesScanned}");
        builder.AppendLine($"- 弱點總數: {summary.TotalFindings}");
        builder.AppendLine();

        builder.AppendLine("## 風險統計");
        builder.AppendLine($"- Critical: {summary.Critical}");
        builder.AppendLine($"- High: {summary.High}");
        builder.AppendLine($"- Medium: {summary.Medium}");
        builder.AppendLine($"- Low: {summary.Low}");
        builder.AppendLine();

        // 修復優先順序表（依 CVSS 評分由高到低）
        if (findings.Count > 0)
        {
            builder.AppendLine("## 修復優先順序表");
            builder.AppendLine();
            builder.AppendLine("| 優先序 | 規則碼 | 弱點名稱 | 嚴重度 | CVSS v3 | 發現次數 | 影響檔案數 |");
            builder.AppendLine("|--------|--------|----------|--------|---------|---------|-----------|");

            var priorityGroups = findings
                .GroupBy(f => new { f.RuleCode, f.Title, f.Severity, f.CvssScore })
                .OrderByDescending(g => g.Key.CvssScore)
                .ThenBy(g => SeverityRank(g.Key.Severity))
                .Select((g, idx) => new
                {
                    Rank = idx + 1,
                    g.Key.RuleCode,
                    g.Key.Title,
                    g.Key.Severity,
                    g.Key.CvssScore,
                    Count = g.Count(),
                    Files = g.Select(f => f.FilePath).Distinct().Count()
                });

            foreach (var row in priorityGroups)
            {
                var cvssLabel = row.CvssScore >= 9.0 ? $"**{row.CvssScore:F1}**" : $"{row.CvssScore:F1}";
                builder.AppendLine($"| {row.Rank} | {row.RuleCode} | {row.Title} | {row.Severity} | {cvssLabel} | {row.Count} | {row.Files} |");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## 重大弱點清單");
        if (findings.Count == 0)
        {
            builder.AppendLine("目前未偵測到符合規則的弱點。");
        }
        else
        {
            foreach (var finding in findings)
            {
                builder.AppendLine($"### [{finding.Severity}] {finding.Title} ({finding.RuleCode})");
                builder.AppendLine($"- **CVSS v3 Base Score**: {finding.CvssScore:F1}");
                builder.AppendLine($"- 檔案: {finding.FilePath}:{finding.Line}");
                builder.AppendLine($"- 證據: {finding.Evidence}");
                builder.AppendLine($"- 建議修復: {finding.Recommendation}");
                builder.AppendLine();
            }
        }

        if (aiSuggestions.Count > 0)
        {
            builder.AppendLine("## AI 新增建議");
            foreach (var suggestion in aiSuggestions)
            {
                builder.AppendLine($"- {suggestion}");
            }
            builder.AppendLine();
        }

        if (skippedFiles.Count > 0)
        {
            builder.AppendLine("## 附錄: 略過檔案");
            foreach (var skipped in skippedFiles)
            {
                builder.AppendLine($"- {skipped}");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 若比對位置落在單行注釋（// ...）或測試斷言（Assert.xxx）行上，則回傳 true 表示應略過，
    /// 以消除注釋與單元測試的假陽性。
    /// </summary>
    private static bool IsOnCommentOrTestLine(string content, int matchIndex)
    {
        // 找到該行的行首
        var lineStart = matchIndex;
        while (lineStart > 0 && content[lineStart - 1] != '\n')
            lineStart--;

        // 找到該行的行尾
        var lineEnd = matchIndex;
        while (lineEnd < content.Length && content[lineEnd] != '\n')
            lineEnd++;

        var fullLine = content[lineStart..lineEnd];
        var trimmed = fullLine.TrimStart();

        // 略過單行注釋 // ... 或 XML doc 注釋 * ...
        if (trimmed.StartsWith("//") || trimmed.StartsWith("*"))
            return true;

        // 略過單元測試斷言（Assert.Contains / Assert.Equal 等）
        if (trimmed.Contains("Assert.", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static int CountLine(string content, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string ExtractEvidence(string content, int index)
    {
        var start = Math.Max(0, index - 40);
        var length = Math.Min(120, content.Length - start);
        return content.Substring(start, length).Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        _ => 3
    };

    private static IReadOnlyList<RuleDefinition> BuildRules()
    {
        return new List<RuleDefinition>
        {
            new("SCS001", "可能的 SQL Injection", "High", new Regex("@?\"[^\"\\r\\n]*\\b(?:SELECT|INSERT|UPDATE|DELETE)\\b[^\"\\r\\n]*\"\\s*\\+|(?:FromSqlRaw|ExecuteSqlRaw|SqlRaw|SqlQuery)\\s*\\([^;)]*\\+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請使用參數化查詢或 EF Core ORM（LINQ），避免在字串常值中以 + 拼接 SQL；EF Core 的 .Add()/.Update()/.Remove() 等 LINQ 操作永遠使用參數化 SQL，不在此規則範圍內。", 9.8),
            new("SCS002", "硬編碼密碼或金鑰", "Critical", new Regex("((password|pwd|apikey|secret)\\s*[:=]\\s*\"[^\"]{4,}\")|(password\\s*=\\s*[^;\\\"\\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請改用環境變數保存密碼", 9.1),
            new("SCS003", "使用弱雜湊或弱加密", "High", new Regex("(MD5|SHA1|DESCryptoServiceProvider|RC2)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請改用 SHA-256 以上雜湊與現代加密演算法。", 7.5),
            new("SCS004", "不安全反序列化", "High", new Regex("BinaryFormatter|TypeNameHandling\\s*\\.\\s*All", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請避免不安全反序列化，並加入型別白名單。", 9.8),
            new("SCS005", "可能的路徑穿越", "Medium", new Regex("(ReadAllText|OpenRead|WriteAllText)\\s*\\([^\\)]*(Request\\.|Query\\.|Form\\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請先正規化路徑並限制在允許目錄內。", 7.5),
            new("SCS006", "可能的開放重新導向", "Medium", new Regex("Redirect\\s*\\([^\\)]*(Request\\.|Query\\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請限制重新導向目標為相對路徑或白名單。", 6.1),
            new("SCS007", "可能的 SSRF 風險", "High", new Regex("HttpClient\\s*\\.[^\\n\\r]*\\((Request\\.|Query\\.|Form\\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請限制可連線的 URL 與網段白名單。", 8.6),
            new("SCS008", "寬鬆 CORS 設定", "High", new Regex("AllowAnyOrigin\\s*\\(\\s*\\)\\s*\\.[^\\n\\r]*AllowCredentials|AllowCredentials\\s*\\(\\s*\\)\\s*\\.[^\\n\\r]*AllowAnyOrigin", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請改為明確網域白名單，不要同時允許任意來源與憑證。", 8.1),
            new("SCS009", "例外細節外洩", "Medium", new Regex("\\b(?:ex|exception|exc|err|error)\\b\\s*\\.\\s*(?:Message|StackTrace|ToString\\s*\\(\\s*\\)|InnerException)|\\.StackTrace\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "例外的 Message/StackTrace 不應直接回傳給 HTTP 用戶端，請改用通用錯誤訊息，細節僅寫入內部日誌（注意：一般型別的 .ToString()、Enum.ToString() 不屬此規則範圍）。", 5.3),
            new("SCS010", "不安全隨機數用途", "Medium", new Regex("new\\s+Random\\s*\\(\\s*\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "安全用途請改用 RandomNumberGenerator。", 5.9),
            new("SCS011", "可能的 XXE 風險", "High", new Regex("new\\s+XmlDocument\\s*\\(|XmlReaderSettings\\s*\\{[^}]*DtdProcessing\\s*=\\s*DtdProcessing\\.Parse", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請關閉 DTD 並明確設定 XmlResolver 為 null。", 9.1),
            new("SCS012", "可能的 XSS 風險 (Html.Raw)", "High", new Regex("Html\\.Raw\\s*\\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請避免直接輸出未編碼內容，改用預設 HTML 編碼輸出。", 7.2),
            new("SCS013", "可能的反射型 XSS", "High", new Regex("Response\\.Write\\s*\\([^\\)]*(Request\\.|Query\\.|Form\\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請先做輸入驗證與輸出編碼，避免直接回寫使用者輸入。", 7.3),
            new("SCS014", "可能的命令注入", "Critical", new Regex("Process\\.Start\\s*\\([^\\)]*(Request\\.|Query\\.|Form\\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請避免將使用者輸入直接帶入系統命令。", 10.0),
            new("SCS015", "不安全 TLS 設定", "High", new Regex("SecurityProtocol\\s*=\\s*[^;]*(Ssl3|Tls\\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請停用舊版 TLS/SSL，至少使用 TLS 1.2。", 7.4),
            new("SCS016", "憑證驗證被關閉", "Critical", new Regex("ServerCertificateValidationCallback\\s*\\+?=\\s*\\([^\\)]*\\)\\s*=>\\s*true", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請勿略過憑證驗證，應確實驗證伺服器憑證。", 9.8),
            new("SCS017", "JWT 驗證設定過寬", "High", new Regex("ValidateIssuer\\s*=\\s*false|ValidateAudience\\s*=\\s*false|ValidateLifetime\\s*=\\s*false|ValidateIssuerSigningKey\\s*=\\s*false", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請啟用完整 JWT 驗證，避免接受不可信 Token。", 8.1),
            new("SCS018", "高風險端點允許匿名", "Medium", new Regex("\\[AllowAnonymous\\][\\s\\S]{0,220}(Delete|Update|Admin|Manage)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請確認高風險操作不允許匿名存取。", 6.5),
            new("SCS019", "停用 CSRF 防護", "High", new Regex("IgnoreAntiforgeryToken|ValidateAntiForgeryToken\\s*\\(\\s*false\\s*\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請啟用 AntiForgery 保護並驗證跨站請求。", 8.8),
            new("SCS020", "日誌可能洩漏敏感資訊", "Medium", new Regex("Log(Information|Warning|Error|Debug|Trace)\\s*\\([^\\)]*(password|apikey|token|secret)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "記錄前請遮罩敏感欄位，避免密碼或金鑰進入日誌。", 5.3),
            new("SCS021", "可能的 LDAP Injection", "High", new Regex("Filter\\s*=\\s*[^\\n;]*\\+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請對 LDAP 搜尋過濾器使用參數化方式或跳脫特殊字元，避免用字串拼接。", 8.8),
            new("SCS022", "可能的 XPath Injection", "High", new Regex("(SelectNodes|SelectSingleNode)\\s*\\([^)]*\\+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "請使用 XPathExpression 搭配參數或事先驗證輸入，不要直接拼接使用者輸入。", 7.5),
            new("SCS023", "不安全 Cookie 設定", "Medium", new Regex("HttpOnly\\s*=\\s*false|\\.Secure\\s*=\\s*false", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Cookie 應設定 HttpOnly=true 與 Secure=true，以防止 XSS 竊取與中間人攻擊。", 6.5),
            new("SCS024", "URL 含敏感參數", "High", new Regex("[\"']https?://[^\"']*[?&](password|pwd|token|apikey|api_key|secret)=", RegexOptions.IgnoreCase | RegexOptions.Compiled), "敏感資料不應出現在 URL 中，請改用 POST body 或 Authorization header 傳遞。", 7.5),
            new("SCS025", "硬編碼 JWT 簽名金鑰", "Critical", new Regex("new\\s+SymmetricSecurityKey\\s*\\(\\s*Encoding\\.[A-Za-z0-9]+\\.GetBytes\\s*\\(\\s*\"[^\"]{8,}\"", RegexOptions.IgnoreCase | RegexOptions.Compiled), "JWT 簽名金鑰不應硬編碼在原始碼中，請改用環境變數或 Azure Key Vault 管理。", 9.8),
            new("SCS026", "開發例外頁面未限制環境", "Medium", new Regex("app\\.UseDeveloperExceptionPage\\s*\\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), "UseDeveloperExceptionPage 僅應在開發環境啟用，請包裹在 if (env.IsDevelopment()) 判斷中。", 5.3),
            new("SCS027", "可能的 ReDoS 正規表達式風險", "Medium", new Regex("new\\s+Regex\\s*\\(\\s*\"[^\"]*\\(\\.[+*]\\)[+*]", RegexOptions.Compiled), "此正規表達式可能存在 Catastrophic Backtracking，請使用原子群組或重寫以避免 DoS。", 5.9),
            new("SCS028", "動態程式碼執行", "High", new Regex("CSharpScript\\.(RunAsync|EvaluateAsync|Run)|ScriptEngine\\s*\\.", RegexOptions.IgnoreCase | RegexOptions.Compiled), "動態執行程式碼帶有高度安全風險，請確認輸入來源可信，並在沙箱環境執行。", 9.0),
            new("SCS029", "動態 Assembly 載入", "High", new Regex("Assembly\\.(LoadFrom|LoadFile)\\s*\\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), "動態載入 Assembly 可能被惡意利用，請驗證來源路徑並考慮使用允許清單。", 8.1),
            new("SCS030", "密碼政策強度過弱", "Medium", new Regex("RequiredLength\\s*=\\s*[1-5]\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "密碼最低長度建議至少 8 位，並同時要求數字、大小寫與特殊符號。", 5.3)
        };
    }
}
