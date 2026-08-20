namespace EffortHours.Analysis;

public interface IRepositoryVersionedAnalysisProvider
{
    public RepositoryVersionedAnalysisCache? VersionedAnalysisCache { get; }

    public bool TryGetPreviousFileVersion(
        string path,
        out RepositoryFileVersion previousVersion);
}

public readonly record struct RepositoryFileVersion(string ContentId);

public sealed record RepositoryVersionedAnalysisCacheStatistics(
    int Requests,
    int Hits,
    int UniqueKeys,
    int Evictions,
    int PeakEntries,
    long RetainedBytes,
    long PeakRetainedBytes,
    int EntryLimit,
    long ByteLimit);

/// <summary>
/// Retains bounded, immutable analyzer artifacts that can share structure across
/// adjacent file versions. Values and keys remain invocation-local and never
/// enter repository evidence or reports.
/// </summary>
public sealed class RepositoryVersionedAnalysisCache
{
    public const int DefaultMaximumEntries = 8;
    public const long DefaultMaximumRetainedBytes = 16L * 1024L * 1024L;

    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _recency = new();
    private readonly HashSet<string> _seenKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InflightEntry> _inflight = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly int _maximumEntries;
    private readonly long _maximumRetainedBytes;
    private int _requests;
    private int _hits;
    private int _evictions;
    private int _peakEntries;
    private long _retainedBytes;
    private long _peakRetainedBytes;

    public RepositoryVersionedAnalysisCache(
        int maximumEntries = DefaultMaximumEntries,
        long maximumRetainedBytes = DefaultMaximumRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRetainedBytes);
        _maximumEntries = maximumEntries;
        _maximumRetainedBytes = maximumRetainedBytes;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<RepositoryVersionedAnalysisArtifact<T>>> factory,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        InflightEntry operation;
        bool isOwner;
        lock (_gate)
        {
            _requests++;
            _seenKeys.Add(key);
            if (_entries.TryGetValue(key, out CacheEntry? cached))
            {
                if (cached.Value is not T typed)
                {
                    throw TypeMismatch();
                }

                _hits++;
                Touch(cached);
                return typed;
            }

            if (_inflight.TryGetValue(key, out InflightEntry? pending))
            {
                if (pending.ValueType != typeof(T))
                {
                    throw TypeMismatch();
                }

                _hits++;
                operation = pending;
                isOwner = false;
            }
            else
            {
                operation = new InflightEntry(
                    typeof(T),
                    new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously));
                _inflight.Add(key, operation);
                isOwner = true;
            }
        }

        if (!isOwner)
        {
            return (T)await operation.Completion.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            RepositoryVersionedAnalysisArtifact<T> artifact =
                await factory(cancellationToken).ConfigureAwait(false);
            Add(key, artifact.Value, artifact.RetainedBytes);
            Complete(key, operation, artifact.Value);
            return artifact.Value;
        }
        catch (Exception exception)
        {
            Fail(key, operation, exception);
            throw;
        }
    }

    public bool TryGetExistingAsync<T>(string key, out Task<T> result)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out CacheEntry? cached))
            {
                if (cached.Value is not T typed)
                {
                    throw TypeMismatch();
                }

                Touch(cached);
                result = Task.FromResult(typed);
                return true;
            }

            if (_inflight.TryGetValue(key, out InflightEntry? pending))
            {
                if (pending.ValueType != typeof(T))
                {
                    throw TypeMismatch();
                }

                result = AwaitAsync<T>(pending.Completion.Task);
                return true;
            }

            result = null!;
            return false;
        }
    }

    public bool TryGetCompleted<T>(string key, out T result)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out CacheEntry? cached))
            {
                result = null!;
                return false;
            }

            if (cached.Value is not T typed)
            {
                throw TypeMismatch();
            }

            Touch(cached);
            result = typed;
            return true;
        }
    }

    public RepositoryVersionedAnalysisCacheStatistics GetStatistics()
    {
        lock (_gate)
        {
            return new RepositoryVersionedAnalysisCacheStatistics(
                _requests,
                _hits,
                _seenKeys.Count,
                _evictions,
                _peakEntries,
                _retainedBytes,
                _peakRetainedBytes,
                _maximumEntries,
                _maximumRetainedBytes);
        }
    }

    private void Add<T>(string key, T value, long retainedBytes)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedBytes);
        if (retainedBytes > _maximumRetainedBytes)
        {
            return;
        }

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out CacheEntry? existing))
            {
                _retainedBytes -= existing.RetainedBytes;
                _entries[key] = existing with { Value = value, RetainedBytes = retainedBytes };
                _retainedBytes += retainedBytes;
                Touch(_entries[key]);
                RecordPeaks();
                return;
            }

            while (_entries.Count >= _maximumEntries ||
                _retainedBytes + retainedBytes > _maximumRetainedBytes)
            {
                RemoveLeastRecent();
            }

            LinkedListNode<string> node = _recency.AddFirst(key);
            _entries.Add(key, new CacheEntry(value, retainedBytes, node));
            _retainedBytes += retainedBytes;
            RecordPeaks();
        }
    }

    private void RemoveLeastRecent()
    {
        LinkedListNode<string> node = _recency.Last!;
        string key = node.Value;
        CacheEntry entry = _entries[key];
        _entries.Remove(key);
        _recency.Remove(node);
        _retainedBytes -= entry.RetainedBytes;
        _evictions++;
    }

    private void Touch(CacheEntry entry)
    {
        _recency.Remove(entry.RecencyNode);
        _recency.AddFirst(entry.RecencyNode);
    }

    private void RecordPeaks()
    {
        _peakEntries = Math.Max(_peakEntries, _entries.Count);
        _peakRetainedBytes = Math.Max(_peakRetainedBytes, _retainedBytes);
    }

    private void Complete(string key, InflightEntry operation, object value)
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

    private void Fail(string key, InflightEntry operation, Exception exception)
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

    private static InvalidOperationException TypeMismatch() => new(
        "A versioned analysis cache key was reused for a different value type.");

    private static async Task<T> AwaitAsync<T>(Task<object> task)
        where T : class => (T)await task.ConfigureAwait(false);

    private sealed record CacheEntry(
        object Value,
        long RetainedBytes,
        LinkedListNode<string> RecencyNode);

    private sealed record InflightEntry(
        Type ValueType,
        TaskCompletionSource<object> Completion);
}

public sealed record RepositoryVersionedAnalysisArtifact<T>(
    T Value,
    long RetainedBytes)
    where T : class;
