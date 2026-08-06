using System.Text;
using Fairbill.Analysis;

namespace Fairbill.ChangeCalibration;

internal sealed class MemoryRepositoryFileSystem : IRepositoryFileSystem
{
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public MemoryRepositoryFileSystem(IReadOnlyDictionary<string, string> files)
    {
        string volumeRoot = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? Path.DirectorySeparatorChar.ToString();
        RootPath = Normalize(Path.Combine(volumeRoot, "fairbill-change-fixture", Guid.NewGuid().ToString("N")));
        _directories.Add(RootPath);
        foreach ((string relativePath, string content) in files)
        {
            string fullPath = Resolve(relativePath);
            string? directory = Path.GetDirectoryName(fullPath);
            while (directory is not null && !string.Equals(directory, RootPath, StringComparison.Ordinal))
            {
                _directories.Add(Normalize(directory));
                directory = Path.GetDirectoryName(directory);
            }

            _files.Add(fullPath, Encoding.UTF8.GetBytes(content));
        }
    }

    public string RootPath { get; }

    public string GetFullPath(string path) => Normalize(path);

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public FileAttributes GetAttributes(string path) => DirectoryExists(path)
        ? FileAttributes.Directory
        : FileExists(path)
            ? FileAttributes.Normal
            : throw new FileNotFoundException("Synthetic fixture entry not found.", path);

    public string[] GetFileSystemEntries(string directoryPath)
    {
        string directory = Normalize(directoryPath);
        return
        [
            .. _directories.Where(path =>
                    !string.Equals(path, directory, StringComparison.Ordinal) &&
                    string.Equals(Path.GetDirectoryName(path), directory, StringComparison.Ordinal))
                .Concat(_files.Keys.Where(path =>
                    string.Equals(Path.GetDirectoryName(path), directory, StringComparison.Ordinal))),
        ];
    }

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        byte[] content = Get(path);
        return new RepositoryFileMetadata(content.LongLength, 0, true);
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
        string text = Encoding.UTF8.GetString(Get(path)).Replace("\r\n", "\n", StringComparison.Ordinal);
        return ValueTask.FromResult(text.Split('\n'));
    }

    private byte[] Get(string path) => _files.TryGetValue(Normalize(path), out byte[]? content)
        ? content
        : throw new FileNotFoundException("Synthetic fixture file not found.", path);

    private string Resolve(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Fixture path must be relative: {relativePath}");
        }

        string fullPath = Normalize(Path.Combine(
            RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(RootPath, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException($"Fixture path escapes its state: {relativePath}");
        }

        return fullPath;
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
