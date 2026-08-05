namespace Fairbill.Analysis;

/// <summary>
/// Provides the storage operations used while analyzing a repository.
/// Implementations must not write into the repository scope.
/// </summary>
public interface IRepositoryFileSystem
{
    public string GetFullPath(string path);

    public bool DirectoryExists(string path);

    public bool FileExists(string path);

    public FileAttributes GetAttributes(string path);

    public string[] GetFileSystemEntries(string directoryPath);

    public RepositoryFileMetadata GetFileMetadata(string path);

    public Stream OpenRead(string path, int bufferSize);

    public ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default);

    public ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default);
}

public readonly record struct RepositoryFileMetadata(
    long Length,
    long LastWriteTimeUtcTicks,
    bool Exists);

public sealed class PhysicalRepositoryFileSystem : IRepositoryFileSystem
{
    public static PhysicalRepositoryFileSystem Instance { get; } = new();

    private PhysicalRepositoryFileSystem()
    {
    }

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public string[] GetFileSystemEntries(string directoryPath) =>
        Directory.GetFileSystemEntries(directoryPath);

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        FileInfo file = new(path);
        return new RepositoryFileMetadata(
            file.Length,
            file.LastWriteTimeUtc.Ticks,
            file.Exists);
    }

    public Stream OpenRead(string path, int bufferSize) => new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        bufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    public async ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

    public async ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
}
