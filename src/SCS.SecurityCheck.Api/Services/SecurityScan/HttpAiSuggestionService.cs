using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SCS.SecurityCheck.Api.Services.SecurityScan;

public sealed class HttpAiSuggestionService(HttpClient httpClient, ILogger<HttpAiSuggestionService> logger) : IAiSuggestionService
{
    public async Task<IReadOnlyList<string>> GetAdditionalSuggestionsAsync(
        ScanRequest request,
        ScanResult result,
        CancellationToken cancellationToken)
    {
        if (!request.EnableAiSuggestions || string.IsNullOrWhiteSpace(request.ApiKey) || string.IsNullOrWhiteSpace(request.AiProvider))
        {
            return Array.Empty<string>();
        }

        try
        {
            return request.AiProvider.Trim().ToLowerInvariant() switch
            {
                "openai" => await QueryOpenAiAsync(request.ApiKey, result, cancellationToken),
                "claude" => await QueryClaudeAsync(request.ApiKey, result, cancellationToken),
                _ => Array.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI suggestion call failed for provider {Provider}", request.AiProvider);
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<string>> QueryOpenAiAsync(string apiKey, ScanResult result, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model = "gpt-4.1-mini",
            messages = new object[]
            {
                new { role = "system", content = "你是資安專家，請只回傳繁體中文 JSON 陣列，每項是一條新增修補建議，不要重複既有建議。" },
                new { role = "user", content = BuildPrompt(result) }
            },
            temperature = 0.2
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return ParseSuggestionList(content);
    }

    private async Task<IReadOnlyList<string>> QueryClaudeAsync(string apiKey, ScanResult result, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = "claude-3-5-sonnet-latest",
            max_tokens = 800,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "user", content = BuildPrompt(result) }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<string>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();

        return ParseSuggestionList(text);
    }

    private static string BuildPrompt(ScanResult result)
    {
        var baseSuggestions = string.Join("\n", result.Findings.Select(f => $"- {f.Recommendation}").Distinct());
        return $"""
請依下列已知掃描結果，提供『不重複』的新增修補建議。
輸出格式必須是 JSON 字串陣列，例如：["建議A","建議B"]。

既有建議：
{baseSuggestions}

發現摘要：
- Critical: {result.Summary.Critical}
- High: {result.Summary.High}
- Medium: {result.Summary.Medium}
- Low: {result.Summary.Low}
""";
    }

    private static IReadOnlyList<string> ParseSuggestionList(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(content.Trim());
            return parsed?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToArray()
                   ?? Array.Empty<string>();
        }
        catch
        {
            return content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimStart('-', '*', '•').Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToArray();
        }
    }
}
