using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed class EvidenceChangeSnapshot : IRepositoryEvidenceChangeSnapshot
{
    private readonly UnavailableSnapshotFileSystem _fileSystem;

    public EvidenceChangeSnapshot(
        RepositoryEvidence evidence,
        ChangeSnapshotInventory inventory)
    {
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        ArgumentNullException.ThrowIfNull(inventory);
        ObjectId = inventory.ObjectId;
        Files = [.. inventory.Files];
        string volumeRoot = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? Path.DirectorySeparatorChar.ToString();
        RootPath = Path.GetFullPath(Path.Combine(
            volumeRoot,
            ".efforthours-evidence-snapshot",
            ObjectId[7..]));
        _fileSystem = new UnavailableSnapshotFileSystem(RootPath);
    }

    public string ObjectId { get; }

    public string RootPath { get; }

    public IRepositoryFileSystem FileSystem => _fileSystem;

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public RepositoryEvidence Evidence { get; }

    public bool SupportsSourceReads => false;

    public ValueTask<byte[]> ReadAllBytesAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        _ = relativePath;
        cancellationToken.ThrowIfCancellationRequested();
        throw SourceUnavailable();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static InvalidOperationException SourceUnavailable() => new(
        "Serialized repository evidence contains source hashes and measurements, but no source bodies.");

    private sealed class UnavailableSnapshotFileSystem(string rootPath) : IRepositoryFileSystem
    {
        private readonly string _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public bool DirectoryExists(string path) =>
            string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                _rootPath,
                StringComparison.Ordinal);

        public bool FileExists(string path)
        {
            _ = path;
            return false;
        }

        public FileAttributes GetAttributes(string path) => DirectoryExists(path)
            ? FileAttributes.Directory
            : throw SourceUnavailable();

        public string[] GetFileSystemEntries(string directoryPath)
        {
            _ = directoryPath;
            throw SourceUnavailable();
        }

        public RepositoryFileMetadata GetFileMetadata(string path)
        {
            _ = path;
            throw SourceUnavailable();
        }

        public Stream OpenRead(string path, int bufferSize)
        {
            _ = path;
            _ = bufferSize;
            throw SourceUnavailable();
        }

        public ValueTask<byte[]> ReadAllBytesAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            throw SourceUnavailable();
        }

        public ValueTask<string[]> ReadAllLinesAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _ = path;
            cancellationToken.ThrowIfCancellationRequested();
            throw SourceUnavailable();
        }
    }
}
