using System.Collections.Concurrent;

namespace SCS.SecurityCheck.Api.Services.SecurityScan;

public sealed class ScanJobManager(SecurityScannerService scanner, ILogger<ScanJobManager> logger)
{
    private const int MaxConcurrentJobs = 3;
    private const int MaxStoredJobs = 200;
    private static readonly TimeSpan CompletedJobRetention = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, ScanJobState> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public ScanJobStartResponse Start(ScanRequest request)
    {
        var runningJobs = _jobs.Values.Count(x => x.Status is "queued" or "running");
        if (runningJobs >= MaxConcurrentJobs)
        {
            throw new InvalidOperationException("CONCURRENT_SCAN_LIMIT_REACHED");
        }

        TrimCompletedJobsIfNeeded();

        var scanId = Guid.NewGuid().ToString("N");
        var accessKey = Guid.NewGuid().ToString("N");
        var state = new ScanJobState(scanId, request);
        state.SetAccessKey(accessKey);
        _jobs[scanId] = state;

        _ = Task.Run(async () =>
        {
            state.UpdateStatus("running");
            var progress = new Progress<ScanProgressInfo>(p => state.UpdateProgress(p));

            try
            {
                var result = await scanner.ScanAsync(request, state.TokenSource.Token, progress);
                state.Complete(result);
                ScheduleCleanup(scanId);
            }
            catch (OperationCanceledException)
            {
                state.Cancel();
                ScheduleCleanup(scanId);
            }
            catch (ScanValidationException ex)
            {
                state.Fail($"{ex.Code}: {ex.Message.Trim()}");
                ScheduleCleanup(scanId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scan job {ScanId} failed.", scanId);
                state.Fail("發生未預期錯誤，請檢查輸入路徑或稍後重試。");
                ScheduleCleanup(scanId);
            }
        });

        return new ScanJobStartResponse(scanId, "queued", accessKey);
    }

    public ScanJobStatusResponse? Get(string scanId, string accessKey)
    {
        if (!_jobs.TryGetValue(scanId, out var state))
        {
            return null;
        }

        if (!state.IsAccessAllowed(accessKey))
        {
            throw new UnauthorizedAccessException("SCAN_ACCESS_DENIED");
        }

        return state.ToResponse();
    }

    public bool Cancel(string scanId, string accessKey)
    {
        if (!_jobs.TryGetValue(scanId, out var state))
        {
            return false;
        }

        if (!state.IsAccessAllowed(accessKey))
        {
            throw new UnauthorizedAccessException("SCAN_ACCESS_DENIED");
        }

        state.TokenSource.Cancel();
        return true;
    }

    private void ScheduleCleanup(string scanId)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(CompletedJobRetention);
            if (_jobs.TryRemove(scanId, out var state))
            {
                state.Dispose();
            }
        });
    }

    private void TrimCompletedJobsIfNeeded()
    {
        if (_jobs.Count < MaxStoredJobs)
        {
            return;
        }

        var removable = _jobs
            .Where(x => x.Value.Status is "completed" or "failed" or "cancelled")
            .OrderBy(x => x.Value.LastUpdatedUtc)
            .Take(Math.Max(1, _jobs.Count - MaxStoredJobs + 1))
            .Select(x => x.Key)
            .ToArray();

        foreach (var key in removable)
        {
            if (_jobs.TryRemove(key, out var state))
            {
                state.Dispose();
            }
        }
    }

    private sealed class ScanJobState(string scanId, ScanRequest request) : IDisposable
    {
        private readonly object _sync = new();

        public CancellationTokenSource TokenSource { get; } = new();
        public string ScanId { get; } = scanId;
        public ScanRequest Request { get; } = request;
        public string AccessKey { get; private set; } = string.Empty;
        public string Status { get; private set; } = "queued";
        public DateTimeOffset LastUpdatedUtc { get; private set; } = DateTimeOffset.UtcNow;
        public int ProcessedFiles { get; private set; }
        public int TotalFiles { get; private set; }
        public string? CurrentFile { get; private set; }
        public string? ErrorMessage { get; private set; }
        public ScanResult? Result { get; private set; }

        public void SetAccessKey(string key)
        {
            lock (_sync)
            {
                AccessKey = key;
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public bool IsAccessAllowed(string key)
        {
            lock (_sync)
            {
                return !string.IsNullOrWhiteSpace(key) && string.Equals(AccessKey, key, StringComparison.Ordinal);
            }
        }

        public void UpdateStatus(string status)
        {
            lock (_sync)
            {
                Status = status;
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void UpdateProgress(ScanProgressInfo progress)
        {
            lock (_sync)
            {
                ProcessedFiles = progress.ProcessedFiles;
                TotalFiles = progress.TotalFiles;
                CurrentFile = progress.CurrentFile;
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void Complete(ScanResult result)
        {
            lock (_sync)
            {
                Result = result;
                Status = "completed";
                ProcessedFiles = TotalFiles;
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void Cancel()
        {
            lock (_sync)
            {
                Status = "cancelled";
                ErrorMessage = "掃描已取消。";
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public void Fail(string errorMessage)
        {
            lock (_sync)
            {
                Status = "failed";
                ErrorMessage = errorMessage;
                LastUpdatedUtc = DateTimeOffset.UtcNow;
            }
        }

        public ScanJobStatusResponse ToResponse()
        {
            lock (_sync)
            {
                return new ScanJobStatusResponse(
                    ScanId,
                    Status,
                    ProcessedFiles,
                    TotalFiles,
                    CurrentFile,
                    ErrorMessage,
                    Result);
            }
        }

            public void Dispose()
            {
                lock (_sync)
                {
                    if (!TokenSource.IsCancellationRequested)
                    {
                        TokenSource.Cancel();
                    }
                    TokenSource.Dispose();
                }
            }
    }
}
