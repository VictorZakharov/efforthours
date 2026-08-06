using System.Text;
using Fairbill.Analysis;

namespace Fairbill.EndToEndTests;

internal sealed class MemoryRepositoryFileSystem : IRepositoryFileSystem
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly HashSet<string> _directories = new(PathComparer);
    private readonly Dictionary<string, byte[]> _files = new(PathComparer);

    public MemoryRepositoryFileSystem()
    {
        string volumeRoot = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? Path.DirectorySeparatorChar.ToString();
        RootPath = Normalize(Path.Combine(volumeRoot, "fairbill-e2e-memory", Guid.NewGuid().ToString("N")));
        _directories.Add(RootPath);
    }

    public string RootPath { get; }

    public void WriteText(string relativePath, string content)
    {
        string fullPath = Normalize(Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(RootPath, fullPath);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(relativePath));
        }

        string? directory = Path.GetDirectoryName(fullPath);
        while (directory is not null && !PathComparer.Equals(directory, RootPath))
        {
            _directories.Add(Normalize(directory));
            directory = Path.GetDirectoryName(directory);
        }

        _files[fullPath] = Encoding.UTF8.GetBytes(content);
    }

    public string GetFullPath(string path) => Normalize(path);

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public FileAttributes GetAttributes(string path) => DirectoryExists(path)
        ? FileAttributes.Directory
        : FileExists(path)
            ? FileAttributes.Normal
            : throw new FileNotFoundException("Memory fixture entry not found.", path);

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string directory = Normalize(directoryPath);
        return
        [
            .. _directories.Where(path =>
                    !PathComparer.Equals(path, directory) &&
                    PathComparer.Equals(Path.GetDirectoryName(path), directory))
                .Concat(_files.Keys.Where(path =>
                    PathComparer.Equals(Path.GetDirectoryName(path), directory))),
        ];
    }

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        byte[] content = Get(path);
        return new RepositoryFileMetadata(content.Length, 0, true);
    }

    public Stream OpenRead(string path, int bufferSize)
    {
        _ = bufferSize;
        return new MemoryStream(Get(path), writable: false);
    }

    public ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<byte[]>([.. Get(path)]);
    }

    public ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string text = Encoding.UTF8.GetString(Get(path));
        return ValueTask.FromResult(text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'));
    }

    private byte[] Get(string path) => _files.TryGetValue(Normalize(path), out byte[]? content)
        ? content
        : throw new FileNotFoundException("Memory fixture file not found.", path);

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
