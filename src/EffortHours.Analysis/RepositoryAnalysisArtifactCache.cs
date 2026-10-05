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
    int EntryLimit)
{
    public RepositoryArtifactWorkingSetStatistics? WorkingSet { get; init; }
}

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
    private readonly Dictionary<string, InflightEntry> _inflight = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly int _maximumEntries;
    private readonly IRepositoryAnalysisArtifactStore? _store;
    private readonly RepositoryArtifactWorkingSet? _workingSet;
    private int _requests;
    private int _hits;
    private int _revisitMisses;
    private int _peakEntries;

    public RepositoryAnalysisArtifactCache(int maximumEntries = DefaultMaximumEntries,
        IRepositoryAnalysisArtifactStore? store = null,
        RepositoryArtifactWorkingSet? workingSet = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        _maximumEntries = maximumEntries;
        _store = store;
        _workingSet = workingSet;
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
                if (_store is not null && _store.TryLoad(key, out T persisted))
                {
                    Add(key, persisted);
                    _hits++;
                    value = persisted;
                    return true;
                }
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

    // A failed probe has no telemetry/admission side effects. Cold requests still
    // use the ordinary single-flight path; completed hits need no request/task wrapper.
    public bool TryGetCompleted<T>(string key, string? workingSetSlot, out T value) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            if (workingSetSlot is not null && _workingSet is not null &&
                _workingSet.TryGet(workingSetSlot, key, out T current))
            {
                _requests++;
                _hits++;
                value = current;
                return true;
            }
            if (_entries.TryGetValue(key, out CacheEntry? entry))
            {
                if (entry.Value is not T typed) throw new InvalidOperationException("Artifact type mismatch.");
                _requests++;
                _hits++;
                value = typed;
                return true;
            }
            value = null!;
            return false;
        }
    }

    public Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        string? workingSetSlot = null,
        Func<T, bool>? shouldRetain = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (TryGetCompleted(key, workingSetSlot, out T cached)) return Task.FromResult(cached);
        RepositoryAnalysisArtifactRequest<T> request = Request<T>(key, workingSetSlot);
        if (!request.IsOwner)
        {
            return request.Result.WaitAsync(cancellationToken);
        }

        return CreateOwnedAsync(key, factory, shouldRetain, request, cancellationToken);
    }

    private async Task<T> CreateOwnedAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        Func<T, bool>? shouldRetain,
        RepositoryAnalysisArtifactRequest<T> request, CancellationToken cancellationToken) where T : class
    {
        try
        {
            T value = await factory(cancellationToken).ConfigureAwait(false);
            if (shouldRetain is null || shouldRetain(value)) Add(key, value);
            request.Complete(value);
            return value;
        }
        catch (Exception exception)
        {
            request.Fail(exception);
            throw;
        }
    }

    public RepositoryAnalysisArtifactRequest<T> Request<T>(string key, string? workingSetSlot = null)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            _requests++;
            if (workingSetSlot is not null && _workingSet is not null &&
                _workingSet.TryGet(workingSetSlot, key, out T current))
            {
                _hits++;
                return RepositoryAnalysisArtifactRequest<T>.Cached(current);
            }
            if (_entries.TryGetValue(key, out CacheEntry? cached))
            {
                if (cached.Value is not T typed)
                {
                    throw new InvalidOperationException(
                        "An immutable analysis-artifact cache key was reused for a different value type.");
                }

                _hits++;
                return RepositoryAnalysisArtifactRequest<T>.Cached(typed);
            }

            if (_inflight.TryGetValue(key, out InflightEntry? pending))
            {
                if (pending.ValueType != typeof(T))
                {
                    throw new InvalidOperationException(
                        "An in-flight analysis-artifact cache key was reused for a different value type.");
                }

                _hits++;
                return new RepositoryAnalysisArtifactRequest<T>(this, key, pending, isOwner: false);
            }

            if (_store is not null && _store.TryLoad(key, out T persisted))
            {
                Add(key, persisted);
                _hits++;
                return RepositoryAnalysisArtifactRequest<T>.Cached(persisted);
            }

            if (!_seenKeys.Add(key))
            {
                _revisitMisses++;
            }

            InflightEntry created = new(
                typeof(T),
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously),
                workingSetSlot);
            _inflight.Add(key, created);
            return new RepositoryAnalysisArtifactRequest<T>(this, key, created, isOwner: true);
        }
    }

    public void Add<T>(string key, T value)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        string rank = Rank(key);
        _store?.Save(key, value);
        lock (_gate)
        {
            if (_inflight.TryGetValue(key, out InflightEntry? pending) &&
                pending.WorkingSetSlot is not null)
                _workingSet?.Add(pending.WorkingSetSlot, key, value);
            _seenKeys.Add(key);
            _admittedKeys.Add(key);
            if (_entries.TryGetValue(key, out CacheEntry? existing))
            {
                _entries[key] = existing with { Value = value };
                return;
            }

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
                _maximumEntries)
            { WorkingSet = _workingSet?.GetStatistics() };
        }
    }

    private static string Rank(string key)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToLowerInvariant();
        return $"{digest}\0{key}";
    }

    private static string KeyFromRank(string rank) => rank[65..];

    private void CompleteInflight<T>(
        string key,
        InflightEntry operation,
        T value)
        where T : class
    {
        lock (_gate)
        {
            if (_inflight.TryGetValue(key, out InflightEntry? current) &&
                ReferenceEquals(current, operation))
            {
                _inflight.Remove(key);
            }
        }

        operation.Completion.TrySetResult(value);
    }

    private void FailInflight(
        string key,
        InflightEntry operation,
        Exception exception)
    {
        lock (_gate)
        {
            if (_inflight.TryGetValue(key, out InflightEntry? current) &&
                ReferenceEquals(current, operation))
            {
                _inflight.Remove(key);
            }
        }

        operation.Completion.TrySetException(exception);
        _ = operation.Completion.Task.Exception;
    }

    private sealed record CacheEntry(object Value, string Rank);

    internal sealed record InflightEntry(
        Type ValueType,
        TaskCompletionSource<object> Completion,
        string? WorkingSetSlot);

    public sealed class RepositoryAnalysisArtifactRequest<T>
        where T : class
    {
        private readonly RepositoryAnalysisArtifactCache? _cache;
        private readonly string? _key;
        private readonly InflightEntry? _operation;
        private readonly Task<T> _result;

        internal RepositoryAnalysisArtifactRequest(
            RepositoryAnalysisArtifactCache cache,
            string key,
            InflightEntry operation,
            bool isOwner)
        {
            _cache = cache;
            _key = key;
            _operation = operation;
            IsOwner = isOwner;
            _result = AwaitAsync(operation.Completion.Task);
        }

        private RepositoryAnalysisArtifactRequest(T value)
        {
            _result = Task.FromResult(value);
        }

        public bool IsOwner { get; }

        public Task<T> Result => _result;

        internal static RepositoryAnalysisArtifactRequest<T> Cached(T value) => new(value);

        internal void Complete(T value) => _cache!.CompleteInflight(
            _key!,
            _operation!,
            value);

        internal void Fail(Exception exception) => _cache!.FailInflight(
            _key!,
            _operation!,
            exception);

        private static async Task<T> AwaitAsync(Task<object> result) =>
            (T)await result.ConfigureAwait(false);
    }
}
