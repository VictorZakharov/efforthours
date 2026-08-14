using EffortHours.Analysis;

namespace EffortHours.Change;

internal sealed partial class GitSnapshotFileSystem : IRepositoryFileSystem, IChangeSnapshot
{
    private readonly Dictionary<string, ChangeSnapshotFile> _files;
    private readonly Lock _directoryGate = new();
    private readonly string _repositoryPath;
    private readonly Func<GitBatchObjectReader>? _sharedObjectReader;
    private HashSet<string>? _directories;
    private Dictionary<string, string[]>? _entriesByDirectory;
    private GitBatchObjectReader? _ownedObjectReader;

    private GitSnapshotFileSystem(
        string repositoryPath,
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files,
        Func<GitBatchObjectReader>? sharedObjectReader = null)
    {
        _repositoryPath = repositoryPath;
        _sharedObjectReader = sharedObjectReader;
        ObjectId = objectId;
        RootPath = Path.GetFullPath(Path.Combine(
            repositoryPath,
            ".efforthours-virtual-snapshot",
            objectId));
        _files = files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        Files = [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)];
    }

    public string ObjectId { get; }

    public string RootPath { get; }

    public IRepositoryFileSystem FileSystem => this;

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public static async Task<GitSnapshotFileSystem> CreateAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChangeSnapshotFile> files = await ReadFilesAsync(
            repositoryPath,
            objectId,
            cancellationToken).ConfigureAwait(false);
        return new GitSnapshotFileSystem(repositoryPath, objectId, files);
    }

    internal static GitSnapshotFileSystem Create(
        string repositoryPath,
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files,
        Func<GitBatchObjectReader>? sharedObjectReader = null) =>
        new(repositoryPath, objectId, files, sharedObjectReader);

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
        return new RepositoryFileMetadata(file.Length, 0L, true);
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
    }

    private GitBatchObjectReader ObjectReader =>
        _sharedObjectReader?.Invoke() ??
        (_ownedObjectReader ??= new GitBatchObjectReader(_repositoryPath));

    private ChangeSnapshotFile GetFile(string path) =>
        TryGetFile(path, out ChangeSnapshotFile file)
            ? file
            : throw new FileNotFoundException("Git snapshot file was not found.", path);

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
        return _files.TryGetValue(relativePath, out file!);
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
            foreach (ChangeSnapshotFile file in Files)
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
