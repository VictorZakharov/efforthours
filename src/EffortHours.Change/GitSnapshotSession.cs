using EffortHours.Analysis;

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

    public int ObjectReaderStarts { get; init; }

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
}

/// <summary>
/// Owns the bounded immutable-inventory and Git-object caches for one repository
/// during one portfolio invocation.
/// </summary>
internal sealed class GitSnapshotSession : IAsyncDisposable
{
    internal const int MaximumSnapshotInventories = 16;
    internal const int MaximumRememberedFirstParents =
        GitPortfolioPlannerOptions.DefaultMaximumHistoryCommits;
    internal const int MaximumIncrementalSnapshotPaths = 1_024;
    internal const int MaximumIncrementalPathCharacters = 16_000;

    private readonly Dictionary<string, GitSnapshotInventory> _inventories =
        new(StringComparer.Ordinal);
    private readonly Queue<string> _inventoryOrder = new();
    private readonly Dictionary<string, string?> _firstParents = new(StringComparer.Ordinal);
    private readonly Queue<string> _firstParentOrder = new();
    private readonly HashSet<string> _batchEligibleHeads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GitSnapshotDelta> _primedDeltas =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenInventoryObjects = new(StringComparer.Ordinal);
    private readonly Func<string, string, CancellationToken, Task<IChangeSnapshot>>? _snapshotFactory;
    private readonly RepositoryAnalysisArtifactCache _analysisArtifactCache = new();
    private readonly Lock _gate = new();
    private GitBatchObjectReader? _objectReader;
    private int _inventoryRequests;
    private int _inventoryHits;
    private int _inventoryRevisitMisses;
    private int _fullInventoryLoads;
    private int _incrementalInventoryLoads;
    private int _batchedIncrementalInventoryLoads;
    private int _inventoryEvictions;
    private int _peakRetainedInventories;
    private bool _disposed;

    public GitSnapshotSession(
        string repositoryPath,
        Func<string, string, CancellationToken, Task<IChangeSnapshot>>? snapshotFactory)
    {
        RepositoryPath = Path.GetFullPath(repositoryPath);
        _snapshotFactory = snapshotFactory;
    }

    public string RepositoryPath { get; }

    public bool IsDisposed
    {
        get
        {
            lock (_gate)
            {
                return _disposed;
            }
        }
    }

    public async Task<IChangeSnapshot> OpenSnapshotAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_snapshotFactory is not null)
        {
            return await _snapshotFactory(RepositoryPath, objectId, cancellationToken)
                .ConfigureAwait(false);
        }

        string normalizedObjectId = objectId.ToLowerInvariant();
        Interlocked.Increment(ref _inventoryRequests);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_inventories.TryGetValue(
                normalizedObjectId,
                out GitSnapshotInventory? cachedInventory))
            {
                _inventoryHits++;
                return CreateSnapshot(normalizedObjectId, cachedInventory);
            }

            if (!_seenInventoryObjects.Add(normalizedObjectId))
            {
                _inventoryRevisitMisses++;
            }
        }

        GitSnapshotInventory inventory = await ReadInventoryAsync(
            normalizedObjectId,
            cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_inventories.TryGetValue(
                normalizedObjectId,
                out GitSnapshotInventory? cachedInventory))
            {
                AddInventory(normalizedObjectId, inventory);
                cachedInventory = inventory;
            }
            else
            {
                _inventoryHits++;
            }

            return CreateSnapshot(normalizedObjectId, cachedInventory);
        }
    }

    public async Task<IChangeSnapshot> OpenStandaloneSnapshotAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        if (_snapshotFactory is not null)
        {
            return await _snapshotFactory(RepositoryPath, objectId, cancellationToken)
                .ConfigureAwait(false);
        }

        return await GitSnapshotFileSystem.CreateAsync(
            RepositoryPath,
            objectId.ToLowerInvariant(),
            cancellationToken).ConfigureAwait(false);
    }

    public void PrimeCommitMetadata(GitCommitMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        string? parent = metadata.ParentObjectIds.Count == 0
            ? null
            : metadata.ParentObjectIds[0].ToLowerInvariant();
        string objectId = metadata.ObjectId.ToLowerInvariant();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (metadata.ParentObjectIds.Count == 1)
            {
                _batchEligibleHeads.Add(objectId);
            }
            else
            {
                _batchEligibleHeads.Remove(objectId);
            }

            if (_firstParents.ContainsKey(objectId))
            {
                _firstParents[objectId] = parent;
                return;
            }

            while (_firstParents.Count >= MaximumRememberedFirstParents)
            {
                string evicted = _firstParentOrder.Dequeue();
                _firstParents.Remove(evicted);
                _batchEligibleHeads.Remove(evicted);
                _primedDeltas.Remove(evicted);
            }

            _firstParents.Add(objectId, parent);
            _firstParentOrder.Enqueue(objectId);
        }
    }

    public async Task PrimeIncrementalDeltasAsync(
        IEnumerable<string> objectIds,
        CancellationToken cancellationToken)
    {
        if (_snapshotFactory is not null)
        {
            return;
        }

        string[] requested;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            requested = [.. objectIds
                .Select(objectId => objectId.ToLowerInvariant())
                .Where(objectId => _batchEligibleHeads.Contains(objectId) &&
                    !_primedDeltas.ContainsKey(objectId))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
        }

        if (requested.Length == 0)
        {
            return;
        }

        IReadOnlyDictionary<string, GitSnapshotDelta> deltas;
        try
        {
            deltas = await GitSnapshotDeltaBatch.ReadAsync(
                RepositoryPath,
                requested,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalCommandOutputLimitException)
        {
            return;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach ((string objectId, GitSnapshotDelta delta) in deltas)
            {
                _primedDeltas.TryAdd(objectId, delta);
            }
        }
    }

    public GitSnapshotSessionStatistics GetStatistics()
    {
        lock (_gate)
        {
            GitObjectReaderStatistics reader = _objectReader?.GetStatistics() ?? new();
            RepositoryAnalysisArtifactCacheStatistics artifacts =
                _analysisArtifactCache.GetStatistics();
            return new GitSnapshotSessionStatistics
            {
                AnalysisArtifactRequests = artifacts.Requests,
                AnalysisArtifactHits = artifacts.Hits,
                UniqueAnalysisArtifactKeys = artifacts.UniqueKeys,
                AnalysisArtifactRevisitMisses = artifacts.RevisitMisses,
                AnalysisArtifactEvictions = artifacts.Evictions,
                PeakRetainedAnalysisArtifacts = artifacts.PeakEntries,
                InventoryRequests = _inventoryRequests,
                InventoryHits = _inventoryHits,
                UniqueInventoryObjects = _seenInventoryObjects.Count,
                InventoryRevisitMisses = _inventoryRevisitMisses,
                FullInventoryLoads = _fullInventoryLoads,
                IncrementalInventoryLoads = _incrementalInventoryLoads,
                BatchedIncrementalInventoryLoads = _batchedIncrementalInventoryLoads,
                InventoryEvictions = _inventoryEvictions,
                PeakRetainedInventories = _peakRetainedInventories,
                ObjectReaderStarts = _objectReader is null ? 0 : 1,
                BlobRequests = reader.Requests,
                BlobCacheHits = reader.CacheHits,
                BlobCacheEvictions = reader.CacheEvictions,
                UniqueBlobObjects = reader.UniqueObjects,
                BlobRequestedBytes = reader.RequestedBytes,
                BlobCacheHitBytes = reader.CacheHitBytes,
                BlobReadBytes = reader.ReadBytes,
                UniqueBlobBytes = reader.UniqueObjectBytes,
                RetainedBlobBytes = reader.RetainedCacheBytes,
                PeakCachedBlobBytes = reader.PeakCachedBytes,
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        GitBatchObjectReader? reader;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            reader = _objectReader;
            _inventories.Clear();
            _inventoryOrder.Clear();
            _firstParents.Clear();
            _firstParentOrder.Clear();
            _batchEligibleHeads.Clear();
            _primedDeltas.Clear();
            _seenInventoryObjects.Clear();
        }

        if (reader is not null)
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private GitSnapshotFileSystem CreateSnapshot(
        string objectId,
        GitSnapshotInventory inventory) =>
        GitSnapshotFileSystem.Create(
            RepositoryPath,
            objectId,
            inventory,
            GetObjectReader,
            _analysisArtifactCache);

    private GitBatchObjectReader GetObjectReader()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _objectReader ??= new GitBatchObjectReader(RepositoryPath);
        }
    }

    private async Task<GitSnapshotInventory> ReadInventoryAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        string? parentObjectId;
        GitSnapshotInventory? parentInventory = null;
        GitSnapshotDelta? primedDelta = null;
        lock (_gate)
        {
            _firstParents.TryGetValue(objectId, out parentObjectId);
            if (parentObjectId is not null)
            {
                _inventories.TryGetValue(parentObjectId, out parentInventory);
            }

            _primedDeltas.TryGetValue(objectId, out primedDelta);
        }

        IReadOnlyList<string>? changedPaths = primedDelta?.ChangedPaths;
        if (changedPaths is null && parentObjectId is not null && parentInventory is not null)
        {
            changedPaths = await GitSnapshotFileSystem.ReadChangedPathsAsync(
                RepositoryPath,
                parentObjectId,
                objectId,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ChangeSnapshotFile> files;
        if (parentObjectId is not null && parentInventory is not null && changedPaths is not null &&
            changedPaths.Count <= MaximumIncrementalSnapshotPaths &&
                changedPaths.Sum(path => path.Length) <= MaximumIncrementalPathCharacters)
        {
            IReadOnlyList<ChangeSnapshotFile> changedFiles = primedDelta?.ChangedFiles ??
                (changedPaths.Count == 0
                    ? []
                    : await GitSnapshotFileSystem.ReadFilesAsync(
                        RepositoryPath,
                        objectId,
                        cancellationToken,
                        changedPaths).ConfigureAwait(false));
            Interlocked.Increment(ref _incrementalInventoryLoads);
            if (primedDelta is not null)
            {
                Interlocked.Increment(ref _batchedIncrementalInventoryLoads);
            }

            files = GitSnapshotFileSystem.ApplyIncrementalChanges(
                parentInventory.Files,
                changedPaths,
                changedFiles);
        }
        else
        {
            Interlocked.Increment(ref _fullInventoryLoads);
            files = await GitSnapshotFileSystem.ReadFilesAsync(
                RepositoryPath,
                objectId,
                cancellationToken).ConfigureAwait(false);
        }

        return new GitSnapshotInventory(files, parentObjectId, changedPaths);
    }

    private void AddInventory(string objectId, GitSnapshotInventory inventory)
    {
        while (_inventories.Count >= MaximumSnapshotInventories)
        {
            _inventories.Remove(_inventoryOrder.Dequeue());
            _inventoryEvictions++;
        }

        _inventories.Add(objectId, inventory);
        _inventoryOrder.Enqueue(objectId);
        _peakRetainedInventories = Math.Max(_peakRetainedInventories, _inventories.Count);
    }
}
