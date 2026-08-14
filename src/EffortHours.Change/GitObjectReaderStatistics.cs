namespace EffortHours.Change;

internal sealed record GitObjectReaderStatistics
{
    public long Requests { get; init; }

    public long CacheHits { get; init; }

    public long CacheEvictions { get; init; }

    public long PeakCachedBytes { get; init; }
}
