using System.Collections.Concurrent;

namespace EffortHours.Change;

internal sealed partial class ProviderQueryCounters
{
    private long _historicalAcquiredBytes;
    private int _historicalAcquiredObjects;
    private readonly ConcurrentDictionary<(string Repository, string Head), byte> _historicalAcquiredHeads = new();
    public long HistoricalAcquiredBytes => Interlocked.Read(ref _historicalAcquiredBytes);
    public int HistoricalAcquiredObjects => Volatile.Read(ref _historicalAcquiredObjects);
    public bool WasHistoricalHeadAcquired(string repository, string head) => _historicalAcquiredHeads.ContainsKey((repository, head));
    public void AddHistoricalAcquisition(string repository, string head, RepositoryAcquisitionResult acquisition)
    {
        Interlocked.Add(ref _historicalAcquiredBytes, acquisition.AcquiredBytes);
        Interlocked.Add(ref _historicalAcquiredObjects, acquisition.AcquiredObjectCount);
        if (acquisition.AcquiredHeadCount > 0) _historicalAcquiredHeads.TryAdd((repository, head), 0);
    }
}
