using EffortHours.Analysis;

namespace EffortHours.Change;

internal sealed partial class GitSnapshotFileSystem :
    IRepositoryFileSystem,
    IRepositoryAnalysisArtifactCacheProvider,
    IChangeSnapshot
{
    private readonly Lock _directoryGate = new();
    private readonly GitSnapshotInventory _inventory;
    private readonly Lazy<IReadOnlyList<ChangeSnapshotFile>> _resolvedFiles;
    private readonly RepositoryAnalysisArtifactCache? _analysisArtifactCache;
    private readonly string _repositoryPath;
    private readonly Func<GitBatchObjectMetadataReader>? _sharedMetadataReader;
    private readonly Func<GitBatchObjectReader>? _sharedObjectReader;
    private HashSet<string>? _directories;
    private Dictionary<string, string[]>? _entriesByDirectory;
    private GitBatchObjectReader? _ownedObjectReader;
    private GitBatchObjectMetadataReader? _ownedMetadataReader;

    private GitSnapshotFileSystem(
        string repositoryPath,
        string objectId,
        GitSnapshotInventory inventory,
        Func<GitBatchObjectReader>? sharedObjectReader = null,
        Func<GitBatchObjectMetadataReader>? sharedMetadataReader = null,
        RepositoryAnalysisArtifactCache? analysisArtifactCache = null)
    {
        _repositoryPath = repositoryPath;
        _inventory = inventory;
        _analysisArtifactCache = analysisArtifactCache;
        _sharedObjectReader = sharedObjectReader;
        _sharedMetadataReader = sharedMetadataReader;
        ObjectId = objectId;
        RootPath = Path.GetFullPath(Path.Combine(
            repositoryPath,
            ".efforthours-virtual-snapshot",
            objectId));
        _resolvedFiles = new(() => [.. _inventory.Files.Select(ResolveLength)]);
    }

    public string ObjectId { get; }

    public string RootPath { get; }

    public IRepositoryFileSystem FileSystem => this;

    public RepositoryAnalysisArtifactCache? AnalysisArtifactCache =>
        _analysisArtifactCache;

    public IReadOnlyList<ChangeSnapshotFile> Files => _resolvedFiles.Value;

    internal IReadOnlyDictionary<string, ChangeSnapshotFile> FilesByPath =>
        _inventory.FilesByPath;

    internal IReadOnlyDictionary<string, int> ContentObjectCounts =>
        _inventory.ContentObjectCounts;

    internal ChangeAnalysisInventoryIndex AnalysisIndex => _inventory.AnalysisIndex;

    internal int FileCount => _inventory.FileCount;

    internal string InventoryDigest => _inventory.SourceDigest;

    public static async Task<GitSnapshotFileSystem> CreateAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChangeSnapshotFile> files = await ReadFilesAsync(
            repositoryPath,
            objectId,
            cancellationToken).ConfigureAwait(false);
        return new GitSnapshotFileSystem(
            repositoryPath,
            objectId,
            new GitSnapshotInventory(objectId, files));
    }

    internal static GitSnapshotFileSystem Create(
        string repositoryPath,
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files,
        Func<GitBatchObjectReader>? sharedObjectReader = null) =>
        new(
            repositoryPath,
            objectId,
            new GitSnapshotInventory(objectId, files),
            sharedObjectReader);

    internal static GitSnapshotFileSystem Create(
        string repositoryPath,
        string objectId,
        GitSnapshotInventory inventory,
        Func<GitBatchObjectReader>? sharedObjectReader = null,
        Func<GitBatchObjectMetadataReader>? sharedMetadataReader = null,
        RepositoryAnalysisArtifactCache? analysisArtifactCache = null) =>
        new(
            repositoryPath,
            objectId,
            inventory,
            sharedObjectReader,
            sharedMetadataReader,
            analysisArtifactCache);

    internal bool TryGetChangedPathsFrom(
        string baseObjectId,
        out IReadOnlyList<string> changedPaths)
    {
        if (string.Equals(
                _inventory.FirstParentObjectId,
                baseObjectId,
                StringComparison.OrdinalIgnoreCase) &&
            _inventory.ChangedPathsFromFirstParent is { } knownPaths)
        {
            changedPaths = knownPaths;
            return true;
        }

        changedPaths = [];
        return false;
    }

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public bool DirectoryExists(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (string.Equals(fullPath, RootPath, StringComparison.Ordinal))
        {
            return true;
        }

        EnsureDirectoryIndex();
        return _directories!.Contains(fullPath);
    }

    public bool FileExists(string path) => TryGetFile(path, out _);

    public FileAttributes GetAttributes(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (string.Equals(fullPath, RootPath, StringComparison.Ordinal))
        {
            return FileAttributes.Directory;
        }

        if (TryGetFile(fullPath, out ChangeSnapshotFile file))
        {
            return file.IsLink || file.IsSubmodule
                ? FileAttributes.ReparsePoint
                : FileAttributes.Normal;
        }

        EnsureDirectoryIndex();
        if (_directories!.Contains(fullPath))
        {
            return FileAttributes.Directory;
        }

        file = GetFile(fullPath);
        return file.IsLink || file.IsSubmodule
            ? FileAttributes.ReparsePoint
            : FileAttributes.Normal;
    }

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string fullDirectory = Path.GetFullPath(directoryPath);
        EnsureDirectoryIndex();
        if (!_entriesByDirectory!.TryGetValue(fullDirectory, out string[]? entries))
        {
            throw new DirectoryNotFoundException($"Git snapshot directory was not found: {directoryPath}");
        }

        return [.. entries];
    }

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        ChangeSnapshotFile file = GetFile(Path.GetFullPath(path));
        ChangeSnapshotFile resolved = ResolveLength(file);
        return new RepositoryFileMetadata(resolved.Length, 0L, true, file.ObjectId);
    }

    public Stream OpenRead(string path, int bufferSize)
    {
        _ = bufferSize;
        ChangeSnapshotFile file = GetFile(Path.GetFullPath(path));
        return ObjectReader.OpenBlob(file.ObjectId);
    }

    public async ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ChangeSnapshotFile file = GetFile(Path.GetFullPath(path));
        byte[] content = await ObjectReader.ReadBlobAsync(
            file.ObjectId,
            cancellationToken).ConfigureAwait(false);
        return [.. content];
    }

    public async ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        byte[] content = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        using MemoryStream stream = new(content, writable: false);
        using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    ValueTask<byte[]> IChangeSnapshot.ReadAllBytesAsync(
        string relativePath,
        CancellationToken cancellationToken) =>
        ReadAllBytesAsync(ResolveRelativePath(relativePath), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_ownedObjectReader is not null)
        {
            await _ownedObjectReader.DisposeAsync().ConfigureAwait(false);
        }

        _ownedMetadataReader?.Dispose();
    }

    private GitBatchObjectReader ObjectReader =>
        _sharedObjectReader?.Invoke() ??
        (_ownedObjectReader ??= new GitBatchObjectReader(_repositoryPath));

    private GitBatchObjectMetadataReader MetadataReader =>
        _sharedMetadataReader?.Invoke() ??
        (_ownedMetadataReader ??= new GitBatchObjectMetadataReader(_repositoryPath));

    private ChangeSnapshotFile GetFile(string path) =>
        TryGetFile(path, out ChangeSnapshotFile file)
            ? file
            : throw new FileNotFoundException("Git snapshot file was not found.", path);

    private ChangeSnapshotFile ResolveLength(ChangeSnapshotFile file) =>
        file.Length >= 0 || file.IsSubmodule
            ? file
            : file with { Length = MetadataReader.GetBlobLength(file.ObjectId) };

    private bool TryGetFile(string path, out ChangeSnapshotFile file)
    {
        string fullPath = Path.GetFullPath(path);
        if (!IsWithinRoot(fullPath))
        {
            file = null!;
            return false;
        }

        string relativePath = Path.GetRelativePath(RootPath, fullPath)
            .Replace('\\', '/');
        return _inventory.FilesByPath.TryGetValue(relativePath, out file!);
    }

    private void EnsureDirectoryIndex()
    {
        if (_entriesByDirectory is not null)
        {
            return;
        }

        lock (_directoryGate)
        {
            if (_entriesByDirectory is not null)
            {
                return;
            }

            HashSet<string> directories = new(StringComparer.Ordinal) { RootPath };
            Dictionary<string, string> fullFiles = new(StringComparer.Ordinal);
            foreach (ChangeSnapshotFile file in _inventory.FilesByPath.Values)
            {
                string fullPath = ResolveRelativePath(file.Path);
                fullFiles.Add(file.Path, fullPath);
                string? directory = Path.GetDirectoryName(fullPath);
                while (directory is not null && IsWithinRoot(directory))
                {
                    directories.Add(directory);
                    if (string.Equals(directory, RootPath, StringComparison.Ordinal))
                    {
                        break;
                    }

                    directory = Path.GetDirectoryName(directory);
                }
            }

            Dictionary<string, List<string>> entries = directories.ToDictionary(
                directory => directory,
                _ => new List<string>(),
                StringComparer.Ordinal);
            foreach (string directory in directories)
            {
                if (!string.Equals(directory, RootPath, StringComparison.Ordinal))
                {
                    entries[Path.GetDirectoryName(directory)!].Add(directory);
                }
            }

            foreach (string file in fullFiles.Values)
            {
                entries[Path.GetDirectoryName(file)!].Add(file);
            }

            _directories = directories;
            _entriesByDirectory = entries.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        }
    }

    private string ResolveRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string fullPath = Path.GetFullPath(Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath))
        {
            throw new ArgumentOutOfRangeException(nameof(relativePath), "Snapshot path escapes its root.");
        }

        return fullPath;
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(RootPath, path);
        return relative != ".." &&
            !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
