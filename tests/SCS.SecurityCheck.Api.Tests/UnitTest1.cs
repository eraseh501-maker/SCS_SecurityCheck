using SCS.SecurityCheck.Api.Services.SecurityScan;
using Microsoft.Extensions.Configuration;

namespace SCS.SecurityCheck.Api.Tests;

public sealed class SecurityScannerServiceTests
{
    [Fact]
    public async Task ScanAsync_ProjectPathNotFound_ThrowsValidationException()
    {
        var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));

        var ex = await Assert.ThrowsAsync<ScanValidationException>(() =>
            sut.ScanAsync(new ScanRequest("D:\\this-path-should-not-exist-123456"), CancellationToken.None));

        Assert.Equal("PROJECT_PATH_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task ScanAsync_DetectsSqlInjectionAndBuildsTraditionalChineseMarkdown()
    {
        var root = CreateTempProjectWithFile("Program.cs", """
using Microsoft.Data.SqlClient;

var userInput = "abc";
var sql = "SELECT * FROM Users WHERE Name='" + userInput + "'";
Console.WriteLine(sql);
""");

        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));

            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);

            Assert.Contains(result.Findings, f => f.RuleCode == "SCS001");
            Assert.Contains("掃描摘要", result.MarkdownReport);
            Assert.Contains("重大弱點清單", result.MarkdownReport);
            Assert.DoesNotContain("AI 新增建議", result.MarkdownReport);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_WithApiKey_KeepsOnlyAdditionalAiSuggestions()
    {
        var root = CreateTempProjectWithFile("Vuln.cs", """
var conn = "Server=.;Database=App;User Id=sa;Password=123456;";
Console.WriteLine(conn);
""");

        try
        {
            var aiSuggestions = new[]
            {
                "請改用環境變數保存密碼",
                "建議建立統一的密鑰輪替政策"
            };
            var sut = CreateScanner(new StaticAiSuggestionService(aiSuggestions));

            var result = await sut.ScanAsync(
                new ScanRequest(root, true, "openai", "dummy-key"),
                CancellationToken.None);

            Assert.Single(result.AiAdditionalSuggestions);
            Assert.Equal("建議建立統一的密鑰輪替政策", result.AiAdditionalSuggestions[0]);
            Assert.Contains("AI 新增建議", result.MarkdownReport);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsXxeRule_WhenXmlDocumentIsUsed()
    {
        var root = CreateTempProjectWithFile("XmlRisk.cs", """
using System.Xml;

var doc = new XmlDocument();
doc.LoadXml("<root />");
""");

        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS011");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_ReportsProgress_ForEachFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scs-scan-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "A.cs"), "var a = 1;");
        File.WriteAllText(Path.Combine(root, "B.cs"), "var b = 2;");

        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var snapshots = new List<ScanProgressInfo>();
            var progress = new Progress<ScanProgressInfo>(p => snapshots.Add(p));

            await sut.ScanAsync(new ScanRequest(root), CancellationToken.None, progress);

            Assert.NotEmpty(snapshots);
            Assert.Equal(2, snapshots[^1].TotalFiles);
            Assert.Equal(2, snapshots[^1].ProcessedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DetectsLdapInjection_SCS021()
    {
        var root = CreateTempProjectWithFile("LdapVuln.cs", """
var searcher = new DirectorySearcher();
searcher.Filter = "(&(uid=" + userInput + "))";
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS021");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsXpathInjection_SCS022()
    {
        var root = CreateTempProjectWithFile("XPathVuln.cs", """
var nodes = doc.SelectNodes("/users/user[name='" + userName + "']");
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS022");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsInsecureCookie_SCS023()
    {
        var root = CreateTempProjectWithFile("CookieVuln.cs", """
var opts = new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Lax };
Response.Cookies.Append("session", token, opts);
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS023");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsSensitiveDataInUrl_SCS024()
    {
        var root = CreateTempProjectWithFile("UrlVuln.cs", """
var url = "https://api.example.com/reset?token=abc123&password=P@ssw0rd";
httpClient.GetAsync(url);
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS024");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsHardcodedJwtKey_SCS025()
    {
        var root = CreateTempProjectWithFile("JwtVuln.cs", """
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("my-super-secret-key-1234"));
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS025");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsDeveloperExceptionPage_SCS026()
    {
        var root = CreateTempProjectWithFile("Program.cs", """
var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseRouting();
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS026");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsRedosPattern_SCS027()
    {
        var root = CreateTempProjectWithFile("RegexVuln.cs", """
var re = new Regex("(.+)+end");
var isMatch = re.IsMatch(input);
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS027");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsDynamicCodeExecution_SCS028()
    {
        var root = CreateTempProjectWithFile("ScriptVuln.cs", """
var result = await CSharpScript.RunAsync(userCode);
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS028");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_DetectsDynamicAssemblyLoad_SCS029()
    {
        var root = CreateTempProjectWithFile("PluginLoader.cs", """
var asm = Assembly.LoadFrom(pluginPath);
var type = asm.GetType("Plugin.EntryPoint");
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS029");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_WeakPasswordPolicy_SCS030()
    {
        var root = CreateTempProjectWithFile("IdentityConfig.cs", """
services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 4;
    options.Password.RequireDigit = false;
});
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS030");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_FindingHasCvssScore_GreaterThanZero()
    {
        var root = CreateTempProjectWithFile("SqlVuln.cs", """
var sql = "SELECT * FROM Users WHERE Id='" + userId + "'";
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains(result.Findings, f => f.RuleCode == "SCS001" && f.CvssScore > 0);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScanAsync_ReportContainsPriorityTable_WhenFindingsExist()
    {
        var root = CreateTempProjectWithFile("MultiVuln.cs", """
var sql = "SELECT * FROM T WHERE Name='" + name + "'";
var r = new Random();
""");
        try
        {
            var sut = CreateScanner(new StaticAiSuggestionService(Array.Empty<string>()));
            var result = await sut.ScanAsync(new ScanRequest(root), CancellationToken.None);
            Assert.Contains("修復優先順序表", result.MarkdownReport);
            Assert.Contains("CVSS v3", result.MarkdownReport);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateTempProjectWithFile(string fileName, string content)
    {
        var root = Path.Combine(Path.GetTempPath(), $"scs-scan-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, fileName), content);
        return root;
    }

    private static SecurityScannerService CreateScanner(IAiSuggestionService aiSuggestionService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        return new SecurityScannerService(aiSuggestionService, configuration);
    }

    private sealed class StaticAiSuggestionService(string[] suggestions) : IAiSuggestionService
    {
        public Task<IReadOnlyList<string>> GetAdditionalSuggestionsAsync(
            ScanRequest request,
            ScanResult result,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(suggestions);
        }
    }
}
