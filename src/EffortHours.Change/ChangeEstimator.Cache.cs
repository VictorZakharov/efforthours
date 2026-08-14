namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private sealed class SnapshotAnalysisCache
    {
        private readonly int _maximumEntries;
        private readonly Dictionary<SnapshotAnalysisKey, LinkedListNode<SnapshotAnalysisEntry>> _entries = [];
        private readonly LinkedList<SnapshotAnalysisEntry> _leastRecentlyUsed = [];
        private int _requests;
        private int _hits;
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
            _requests++;
            SnapshotAnalysisKey key = new(cacheNamespace, objectId, analysisScopeId);
            if (!_entries.TryGetValue(key, out LinkedListNode<SnapshotAnalysisEntry>? node))
            {
                analysis = null!;
                return false;
            }

            _leastRecentlyUsed.Remove(node);
            _leastRecentlyUsed.AddLast(node);
            _hits++;
            analysis = node.Value.Analysis;
            return true;
        }

        public void Add(
            string cacheNamespace,
            string objectId,
            string analysisScopeId,
            SnapshotAnalysis analysis)
        {
            SnapshotAnalysisKey key = new(cacheNamespace, objectId, analysisScopeId);
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

        public SnapshotAnalysisCacheStatistics GetStatistics() => new(
            _requests,
            _hits,
            _evictions,
            _peakEntries);

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
            int Evictions,
            int PeakEntries);
    }
}
