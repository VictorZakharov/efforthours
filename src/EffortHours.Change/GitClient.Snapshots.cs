namespace EffortHours.Change;

public sealed partial class GitClient
{
    private readonly Dictionary<string, IReadOnlyList<ChangeSnapshotFile>> _snapshotInventories =
        new(PathComparer);
    private readonly Queue<string> _snapshotInventoryOrder = new();
    private readonly Dictionary<string, string?> _firstParents = new(PathComparer);
    private readonly Queue<string> _firstParentOrder = new();
    private readonly Dictionary<string, GitSnapshotSession> _snapshotSessions =
        new(PathComparer);
    private readonly Lock _firstParentGate = new();
    private readonly Lock _snapshotInventoryGate = new();
    private readonly Lock _snapshotSessionGate = new();

    private static StringComparer PathComparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Opens one standalone immutable snapshot. Portfolio planners use an explicit
    /// repository-scoped session so several snapshots can additionally share one
    /// bounded object reader with a deterministic disposal boundary.
    /// </summary>
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

    internal GitSnapshotSession GetSnapshotSession(string repositoryPath)
    {
        string root = Path.GetFullPath(repositoryPath);
        lock (_snapshotSessionGate)
        {
            if (!_snapshotSessions.TryGetValue(root, out GitSnapshotSession? session) ||
                session.IsDisposed)
            {
                session = new GitSnapshotSession(root, _snapshotFactory);
                _snapshotSessions[root] = session;
            }

            return session;
        }
    }

    private void AddSnapshotInventory(
        string key,
        IReadOnlyList<ChangeSnapshotFile> files)
    {
        if (_snapshotInventories.ContainsKey(key))
        {
            return;
        }

        while (_snapshotInventories.Count >=
            GitSnapshotSession.MaximumStandaloneSnapshotInventories)
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
            if (changedPaths.Count <= GitSnapshotSession.MaximumIncrementalSnapshotPaths &&
                changedPaths.Sum(path => path.Length) <=
                    GitSnapshotSession.MaximumIncrementalPathCharacters)
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

            while (_firstParents.Count >= GitSnapshotSession.MaximumRememberedFirstParents)
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
