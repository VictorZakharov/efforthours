using System.Security.Cryptography;
using System.Text;

namespace EffortHours.Analysis;

public interface IRepositoryAnalysisArtifactCacheProvider
{
    public RepositoryAnalysisArtifactCache? AnalysisArtifactCache { get; }
}

public sealed record RepositoryAnalysisArtifactCacheStatistics(
    int Requests,
    int Hits,
    int UniqueKeys,
    int RevisitMisses,
    int Evictions,
    int PeakEntries,
    int EntryLimit);

/// <summary>
/// Retains immutable, analyzer-versioned artifacts for one repository invocation.
/// Deterministic key-ranked admission makes retention independent of concurrent
/// completion order. Keys and values remain process-local and are never included
/// in reports.
/// </summary>
public sealed class RepositoryAnalysisArtifactCache
{
    public const int DefaultMaximumEntries = 8_192;

    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _retainedRanks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _admittedKeys = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly int _maximumEntries;
    private int _requests;
    private int _hits;
    private int _revisitMisses;
    private int _peakEntries;

    public RepositoryAnalysisArtifactCache(int maximumEntries = DefaultMaximumEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        _maximumEntries = maximumEntries;
    }

    public bool TryGet<T>(string key, out T value)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            _requests++;
            if (!_entries.TryGetValue(key, out CacheEntry? entry))
            {
                if (!_seenKeys.Add(key))
                {
                    _revisitMisses++;
                }

                value = null!;
                return false;
            }

            if (entry.Value is not T typed)
            {
                throw new InvalidOperationException(
                    "An immutable analysis-artifact cache key was reused for a different value type.");
            }

            _hits++;
            value = typed;
            return true;
        }
    }

    public void Add<T>(string key, T value)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            _seenKeys.Add(key);
            _admittedKeys.Add(key);
            if (_entries.TryGetValue(key, out CacheEntry? existing))
            {
                _entries[key] = existing with { Value = value };
                return;
            }

            string rank = Rank(key);
            if (_entries.Count >= _maximumEntries)
            {
                string largestRetainedRank = _retainedRanks.Max!;
                if (StringComparer.Ordinal.Compare(rank, largestRetainedRank) >= 0)
                {
                    return;
                }

                _entries.Remove(KeyFromRank(largestRetainedRank));
                _retainedRanks.Remove(largestRetainedRank);
            }

            _entries.Add(key, new CacheEntry(value, rank));
            _retainedRanks.Add(rank);
            _peakEntries = Math.Max(_peakEntries, _entries.Count);
        }
    }

    public RepositoryAnalysisArtifactCacheStatistics GetStatistics()
    {
        lock (_gate)
        {
            return new RepositoryAnalysisArtifactCacheStatistics(
                _requests,
                _hits,
                _seenKeys.Count,
                _revisitMisses,
                Math.Max(0, _admittedKeys.Count - _entries.Count),
                _peakEntries,
                _maximumEntries);
        }
    }

    private static string Rank(string key)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        return $"{digest}\0{key}";
    }

    private static string KeyFromRank(string rank) => rank[65..];

    private sealed record CacheEntry(object Value, string Rank);
}
