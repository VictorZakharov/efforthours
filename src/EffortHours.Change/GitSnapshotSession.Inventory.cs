using System.Diagnostics;

namespace EffortHours.Change;

internal sealed partial class GitSnapshotSession
{
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

        if (parentObjectId is not null && parentInventory is not null && changedPaths is not null &&
            changedPaths.Count <= MaximumIncrementalSnapshotPaths &&
            changedPaths.Sum(path => path.Length) <= MaximumIncrementalPathCharacters)
        {
            return await CreateIncrementalInventoryAsync(
                objectId,
                parentObjectId,
                parentInventory,
                changedPaths,
                primedDelta,
                cancellationToken).ConfigureAwait(false);
        }

        Interlocked.Increment(ref _fullInventoryLoads);
        GitObjectStorageLayout storageLayout = await GetObjectStorageLayoutAsync(
            cancellationToken).ConfigureAwait(false);
        long readStarted = Stopwatch.GetTimestamp();
        IReadOnlyList<ChangeSnapshotFile> files = await GitSnapshotFileSystem.ReadFilesAsync(
            RepositoryPath,
            objectId,
            cancellationToken,
            storageLayout: storageLayout).ConfigureAwait(false);
        Interlocked.Add(
            ref _fullInventoryReadTimestamp,
            Stopwatch.GetTimestamp() - readStarted);
        long projectionStarted = Stopwatch.GetTimestamp();
        GitSnapshotInventory inventory = new(objectId, files, parentObjectId, changedPaths);
        Interlocked.Add(
            ref _fullInventoryProjectionTimestamp,
            Stopwatch.GetTimestamp() - projectionStarted);
        return inventory;
    }

    private async Task<GitObjectStorageLayout> GetObjectStorageLayoutAsync(
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_objectStorageLayout is { } cached)
            {
                return cached;
            }
        }

        GitObjectStorageLayout layout = await GitObjectStorageLayout.ReadAsync(
            RepositoryPath,
            cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _objectStorageLayout ??= layout;
            return _objectStorageLayout.Value;
        }
    }

    private async Task<GitSnapshotInventory> CreateIncrementalInventoryAsync(
        string objectId,
        string parentObjectId,
        GitSnapshotInventory parentInventory,
        IReadOnlyList<string> changedPaths,
        GitSnapshotDelta? primedDelta,
        CancellationToken cancellationToken)
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

        long projectionStarted = Stopwatch.GetTimestamp();
        GitSnapshotInventory inventory = GitSnapshotInventory.CreateIncremental(
            objectId,
            parentObjectId,
            parentInventory,
            changedPaths,
            changedFiles);
        Interlocked.Add(
            ref _incrementalInventoryProjectionTimestamp,
            Stopwatch.GetTimestamp() - projectionStarted);
        return inventory;
    }

    private static TimeSpan Duration(long timestamp) => TimeSpan.FromSeconds(
        (double)timestamp / Stopwatch.Frequency);
}
