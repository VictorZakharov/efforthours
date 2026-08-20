using EffortHours.Analysis;

namespace EffortHours.Tests;

internal sealed class VersionedInMemoryRepository :
    IRepositoryFileSystem,
    IRepositoryAnalysisArtifactCacheProvider,
    IRepositoryVersionedAnalysisProvider
{
    private readonly InMemoryRepository _inner = new();
    private readonly string _path;
    private readonly string _contentId;
    private readonly string? _previousContentId;

    public VersionedInMemoryRepository(
        string path,
        string content,
        string contentId,
        string? previousContentId,
        RepositoryAnalysisArtifactCache analysisCache,
        RepositoryVersionedAnalysisCache versionedAnalysisCache)
    {
        _path = path;
        _contentId = contentId;
        _previousContentId = previousContentId;
        AnalysisArtifactCache = analysisCache;
        VersionedAnalysisCache = versionedAnalysisCache;
        _inner.WriteText(path, content);
    }

    public string RootPath => _inner.RootPath;

    public RepositoryAnalysisArtifactCache? AnalysisArtifactCache { get; }

    public RepositoryVersionedAnalysisCache? VersionedAnalysisCache { get; }

    public bool TryGetPreviousFileVersion(
        string path,
        out RepositoryFileVersion previousVersion)
    {
        if (_previousContentId is not null &&
            string.Equals(Normalize(path), Normalize(_path), StringComparison.OrdinalIgnoreCase))
        {
            previousVersion = new RepositoryFileVersion(_previousContentId);
            return true;
        }

        previousVersion = default;
        return false;
    }

    public string GetFullPath(string path) => _inner.GetFullPath(path);

    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    public bool FileExists(string path) => _inner.FileExists(path);

    public FileAttributes GetAttributes(string path) => _inner.GetAttributes(path);

    public string[] GetFileSystemEntries(string directoryPath) =>
        _inner.GetFileSystemEntries(directoryPath);

    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        RepositoryFileMetadata metadata = _inner.GetFileMetadata(path);
        return metadata with { ContentId = _contentId };
    }

    public Stream OpenRead(string path, int bufferSize) => _inner.OpenRead(path, bufferSize);

    public ValueTask<byte[]> ReadAllBytesAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        _inner.ReadAllBytesAsync(path, cancellationToken);

    public ValueTask<string[]> ReadAllLinesAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        _inner.ReadAllLinesAsync(path, cancellationToken);

    private string Normalize(string path)
    {
        string candidate = Path.IsPathRooted(path)
            ? path
            : Path.Combine(RootPath, path.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetRelativePath(RootPath, _inner.GetFullPath(candidate)).Replace('\\', '/');
    }
}
