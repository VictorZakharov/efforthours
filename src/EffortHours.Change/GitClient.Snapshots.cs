namespace EffortHours.Change;

public sealed partial class GitClient
{
    private const int MaximumSnapshotInventories = 16;
    private const int MaximumRememberedFirstParents = 1_024;
    private const int MaximumIncrementalSnapshotPaths = 1_024;
    private const int MaximumIncrementalPathCharacters = 16_000;

    private readonly Dictionary<string, IReadOnlyList<ChangeSnapshotFile>> _snapshotInventories =
        new(PathComparer);
    private readonly Queue<string> _snapshotInventoryOrder = new();
    private readonly Dictionary<string, string?> _firstParents = new(PathComparer);
    private readonly Queue<string> _firstParentOrder = new();
    private readonly Lock _firstParentGate = new();
    private readonly Lock _snapshotInventoryGate = new();

    private static StringComparer PathComparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public async Task<IChangeSnapshot> OpenSnapshotAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        if (_snapshotFactory is not null)
        {
            return await _snapshotFactory(repositoryPath, objectId, cancellationToken)
                .ConfigureAwait(false);
        }

        string root = Path.GetFullPath(repositoryPath);
        string normalizedObjectId = objectId.ToLowerInvariant();
        string key = SnapshotKey(root, normalizedObjectId);
        lock (_snapshotInventoryGate)
        {
            if (_snapshotInventories.TryGetValue(
                key,
                out IReadOnlyList<ChangeSnapshotFile>? cachedFiles))
            {
                return GitSnapshotFileSystem.Create(root, normalizedObjectId, cachedFiles);
            }
        }

        IReadOnlyList<ChangeSnapshotFile> files = await ReadSnapshotInventoryAsync(
            root,
            normalizedObjectId,
            key,
            cancellationToken).ConfigureAwait(false);
        lock (_snapshotInventoryGate)
        {
            if (!_snapshotInventories.TryGetValue(key, out IReadOnlyList<ChangeSnapshotFile>? cachedFiles))
            {
                AddSnapshotInventory(key, files);
                cachedFiles = files;
            }

            return GitSnapshotFileSystem.Create(root, normalizedObjectId, cachedFiles);
        }
    }

    internal void PrimeCommitMetadata(string repositoryPath, GitCommitMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        RememberFirstParent(
            repositoryPath,
            metadata.ObjectId,
            metadata.ParentObjectIds.Count == 0 ? null : metadata.ParentObjectIds[0]);
    }

    private void AddSnapshotInventory(
        string key,
        IReadOnlyList<ChangeSnapshotFile> files)
    {
        if (_snapshotInventories.ContainsKey(key))
        {
            return;
        }

        while (_snapshotInventories.Count >= MaximumSnapshotInventories)
        {
            string oldest = _snapshotInventoryOrder.Dequeue();
            _snapshotInventories.Remove(oldest);
        }

        _snapshotInventories.Add(key, files);
        _snapshotInventoryOrder.Enqueue(key);
    }

    private async Task<IReadOnlyList<ChangeSnapshotFile>> ReadSnapshotInventoryAsync(
        string repositoryPath,
        string objectId,
        string key,
        CancellationToken cancellationToken)
    {
        string? parentObjectId;
        lock (_firstParentGate)
        {
            _firstParents.TryGetValue(key, out parentObjectId);
        }

        IReadOnlyList<ChangeSnapshotFile>? parentFiles = null;
        if (parentObjectId is not null)
        {
            lock (_snapshotInventoryGate)
            {
                _snapshotInventories.TryGetValue(
                    SnapshotKey(repositoryPath, parentObjectId),
                    out parentFiles);
            }
        }

        if (parentObjectId is not null && parentFiles is not null)
        {
            IReadOnlyList<string> changedPaths = await GitSnapshotFileSystem.ReadChangedPathsAsync(
                repositoryPath,
                parentObjectId,
                objectId,
                cancellationToken).ConfigureAwait(false);
            if (changedPaths.Count <= MaximumIncrementalSnapshotPaths &&
                changedPaths.Sum(path => path.Length) <= MaximumIncrementalPathCharacters)
            {
                IReadOnlyList<ChangeSnapshotFile> changedFiles = changedPaths.Count == 0
                    ? []
                    : await GitSnapshotFileSystem.ReadFilesAsync(
                        repositoryPath,
                        objectId,
                        cancellationToken,
                        changedPaths).ConfigureAwait(false);
                return GitSnapshotFileSystem.ApplyIncrementalChanges(
                    parentFiles,
                    changedPaths,
                    changedFiles);
            }
        }

        return await GitSnapshotFileSystem.ReadFilesAsync(
            repositoryPath,
            objectId,
            cancellationToken).ConfigureAwait(false);
    }

    private void RememberFirstParent(
        string repositoryPath,
        string objectId,
        string? parentObjectId)
    {
        string key = SnapshotKey(repositoryPath, objectId);
        lock (_firstParentGate)
        {
            if (_firstParents.ContainsKey(key))
            {
                _firstParents[key] = parentObjectId?.ToLowerInvariant();
                return;
            }

            while (_firstParents.Count >= MaximumRememberedFirstParents)
            {
                _firstParents.Remove(_firstParentOrder.Dequeue());
            }

            _firstParents.Add(key, parentObjectId?.ToLowerInvariant());
            _firstParentOrder.Enqueue(key);
        }
    }

    private static string SnapshotKey(string repositoryPath, string objectId) =>
        $"{Path.GetFullPath(repositoryPath)}\0{objectId.ToLowerInvariant()}";
}
