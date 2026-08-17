namespace EffortHours.Change;

internal sealed record GitSnapshotSessionStatistics
{
    public int InventoryRequests { get; init; }

    public int InventoryHits { get; init; }

    public int FullInventoryLoads { get; init; }

    public int IncrementalInventoryLoads { get; init; }

    public int InventoryEvictions { get; init; }

    public int PeakRetainedInventories { get; init; }

    public int ObjectReaderStarts { get; init; }

    public long BlobRequests { get; init; }

    public long BlobCacheHits { get; init; }

    public long BlobCacheEvictions { get; init; }

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

    private readonly Dictionary<string, IReadOnlyList<ChangeSnapshotFile>> _inventories =
        new(StringComparer.Ordinal);
    private readonly Queue<string> _inventoryOrder = new();
    private readonly Dictionary<string, string?> _firstParents = new(StringComparer.Ordinal);
    private readonly Queue<string> _firstParentOrder = new();
    private readonly Func<string, string, CancellationToken, Task<IChangeSnapshot>>? _snapshotFactory;
    private readonly Lock _gate = new();
    private GitBatchObjectReader? _objectReader;
    private int _inventoryRequests;
    private int _inventoryHits;
    private int _fullInventoryLoads;
    private int _incrementalInventoryLoads;
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
                out IReadOnlyList<ChangeSnapshotFile>? cachedFiles))
            {
                _inventoryHits++;
                return CreateSnapshot(normalizedObjectId, cachedFiles);
            }
        }

        IReadOnlyList<ChangeSnapshotFile> files = await ReadInventoryAsync(
            normalizedObjectId,
            cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_inventories.TryGetValue(
                normalizedObjectId,
                out IReadOnlyList<ChangeSnapshotFile>? cachedFiles))
            {
                AddInventory(normalizedObjectId, files);
                cachedFiles = files;
            }
            else
            {
                _inventoryHits++;
            }

            return CreateSnapshot(normalizedObjectId, cachedFiles);
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
            if (_firstParents.ContainsKey(objectId))
            {
                _firstParents[objectId] = parent;
                return;
            }

            while (_firstParents.Count >= MaximumRememberedFirstParents)
            {
                _firstParents.Remove(_firstParentOrder.Dequeue());
            }

            _firstParents.Add(objectId, parent);
            _firstParentOrder.Enqueue(objectId);
        }
    }

    public GitSnapshotSessionStatistics GetStatistics()
    {
        lock (_gate)
        {
            GitObjectReaderStatistics reader = _objectReader?.GetStatistics() ?? new();
            return new GitSnapshotSessionStatistics
            {
                InventoryRequests = _inventoryRequests,
                InventoryHits = _inventoryHits,
                FullInventoryLoads = _fullInventoryLoads,
                IncrementalInventoryLoads = _incrementalInventoryLoads,
                InventoryEvictions = _inventoryEvictions,
                PeakRetainedInventories = _peakRetainedInventories,
                ObjectReaderStarts = _objectReader is null ? 0 : 1,
                BlobRequests = reader.Requests,
                BlobCacheHits = reader.CacheHits,
                BlobCacheEvictions = reader.CacheEvictions,
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
        }

        if (reader is not null)
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private GitSnapshotFileSystem CreateSnapshot(
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files) =>
        GitSnapshotFileSystem.Create(
            RepositoryPath,
            objectId,
            files,
            GetObjectReader);

    private GitBatchObjectReader GetObjectReader()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _objectReader ??= new GitBatchObjectReader(RepositoryPath);
        }
    }

    private async Task<IReadOnlyList<ChangeSnapshotFile>> ReadInventoryAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        string? parentObjectId;
        IReadOnlyList<ChangeSnapshotFile>? parentFiles = null;
        lock (_gate)
        {
            _firstParents.TryGetValue(objectId, out parentObjectId);
            if (parentObjectId is not null)
            {
                _inventories.TryGetValue(parentObjectId, out parentFiles);
            }
        }

        if (parentObjectId is not null && parentFiles is not null)
        {
            IReadOnlyList<string> changedPaths = await GitSnapshotFileSystem.ReadChangedPathsAsync(
                RepositoryPath,
                parentObjectId,
                objectId,
                cancellationToken).ConfigureAwait(false);
            if (changedPaths.Count <= MaximumIncrementalSnapshotPaths &&
                changedPaths.Sum(path => path.Length) <= MaximumIncrementalPathCharacters)
            {
                IReadOnlyList<ChangeSnapshotFile> changedFiles = changedPaths.Count == 0
                    ? []
                    : await GitSnapshotFileSystem.ReadFilesAsync(
                        RepositoryPath,
                        objectId,
                        cancellationToken,
                        changedPaths).ConfigureAwait(false);
                Interlocked.Increment(ref _incrementalInventoryLoads);
                return GitSnapshotFileSystem.ApplyIncrementalChanges(
                    parentFiles,
                    changedPaths,
                    changedFiles);
            }
        }

        Interlocked.Increment(ref _fullInventoryLoads);
        return await GitSnapshotFileSystem.ReadFilesAsync(
            RepositoryPath,
            objectId,
            cancellationToken).ConfigureAwait(false);
    }

    private void AddInventory(string objectId, IReadOnlyList<ChangeSnapshotFile> files)
    {
        while (_inventories.Count >= MaximumSnapshotInventories)
        {
            _inventories.Remove(_inventoryOrder.Dequeue());
            _inventoryEvictions++;
        }

        _inventories.Add(objectId, files);
        _inventoryOrder.Enqueue(objectId);
        _peakRetainedInventories = Math.Max(_peakRetainedInventories, _inventories.Count);
    }
}
