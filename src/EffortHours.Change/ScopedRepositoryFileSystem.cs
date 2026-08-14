using EffortHours.Analysis;

namespace EffortHours.Change;

internal sealed class ScopedRepositoryFileSystem : IRepositoryFileSystem
{
    private readonly IRepositoryFileSystem _inner;
    private readonly string _rootPath;
    private readonly Dictionary<string, string[]> _entriesByDirectory;

    public ScopedRepositoryFileSystem(
        IRepositoryFileSystem inner,
        string rootPath,
        IReadOnlySet<string> relativePaths)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(relativePaths);
        _rootPath = _inner.GetFullPath(rootPath);
        HashSet<string> files = new(StringComparer.Ordinal);
        HashSet<string> directories = new(StringComparer.Ordinal) { _rootPath };
        foreach (string relativePath in relativePaths)
        {
            string fullPath = Resolve(relativePath);
            if (!_inner.FileExists(fullPath))
            {
                continue;
            }

            files.Add(fullPath);
            string? directory = Path.GetDirectoryName(fullPath);
            while (directory is not null && IsWithinRoot(directory))
            {
                directories.Add(directory);
                if (string.Equals(directory, _rootPath, StringComparison.Ordinal))
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
            if (!string.Equals(directory, _rootPath, StringComparison.Ordinal))
            {
                entries[Path.GetDirectoryName(directory)!].Add(directory);
            }
        }

        foreach (string file in files)
        {
            entries[Path.GetDirectoryName(file)!].Add(file);
        }

        _entriesByDirectory = entries.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Order(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    public string GetFullPath(string path) => _inner.GetFullPath(path);

    public bool DirectoryExists(string path) =>
        _entriesByDirectory.ContainsKey(_inner.GetFullPath(path));

    public bool FileExists(string path) => _inner.FileExists(path);

    public FileAttributes GetAttributes(string path)
    {
        string fullPath = _inner.GetFullPath(path);
        return _entriesByDirectory.ContainsKey(fullPath)
            ? FileAttributes.Directory
            : _inner.GetAttributes(fullPath);
    }

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string fullPath = _inner.GetFullPath(directoryPath);
        if (_entriesByDirectory.TryGetValue(fullPath, out string[]? entries))
        {
            return [.. entries];
        }

        if (!_inner.DirectoryExists(fullPath))
        {
            throw new DirectoryNotFoundException($"Repository directory was not found: {directoryPath}");
        }

        return [];
    }

    public RepositoryFileMetadata GetFileMetadata(string path) => _inner.GetFileMetadata(path);

    public Stream OpenRead(string path, int bufferSize) => _inner.OpenRead(path, bufferSize);

    public ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        _inner.ReadAllBytesAsync(path, cancellationToken);

    public ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        _inner.ReadAllLinesAsync(path, cancellationToken);

    private string Resolve(string relativePath)
    {
        ChangeSnapshotInventoryBuilder.ValidateRelativePath(relativePath);
        string fullPath = _inner.GetFullPath(Path.Combine(
            _rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath))
        {
            throw new ArgumentOutOfRangeException(nameof(relativePath), "Analysis path escapes its root.");
        }

        return fullPath;
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(_rootPath, path);
        return relative != ".." &&
            !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
