namespace EffortHours.Change;

internal sealed record GitObjectReaderStatistics
{
    public long Requests { get; init; }

    public long CacheHits { get; init; }

    public long CacheEvictions { get; init; }

    public long UniqueObjects { get; init; }

    public long RequestedBytes { get; init; }

    public long CacheHitBytes { get; init; }

    public long ReadBytes { get; init; }

    public long UniqueObjectBytes { get; init; }

    public long RetainedCacheBytes { get; init; }

    public long PeakCachedBytes { get; init; }

    public TimeSpan ProcessCpuTime { get; init; }

    public TimeSpan ProcessOccupiedTime { get; init; }

    public TimeSpan ProcessWaitTime { get; init; }
}
