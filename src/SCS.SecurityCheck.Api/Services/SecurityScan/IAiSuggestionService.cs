namespace SCS.SecurityCheck.Api.Services.SecurityScan;

public interface IAiSuggestionService
{
    Task<IReadOnlyList<string>> GetAdditionalSuggestionsAsync(
        ScanRequest request,
        ScanResult result,
        CancellationToken cancellationToken);
}

public sealed class NoOpAiSuggestionService : IAiSuggestionService
{
    public Task<IReadOnlyList<string>> GetAdditionalSuggestionsAsync(
        ScanRequest request,
        ScanResult result,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
