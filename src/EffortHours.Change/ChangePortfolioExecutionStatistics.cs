using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioExecutionStatistics
{
    public int RepositoryCount { get; init; }

    public int MaximumActiveRepositories { get; init; }

    public int MaximumConcurrentFileAnalysesPerRepository { get; init; }

    public int SnapshotAnalysisRequests { get; init; }

    public int SnapshotAnalysisHits { get; init; }

    public int UniqueSnapshotAnalysisKeys { get; init; }

    public int SnapshotAnalysisRevisitMisses { get; init; }

    public int AnalysisArtifactRequests { get; init; }

    public int AnalysisArtifactHits { get; init; }

    public int UniqueAnalysisArtifactKeys { get; init; }

    public int AnalysisArtifactRevisitMisses { get; init; }

    public int AnalysisArtifactEvictions { get; init; }

    public int PeakRetainedAnalysisArtifacts { get; init; }

    public int SnapshotAnalysisEvictions { get; init; }

    public int PeakRetainedSnapshotAnalyses { get; init; }

    public int SnapshotInventoryRequests { get; init; }

    public int SnapshotInventoryHits { get; init; }

    public int UniqueSnapshotInventoryObjects { get; init; }

    public int SnapshotInventoryRevisitMisses { get; init; }

    public int FullSnapshotInventoryLoads { get; init; }

    public int IncrementalSnapshotInventoryLoads { get; init; }

    public int BatchedIncrementalSnapshotInventoryLoads { get; init; }

    public int SnapshotInventoryEvictions { get; init; }

    public int PeakRetainedSnapshotInventories { get; init; }

    public int ObjectDatabaseReaders { get; init; }

    public long BlobRequests { get; init; }

    public long BlobCacheHits { get; init; }

    public long BlobCacheEvictions { get; init; }

    public long UniqueBlobObjects { get; init; }

    public long BlobRequestedBytes { get; init; }

    public long BlobCacheHitBytes { get; init; }

    public long BlobReadBytes { get; init; }

    public long UniqueBlobBytes { get; init; }

    public long RetainedBlobBytesPerRepository { get; init; }

    public long PeakCachedBlobBytesPerRepository { get; init; }

    public int SnapshotAnalysisRetentionLimit { get; init; }

    public int AnalysisArtifactRetentionLimit { get; init; }

    public int SnapshotInventoryRetentionLimit { get; init; }

    public int SnapshotDeltaBatchOutputByteLimit { get; init; }

    public long BlobCacheByteLimitPerRepository { get; init; }

    public Diagnostic CreateDiagnostic() => new()
    {
        Code = "FB5325",
        Severity = DiagnosticSeverity.Information,
        Message = $"Repository-scoped portfolio reuse served {SnapshotAnalysisHits} of " +
            $"{SnapshotAnalysisRequests} snapshot-analysis request(s) across " +
            $"{UniqueSnapshotAnalysisKeys} unique key(s), with " +
            $"{SnapshotAnalysisRevisitMisses} revisit miss(es); served {AnalysisArtifactHits} of " +
            $"{AnalysisArtifactRequests} immutable file-analysis artifact request(s) across " +
            $"{UniqueAnalysisArtifactKeys} unique key(s), with " +
            $"{AnalysisArtifactRevisitMisses} revisit miss(es); served {SnapshotInventoryHits} of " +
            $"{SnapshotInventoryRequests} immutable-inventory request(s) across " +
            $"{UniqueSnapshotInventoryObjects} unique object(s), with " +
            $"{SnapshotInventoryRevisitMisses} revisit miss(es); built " +
            $"{FullSnapshotInventoryLoads} full and {IncrementalSnapshotInventoryLoads} incremental " +
            $"inventories, including {BatchedIncrementalSnapshotInventoryLoads} from repository-level " +
            $"batch diffs; evicted {SnapshotAnalysisEvictions} analysis entries and " +
            $"{SnapshotInventoryEvictions} inventory entries; " +
            $"{ObjectDatabaseReaders} Git object reader(s) served {BlobRequests} blob request(s) over " +
            $"{UniqueBlobObjects} unique object(s) with {BlobCacheHits} cache hit(s), " +
            $"{BlobCacheEvictions} eviction(s), and {BlobReadBytes} byte(s) read from Git. At most " +
            $"{MaximumActiveRepositories} repository session(s) were active concurrently, " +
            $"with at most {MaximumConcurrentFileAnalysesPerRepository} independent file " +
            "analyses per repository and retention limits of " +
            $"{SnapshotAnalysisRetentionLimit} analyses, " +
            $"{AnalysisArtifactRetentionLimit} file-analysis artifacts, " +
            $"{SnapshotInventoryRetentionLimit} inventories, and " +
            $"{BlobCacheByteLimitPerRepository / 1024 / 1024} MiB of blobs per repository; " +
            $"repository batch output is capped at " +
            $"{SnapshotDeltaBatchOutputByteLimit / 1024 / 1024} MiB.",
    };
}

public sealed record ChangePortfolioEstimateBatch
{
    public IReadOnlyList<ChangeEstimateReport> Reports { get; init; } = [];

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }
}
