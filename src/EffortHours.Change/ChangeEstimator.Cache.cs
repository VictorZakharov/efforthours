namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private sealed class SnapshotAnalysisCache
    {
        private readonly int _maximumEntries;
        private readonly Dictionary<SnapshotAnalysisKey, LinkedListNode<SnapshotAnalysisEntry>> _entries = [];
        private readonly LinkedList<SnapshotAnalysisEntry> _leastRecentlyUsed = [];
        private readonly HashSet<SnapshotAnalysisKey> _seenKeys = [];
        private readonly Dictionary<SnapshotAnalysisKey, TaskCompletionSource<SnapshotAnalysis>>
            _inflight = [];
        private readonly Lock _gate = new();
        private int _requests;
        private int _hits;
        private int _revisitMisses;
        private int _evictions;
        private int _peakEntries;

        public SnapshotAnalysisCache(int maximumEntries = int.MaxValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
            _maximumEntries = maximumEntries;
        }

        public bool TryGet(
            string cacheNamespace,
            string objectId,
            string analysisScopeId,
            out SnapshotAnalysis analysis)
        {
            lock (_gate)
            {
                _requests++;
                SnapshotAnalysisKey key = new(cacheNamespace, objectId, analysisScopeId);
                if (!_entries.TryGetValue(key, out LinkedListNode<SnapshotAnalysisEntry>? node))
                {
                    if (!_seenKeys.Add(key))
                    {
                        _revisitMisses++;
                    }

                    analysis = null!;
                    return false;
                }

                _leastRecentlyUsed.Remove(node);
                _leastRecentlyUsed.AddLast(node);
                _hits++;
                analysis = node.Value.Analysis;
                return true;
            }
        }

        public void Add(
            string cacheNamespace,
            string objectId,
            string analysisScopeId,
            SnapshotAnalysis analysis)
        {
            lock (_gate)
            {
                SnapshotAnalysisKey key = new(cacheNamespace, objectId, analysisScopeId);
                AddCore(key, analysis);
            }
        }

        public bool TryGetExistingAsync(
            string cacheNamespace,
            string objectId,
            string analysisScopeId,
            out Task<SnapshotAnalysis> result)
        {
            lock (_gate)
            {
                SnapshotAnalysisKey key = new(cacheNamespace, objectId, analysisScopeId);
                if (_entries.TryGetValue(
                    key,
                    out LinkedListNode<SnapshotAnalysisEntry>? cached))
                {
                    _leastRecentlyUsed.Remove(cached);
                    _leastRecentlyUsed.AddLast(cached);
                    result = Task.FromResult(cached.Value.Analysis);
                    return true;
                }

                if (_inflight.TryGetValue(key, out TaskCompletionSource<SnapshotAnalysis>? pending))
                {
                    result = pending.Task;
                    return true;
                }

                result = null!;
                return false;
            }
        }

        public async Task<SnapshotAnalysis> GetOrCreateAsync(
            string cacheNamespace,
            string objectId,
            string analysisScopeId,
            Func<CancellationToken, Task<SnapshotAnalysis>> factory,
            CancellationToken cancellationToken,
            bool recordRequest = true)
        {
            ArgumentNullException.ThrowIfNull(factory);
            SnapshotAnalysisKey key = new(cacheNamespace, objectId, analysisScopeId);
            TaskCompletionSource<SnapshotAnalysis> operation;
            bool owner;
            lock (_gate)
            {
                bool seenBefore = false;
                if (recordRequest)
                {
                    _requests++;
                    seenBefore = !_seenKeys.Add(key);
                }

                if (_entries.TryGetValue(
                    key,
                    out LinkedListNode<SnapshotAnalysisEntry>? cached))
                {
                    _leastRecentlyUsed.Remove(cached);
                    _leastRecentlyUsed.AddLast(cached);
                    if (recordRequest && seenBefore)
                    {
                        _hits++;
                    }

                    return cached.Value.Analysis;
                }

                if (_inflight.TryGetValue(key, out operation!))
                {
                    if (recordRequest && seenBefore)
                    {
                        _hits++;
                    }

                    owner = false;
                }
                else
                {
                    if (recordRequest && seenBefore)
                    {
                        _revisitMisses++;
                    }

                    operation = new TaskCompletionSource<SnapshotAnalysis>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _inflight.Add(key, operation);
                    owner = true;
                }
            }

            if (!owner)
            {
                return await operation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                SnapshotAnalysis analysis = await factory(cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    AddCore(key, analysis);
                    _inflight.Remove(key);
                }

                operation.TrySetResult(analysis);
                return analysis;
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    _inflight.Remove(key);
                }

                operation.TrySetException(exception);
                _ = operation.Task.Exception;
                throw;
            }
        }

        public SnapshotAnalysisCacheStatistics GetStatistics()
        {
            lock (_gate)
            {
                return new SnapshotAnalysisCacheStatistics(
                    _requests,
                    _hits,
                    _seenKeys.Count,
                    _revisitMisses,
                    _evictions,
                    _peakEntries);
            }
        }

        private void AddCore(SnapshotAnalysisKey key, SnapshotAnalysis analysis)
        {
            if (_entries.TryGetValue(key, out LinkedListNode<SnapshotAnalysisEntry>? existing))
            {
                _leastRecentlyUsed.Remove(existing);
                _entries.Remove(key);
            }

            while (_entries.Count >= _maximumEntries)
            {
                LinkedListNode<SnapshotAnalysisEntry> oldest = _leastRecentlyUsed.First!;
                _leastRecentlyUsed.RemoveFirst();
                _entries.Remove(oldest.Value.Key);
                _evictions++;
            }

            SnapshotAnalysisEntry entry = new(key, analysis);
            LinkedListNode<SnapshotAnalysisEntry> node = _leastRecentlyUsed.AddLast(entry);
            _entries.Add(key, node);
            _peakEntries = Math.Max(_peakEntries, _entries.Count);
        }

        private readonly record struct SnapshotAnalysisKey(
            string CacheNamespace,
            string ObjectId,
            string AnalysisScopeId);

        private sealed record SnapshotAnalysisEntry(
            SnapshotAnalysisKey Key,
            SnapshotAnalysis Analysis);

        public readonly record struct SnapshotAnalysisCacheStatistics(
            int Requests,
            int Hits,
            int UniqueKeys,
            int RevisitMisses,
            int Evictions,
            int PeakEntries);
    }
}
