using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioExecutionStatistics
{
    public int RepositoryCount { get; init; }

    public int MaximumActiveRepositories { get; init; }

    public int SnapshotAnalysisRequests { get; init; }

    public int SnapshotAnalysisHits { get; init; }

    public int SnapshotAnalysisEvictions { get; init; }

    public int PeakRetainedSnapshotAnalyses { get; init; }

    public int SnapshotInventoryRequests { get; init; }

    public int SnapshotInventoryHits { get; init; }

    public int FullSnapshotInventoryLoads { get; init; }

    public int IncrementalSnapshotInventoryLoads { get; init; }

    public int SnapshotInventoryEvictions { get; init; }

    public int PeakRetainedSnapshotInventories { get; init; }

    public int ObjectDatabaseReaders { get; init; }

    public long BlobRequests { get; init; }

    public long BlobCacheHits { get; init; }

    public long BlobCacheEvictions { get; init; }

    public long PeakCachedBlobBytesPerRepository { get; init; }

    public int SnapshotAnalysisRetentionLimit { get; init; }

    public int SnapshotInventoryRetentionLimit { get; init; }

    public long BlobCacheByteLimitPerRepository { get; init; }

    public Diagnostic CreateDiagnostic() => new()
    {
        Code = "FB5325",
        Severity = DiagnosticSeverity.Information,
        Message = $"Repository-scoped portfolio reuse served {SnapshotAnalysisHits} of " +
            $"{SnapshotAnalysisRequests} snapshot-analysis request(s) and {SnapshotInventoryHits} of " +
            $"{SnapshotInventoryRequests} immutable-inventory request(s) from bounded caches; built " +
            $"{FullSnapshotInventoryLoads} full and {IncrementalSnapshotInventoryLoads} incremental " +
            $"inventories; evicted {SnapshotAnalysisEvictions} analysis entries and " +
            $"{SnapshotInventoryEvictions} inventory entries; " +
            $"{ObjectDatabaseReaders} Git object reader(s) served {BlobRequests} blob request(s) with " +
            $"{BlobCacheHits} cache hit(s) and {BlobCacheEvictions} eviction(s). At most " +
            $"{MaximumActiveRepositories} repository was active, " +
            $"with retention limits of {SnapshotAnalysisRetentionLimit} analyses, " +
            $"{SnapshotInventoryRetentionLimit} inventories, and " +
            $"{BlobCacheByteLimitPerRepository / 1024 / 1024} MiB of blobs per repository.",
    };
}

public sealed record ChangePortfolioEstimateBatch
{
    public IReadOnlyList<ChangeEstimateReport> Reports { get; init; } = [];

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }
}
