namespace EffortHours.Change;

internal sealed record GitSnapshotSessionStatistics
{
    public int AnalysisArtifactRequests { get; init; }

    public int AnalysisArtifactHits { get; init; }

    public int UniqueAnalysisArtifactKeys { get; init; }

    public int AnalysisArtifactRevisitMisses { get; init; }

    public int AnalysisArtifactEvictions { get; init; }

    public int PeakRetainedAnalysisArtifacts { get; init; }

    public int InventoryRequests { get; init; }

    public int InventoryHits { get; init; }

    public int UniqueInventoryObjects { get; init; }

    public int InventoryRevisitMisses { get; init; }

    public int FullInventoryLoads { get; init; }

    public int IncrementalInventoryLoads { get; init; }

    public int BatchedIncrementalInventoryLoads { get; init; }

    public int InventoryEvictions { get; init; }

    public int PeakRetainedInventories { get; init; }

    public int PeakRetainedInventoryRoots { get; init; }

    public int ObjectReaderStarts { get; init; }

    public int ObjectMetadataReaderStarts { get; init; }

    public int ObjectMetadataRequests { get; init; }

    public int ObjectMetadataCacheHits { get; init; }

    public int UniqueObjectMetadataObjects { get; init; }

    public int ObjectMetadataCacheEvictions { get; init; }

    public int PeakCachedObjectMetadataLengths { get; init; }

    public long BlobRequests { get; init; }

    public long BlobCacheHits { get; init; }

    public long BlobCacheEvictions { get; init; }

    public long UniqueBlobObjects { get; init; }

    public long BlobRequestedBytes { get; init; }

    public long BlobCacheHitBytes { get; init; }

    public long BlobReadBytes { get; init; }

    public long UniqueBlobBytes { get; init; }

    public long RetainedBlobBytes { get; init; }

    public long PeakCachedBlobBytes { get; init; }

    public TimeSpan ObjectReaderCpuTime { get; init; }

    public TimeSpan ObjectReaderOccupiedTime { get; init; }

    public TimeSpan ObjectReaderWaitTime { get; init; }
}
