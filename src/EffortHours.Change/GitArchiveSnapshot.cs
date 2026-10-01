using System.Formats.Tar;
using System.Text;
using EffortHours.Analysis;

namespace EffortHours.Change;

/// <summary>Read-only archive projection. No archive entry is written to a target tree.</summary>
public sealed class GitArchiveSnapshot : IRepositoryFileSystem, IRepositoryAnalysisArtifactCacheProvider
{
    private readonly IReadOnlyDictionary<string, byte[]> _files;
    private readonly Dictionary<string, string> _contentIds;
    private readonly Dictionary<string, string[]> _entries;

    public GitArchiveSnapshot(IReadOnlyDictionary<string, byte[]> files,
        RepositoryAnalysisArtifactCache? artifactCache = null)
    {
        _files = files;
        RootPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "efforthours-archive", "snapshot"));
        AnalysisArtifactCache = artifactCache;
        _contentIds = files.ToDictionary(p => p.Key, p => SnapshotMeasurementIdentity.Hash(p.Value), StringComparer.Ordinal);
        Dictionary<string, SortedSet<string>> directories = new(StringComparer.Ordinal) { [""] = new(StringComparer.Ordinal) };
        foreach (string path in files.Keys)
        {
            RequireSafePath(path);
            string parent = "";
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string child = parent.Length == 0 ? segments[i] : parent + "/" + segments[i];
                directories[parent].Add(child);
                if (i < segments.Length - 1) directories.TryAdd(child, new(StringComparer.Ordinal));
                parent = child;
            }
        }
        _entries = directories.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
    }

    public string RootPath { get; }
    public IReadOnlyDictionary<string, byte[]> Files => _files;
    public RepositoryAnalysisArtifactCache? AnalysisArtifactCache { get; }
    public string InputDigest => SnapshotMeasurementIdentity.Digest(_contentIds.OrderBy(p => p.Key, StringComparer.Ordinal)
        .Select(p => new { Path = p.Key, Digest = p.Value }));

    public GitArchiveSnapshot Select(IEnumerable<string> paths) => new(
        paths.Distinct(StringComparer.Ordinal).ToDictionary(p => p, p => _files[p], StringComparer.Ordinal), AnalysisArtifactCache);

    public string GetFullPath(string path) => Path.GetFullPath(path);
    public bool DirectoryExists(string path) => _entries.ContainsKey(Relative(path));
    public bool FileExists(string path) => _files.ContainsKey(Relative(path));
    public FileAttributes GetAttributes(string path) => DirectoryExists(path) ? FileAttributes.Directory : FileAttributes.Normal;
    public string[] GetFileSystemEntries(string directoryPath) => [.. _entries[Relative(directoryPath)].Select(p => Path.Combine(RootPath, p.Replace('/', Path.DirectorySeparatorChar)))];
    public RepositoryFileMetadata GetFileMetadata(string path)
    {
        string relative = Relative(path);
        return _files.TryGetValue(relative, out byte[]? bytes)
            ? new(bytes.LongLength, 0, true, _contentIds[relative]) : new(0, 0, false);
    }
    public Stream OpenRead(string path, int bufferSize) => new MemoryStream(_files[Relative(path)], writable: false);
    public ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_files[Relative(path)].ToArray());
    }
    public ValueTask<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using StreamReader reader = new(OpenRead(path, 0), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        List<string> lines = [];
        while (reader.ReadLine() is { } line) lines.Add(line);
        return ValueTask.FromResult(lines.ToArray());
    }

    private string Relative(string path)
    {
        string relative = Path.GetRelativePath(RootPath, Path.GetFullPath(path)).Replace('\\', '/');
        if (relative == ".") return "";
        RequireSafePath(relative);
        return relative;
    }

    public static void RequireSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.Contains('\\') || path.Contains(':') ||
            path.StartsWith('/') || path.Any(char.IsControl) || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Snapshot paths/selectors must be safe repository-relative paths.");
    }

    internal static GitArchiveSnapshot Read(byte[] archive, RepositoryAnalysisArtifactCache? cache)
    {
        using MemoryStream stream = new(archive, writable: false);
        using TarReader reader = new(stream);
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        HashSet<string> portable = new(StringComparer.OrdinalIgnoreCase);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is TarEntryType.Directory or TarEntryType.GlobalExtendedAttributes) continue;
            RequireSafePath(entry.Name);
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                throw new InvalidDataException("Archive links, submodules, and special entries are unsupported.");
            if (files.Count >= 100_000 || !portable.Add(entry.Name))
                throw new InvalidDataException("Archive file-count limit or portable path uniqueness was violated.");
            using MemoryStream content = new();
            entry.DataStream?.CopyTo(content);
            files.Add(entry.Name, content.ToArray());
        }
        if (files.Count == 0) throw new InvalidDataException("Selected archive is empty.");
        return new(files, cache);
    }
}

public sealed partial class GitClient
{
    public async Task<GitArchiveSnapshot> OpenArchiveAsync(string repositoryPath, string objectId,
        RepositoryAnalysisArtifactCache? artifactCache = null, int maximumBytes = 256 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ChangeSnapshotFile> inventory = await ReadSnapshotInventoryAsync(repositoryPath, objectId,
            SnapshotKey(repositoryPath, objectId), cancellationToken)
            .ConfigureAwait(false);
        if (inventory.Any(f => f.IsLink || f.IsSubmodule))
            throw new InvalidDataException("Snapshot portfolios do not support symlinks or submodules.");
        IReadOnlyList<string> arguments = await ArchiveArgumentsAsync(repositoryPath, objectId, cancellationToken).ConfigureAwait(false);
        byte[] archive = await ExternalCommand.RunBinaryAsync("git", repositoryPath,
            arguments, [], maximumBytes, cancellationToken)
            .ConfigureAwait(false);
        return GitArchiveSnapshot.Read(archive, artifactCache);
    }
}
