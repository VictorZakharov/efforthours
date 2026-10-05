using EffortHours.Analysis;

namespace EffortHours.Tests;

internal sealed class VersionedInMemoryRepository :
    IRepositoryFileSystem,
    IRepositoryAnalysisArtifactCacheProvider,
    IRepositoryVersionedAnalysisProvider,
    IRepositoryImmutableIdentityProvider
{
    private readonly InMemoryRepository _inner = new();
    private readonly string _path;
    private readonly string _contentId;
    private readonly string? _previousContentId;
    private readonly Dictionary<string, string>? _identities;
    private readonly Dictionary<string, string>? _previousIdentities;

    public VersionedInMemoryRepository(IReadOnlyDictionary<string, string> files,
        IReadOnlyDictionary<string, string> previousFiles, RepositoryAnalysisArtifactCache analysisCache,
        RepositoryVersionedAnalysisCache versionCache)
        : this(files.First().Key, files.First().Value, "unused", null, analysisCache, versionCache)
    {
        static Dictionary<string, string> Ids(IReadOnlyDictionary<string, string> values) => values.ToDictionary(
            item => item.Key, item => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(item.Value))), StringComparer.Ordinal);
        _identities = Ids(files);
        _previousIdentities = Ids(previousFiles);
        foreach (var file in files) _inner.WriteText(file.Key, file.Value);
        RepositoryPathSetIdentity = string.Join("|", files.Keys.Order(StringComparer.Ordinal));
    }

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

    public string RepositoryPathSetIdentity { get; set; } = "one-file-path-set";

    public bool TryGetFileContentId(string path, out string contentId)
    {
        if (_identities is not null) return _identities.TryGetValue(Normalize(path), out contentId!);
        contentId = _contentId;
        return FileExists(path);
    }

    public string RootPath => _inner.RootPath;

    public RepositoryAnalysisArtifactCache? AnalysisArtifactCache { get; }

    public RepositoryVersionedAnalysisCache? VersionedAnalysisCache { get; }

    public bool TryGetPreviousFileVersion(
        string path,
        out RepositoryFileVersion previousVersion)
    {
        if (_previousIdentities is not null)
        {
            if (_previousIdentities.TryGetValue(Normalize(path), out string? id))
            { previousVersion = new RepositoryFileVersion(id); return true; }
            previousVersion = default;
            return false;
        }
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
        return metadata with { ContentId = TryGetFileContentId(path, out string id) ? id : null };
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
