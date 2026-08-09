using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

internal class InMemoryRepository : IRepositoryFileSystem
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly HashSet<string> _directories = new(PathComparer);
    private readonly Dictionary<string, MemoryFile> _files = new(PathComparer);
    private readonly Dictionary<string, FileAttributes> _attributeOverrides = new(PathComparer);
    private long _logicalClock = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    public InMemoryRepository()
    {
        string volumeRoot = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? Path.DirectorySeparatorChar.ToString();
        RootPath = Path.GetFullPath(Path.Combine(
            volumeRoot,
            "efforthours-in-memory-tests",
            Guid.NewGuid().ToString("N")));
        _directories.Add(Normalize(RootPath));
    }

    public string RootPath { get; }

    public void WriteText(string relativePath, string content) =>
        WriteBytes(relativePath, Encoding.UTF8.GetBytes(content));

    public void WriteBytes(string relativePath, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        string fullPath = ResolveRepositoryPath(relativePath);
        AddParentDirectories(fullPath);
        _files[fullPath] = new MemoryFile([.. content], ++_logicalClock);
    }

    public string[] EnumerateRelativeFiles() =>
        [.. _files.Keys
            .Select(path => Path.GetRelativePath(RootPath, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)];

    public void SetAttributes(string relativePath, FileAttributes attributes)
    {
        string fullPath = ResolveRepositoryPath(relativePath);
        if (!_directories.Contains(fullPath) && !_files.ContainsKey(fullPath))
        {
            throw new FileNotFoundException("The in-memory entry does not exist.", fullPath);
        }

        _attributeOverrides[fullPath] = attributes;
    }

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public FileAttributes GetAttributes(string path)
    {
        string fullPath = Normalize(path);
        if (_attributeOverrides.TryGetValue(fullPath, out FileAttributes attributes))
        {
            return attributes;
        }

        if (_directories.Contains(fullPath))
        {
            return FileAttributes.Directory;
        }

        if (_files.ContainsKey(fullPath))
        {
            return FileAttributes.Normal;
        }

        throw new FileNotFoundException("The in-memory entry does not exist.", fullPath);
    }

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string fullDirectoryPath = Normalize(directoryPath);
        if (!_directories.Contains(fullDirectoryPath))
        {
            throw new DirectoryNotFoundException($"The in-memory directory does not exist: {directoryPath}");
        }

        return
        [
            .. _directories
                .Where(path => !PathComparer.Equals(path, fullDirectoryPath) &&
                    PathComparer.Equals(Path.GetDirectoryName(path), fullDirectoryPath))
                .Concat(_files.Keys.Where(path =>
                    PathComparer.Equals(Path.GetDirectoryName(path), fullDirectoryPath))),
        ];
    }

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        string fullPath = Normalize(path);
        if (!_files.TryGetValue(fullPath, out MemoryFile? file))
        {
            throw new FileNotFoundException("The in-memory file does not exist.", fullPath);
        }

        return new RepositoryFileMetadata(file.Content.LongLength, file.LastWriteTimeUtcTicks, true);
    }

    public Stream OpenRead(string path, int bufferSize)
    {
        _ = bufferSize;
        MemoryFile file = GetFile(path);
        return new MemoryStream(file.Content, writable: false);
    }

    public ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<byte[]>([.. GetFile(path).Content]);
    }

    public ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MemoryStream stream = new(GetFile(path).Content, writable: false);
        using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return ValueTask.FromResult(lines.ToArray());
    }

    private MemoryFile GetFile(string path)
    {
        string fullPath = Normalize(path);
        return _files.TryGetValue(fullPath, out MemoryFile? file)
            ? file
            : throw new FileNotFoundException("The in-memory file does not exist.", fullPath);
    }

    private string ResolveRepositoryPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Fixture paths must be repository-relative.", nameof(relativePath));
        }

        string fullPath = Normalize(Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string resolved = Path.GetRelativePath(RootPath, fullPath);
        if (resolved == ".." ||
            Path.IsPathRooted(resolved) ||
            resolved.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            resolved.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(relativePath), "Fixture path escapes the repository root.");
        }

        return fullPath;
    }

    private void AddParentDirectories(string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        while (directory is not null && !PathComparer.Equals(directory, RootPath))
        {
            _directories.Add(Normalize(directory));
            directory = Path.GetDirectoryName(directory);
        }

        _directories.Add(Normalize(RootPath));
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed record MemoryFile(byte[] Content, long LastWriteTimeUtcTicks);
}

internal sealed class InMemoryScanCacheStore : IRepositoryScanCacheStore
{
    private readonly Dictionary<string, string> _documents = new(StringComparer.Ordinal);

    public bool Exists(string path) => _documents.ContainsKey(Path.GetFullPath(path));

    public string ReadAllText(string path) => _documents[Path.GetFullPath(path)];

    public Task<RepositoryScanCache?> LoadAsync(
        string path,
        string repositoryKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_documents.TryGetValue(Path.GetFullPath(path), out string? json))
        {
            return Task.FromResult<RepositoryScanCache?>(null);
        }

        RepositoryScanCache cache = ContractJson.Deserialize<RepositoryScanCache>(json);
        return Task.FromResult<RepositoryScanCache?>(
            cache.AnalyzerVersion == RepositoryScanner.AnalyzerVersion &&
            cache.RepositoryKey == repositoryKey &&
            ContractValidation.Validate(cache).Count == 0
                ? cache
                : null);
    }

    public Task SaveAsync(
        string path,
        RepositoryScanCache cache,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents[Path.GetFullPath(path)] = ContractJson.Serialize(cache) + Environment.NewLine;
        return Task.CompletedTask;
    }
}
