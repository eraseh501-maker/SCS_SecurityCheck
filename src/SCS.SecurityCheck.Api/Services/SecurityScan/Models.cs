using System.Text.RegularExpressions;

namespace SCS.SecurityCheck.Api.Services.SecurityScan;

public sealed record ScanRequest(
    string ProjectPath,
    bool EnableAiSuggestions = false,
    string? AiProvider = null,
    string? ApiKey = null,
    int MaxFiles = 2000,
    int MaxFileSizeKb = 512);

public sealed record ScanFinding(
    string RuleCode,
    string Title,
    string Severity,
    string FilePath,
    int Line,
    string Evidence,
    string Recommendation,
    double CvssScore);

public sealed record ScanSummary(int Critical, int High, int Medium, int Low, int TotalFilesScanned, int TotalFindings);

public sealed record ScanProgressInfo(int ProcessedFiles, int TotalFiles, string? CurrentFile);

public sealed record ScanResult(
    ScanSummary Summary,
    IReadOnlyList<ScanFinding> Findings,
    string MarkdownReport,
    IReadOnlyList<string> AiAdditionalSuggestions,
    IReadOnlyList<string> SkippedFiles);

public sealed record ScanJobStartResponse(string ScanId, string Status, string AccessKey);

public sealed record ScanJobStatusResponse(
    string ScanId,
    string Status,
    int ProcessedFiles,
    int TotalFiles,
    string? CurrentFile,
    string? ErrorMessage,
    ScanResult? Result);

public sealed class ScanValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal sealed record RuleDefinition(
    string Code,
    string Title,
    string Severity,
    Regex Pattern,
    string Recommendation,
    double CvssScore);
