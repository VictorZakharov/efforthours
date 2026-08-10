using System.Security.Cryptography;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed class DirectoryChangeSnapshot : IRepositoryEvidenceChangeSnapshot, IRepositoryFileSystem
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly IRepositoryFileSystem _backingFileSystem;
    private readonly Dictionary<string, ChangeSnapshotFile> _files = new(PathComparer);
    private readonly HashSet<string> _directories = new(PathComparer);

    public DirectoryChangeSnapshot(
        string rootPath,
        IRepositoryFileSystem backingFileSystem,
        RepositoryEvidence evidence,
        ChangeSnapshotInventory inventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _backingFileSystem = backingFileSystem ?? throw new ArgumentNullException(nameof(backingFileSystem));
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        ArgumentNullException.ThrowIfNull(inventory);

        RootPath = Path.TrimEndingDirectorySeparator(_backingFileSystem.GetFullPath(rootPath));
        if (!_backingFileSystem.DirectoryExists(RootPath))
        {
            throw new DirectoryNotFoundException($"Snapshot directory was not found: {rootPath}");
        }

        if ((_backingFileSystem.GetAttributes(RootPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "A directory change snapshot cannot use a symbolic-link or reparse-point root.");
        }

        ObjectId = inventory.ObjectId;
        Files = [.. inventory.Files];
        _directories.Add(RootPath);
        foreach (ChangeSnapshotFile file in Files)
        {
            string fullPath = ResolveRelativePath(file.Path);
            _files.Add(fullPath, file);
            string? directory = Path.GetDirectoryName(fullPath);
            while (directory is not null && IsWithinRoot(directory))
            {
                _directories.Add(directory);
                if (PathComparer.Equals(directory, RootPath))
                {
                    break;
                }

                directory = Path.GetDirectoryName(directory);
            }
        }
    }

    public string ObjectId { get; }

    public string RootPath { get; }

    public IRepositoryFileSystem FileSystem => this;

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public RepositoryEvidence Evidence { get; }

    public bool SupportsSourceReads => true;

    public string GetFullPath(string path) => _backingFileSystem.GetFullPath(path);

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public FileAttributes GetAttributes(string path)
    {
        string fullPath = Normalize(path);
        if (_directories.Contains(fullPath))
        {
            return FileAttributes.Directory;
        }

        _ = GetFile(fullPath);
        return FileAttributes.Normal;
    }

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string fullDirectory = Normalize(directoryPath);
        if (!_directories.Contains(fullDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Pinned directory snapshot path was not found: {directoryPath}");
        }

        return
        [
            .. _directories
                .Where(path => !PathComparer.Equals(path, fullDirectory) &&
                    PathComparer.Equals(Path.GetDirectoryName(path), fullDirectory))
                .Concat(_files.Keys.Where(path =>
                    PathComparer.Equals(Path.GetDirectoryName(path), fullDirectory)))
                .Order(StringComparer.Ordinal),
        ];
    }

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        ChangeSnapshotFile file = GetFile(Normalize(path));
        return new RepositoryFileMetadata(file.Length, 0L, true);
    }

    public Stream OpenRead(string path, int bufferSize)
    {
        string fullPath = Normalize(path);
        ChangeSnapshotFile file = GetFile(fullPath);
        EnsureStableMetadata(fullPath, file);
        return _backingFileSystem.OpenRead(fullPath, bufferSize);
    }

    public async ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Normalize(path);
        ChangeSnapshotFile file = GetFile(fullPath);
        EnsureStableMetadata(fullPath, file);

        byte[] content;
        try
        {
            content = await _backingFileSystem.ReadAllBytesAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw SnapshotChanged(file.Path, exception);
        }

        string digest = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (content.LongLength != file.Length ||
            !string.Equals(digest, file.ObjectId, StringComparison.Ordinal))
        {
            throw SnapshotChanged(file.Path);
        }

        return content;
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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private string ResolveRelativePath(string relativePath)
    {
        ChangeSnapshotInventoryBuilder.ValidateRelativePath(relativePath);
        string fullPath = Normalize(Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath) || PathComparer.Equals(fullPath, RootPath))
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativePath),
                "Snapshot path escapes the selected directory.");
        }

        return fullPath;
    }

    private ChangeSnapshotFile GetFile(string fullPath) =>
        _files.TryGetValue(fullPath, out ChangeSnapshotFile? file)
            ? file
            : throw new FileNotFoundException(
                "File is outside the pinned directory snapshot.",
                fullPath);

    private void EnsureStableMetadata(string fullPath, ChangeSnapshotFile file)
    {
        try
        {
            if ((_backingFileSystem.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0 ||
                _backingFileSystem.GetFileMetadata(fullPath).Length != file.Length)
            {
                throw SnapshotChanged(file.Path);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw SnapshotChanged(file.Path, exception);
        }
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(RootPath, path);
        return relative != ".." &&
            !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(_backingFileSystem.GetFullPath(path));

    private static InvalidOperationException SnapshotChanged(
        string relativePath,
        Exception? innerException = null) => new(
            $"Directory snapshot path '{relativePath}' changed after its content identity was pinned. " +
            "Rerun the command against a stable base and head.",
            innerException);
}
