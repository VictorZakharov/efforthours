using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace EffortHours.Change;

internal sealed class GitSnapshotInventory
{
    private readonly ImmutableDictionary<string, int> _contentObjectCounts;
    private readonly ImmutableSortedDictionary<string, ChangeSnapshotFile> _filesByPath;
    private readonly Lazy<IReadOnlyList<ChangeSnapshotFile>> _files;
    private readonly Lazy<string> _pathSetIdentity;
    private readonly GitSnapshotInventoryDigest _sourceDigest;

    public GitSnapshotInventory(
        string objectId,
        IReadOnlyList<ChangeSnapshotFile> files,
        string? firstParentObjectId = null,
        IReadOnlyList<string>? changedPathsFromFirstParent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        _filesByPath = files.ToImmutableSortedDictionary(
            file => file.Path,
            file => file,
            StringComparer.Ordinal);
        _contentObjectCounts = CreateContentObjectCounts(_filesByPath.Values);
        _files = CreateFiles(_filesByPath);
        _pathSetIdentity = CreatePathSetIdentity(_filesByPath.Keys);
        _sourceDigest = GitSnapshotInventoryDigest.Create(_filesByPath.Values);
        AnalysisIndex = ChangeAnalysisScope.CreateInventoryIndex(_filesByPath.Values);
        RootObjectId = objectId.ToLowerInvariant();
        FirstParentInventory = null;
        FirstParentObjectId = firstParentObjectId?.ToLowerInvariant();
        ChangedPathsFromFirstParent = CanonicalPaths(changedPathsFromFirstParent);
    }

    private GitSnapshotInventory(
        GitSnapshotInventory parent,
        string parentObjectId,
        IReadOnlyList<string> changedPaths,
        IReadOnlyList<ChangeSnapshotFile> changedFiles)
    {
        Dictionary<string, ChangeSnapshotFile> replacements = changedFiles.ToDictionary(
            file => file.Path,
            StringComparer.Ordinal);
        string[] affectedPaths = [.. changedPaths
            .Concat(replacements.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        ImmutableSortedDictionary<string, ChangeSnapshotFile> filesByPath =
            parent._filesByPath;
        ImmutableDictionary<string, int> contentObjectCounts = parent._contentObjectCounts;
        GitSnapshotInventoryDigest sourceDigest = parent._sourceDigest;
        List<(string Path, bool BeforeExists, bool AfterExists)> pathChanges = [];
        bool pathSetChanged = false;
        HashSet<string> removedPaths = new(changedPaths, StringComparer.Ordinal);
        foreach (string path in affectedPaths)
        {
            bool beforeExists = filesByPath.TryGetValue(path, out ChangeSnapshotFile? before);
            ChangeSnapshotFile? after = replacements.GetValueOrDefault(path);
            bool afterExists = after is not null || beforeExists && !removedPaths.Contains(path);
            after ??= afterExists ? before : null;
            pathSetChanged |= beforeExists != afterExists;

            if (before is not null && !SameContentObject(before, after))
            {
                contentObjectCounts = RemoveContentObject(contentObjectCounts, before);
            }

            if (after is not null && !SameContentObject(before, after))
            {
                contentObjectCounts = AddContentObject(contentObjectCounts, after);
            }

            filesByPath = after is null
                ? filesByPath.Remove(path)
                : filesByPath.SetItem(path, after);
            sourceDigest = after is null
                ? sourceDigest.Remove(path)
                : sourceDigest.SetItem(after);
            pathChanges.Add((path, beforeExists, afterExists));
        }

        _filesByPath = filesByPath;
        _contentObjectCounts = contentObjectCounts;
        _files = CreateFiles(_filesByPath);
        _pathSetIdentity = pathSetChanged
            ? CreatePathSetIdentity(_filesByPath.Keys)
            : parent._pathSetIdentity;
        _sourceDigest = sourceDigest;
        AnalysisIndex = parent.AnalysisIndex.Apply(pathChanges);
        RootObjectId = parent.RootObjectId;
        FirstParentInventory = parent;
        FirstParentObjectId = parentObjectId.ToLowerInvariant();
        ChangedPathsFromFirstParent = CanonicalPaths(affectedPaths);
    }

    public IReadOnlyList<ChangeSnapshotFile> Files => _files.Value;

    public IReadOnlyDictionary<string, ChangeSnapshotFile> FilesByPath => _filesByPath;

    public IReadOnlyDictionary<string, int> ContentObjectCounts => _contentObjectCounts;

    public ChangeAnalysisInventoryIndex AnalysisIndex { get; }

    public int FileCount => _filesByPath.Count;

    public string PathSetIdentity => _pathSetIdentity.Value;

    public string SourceDigest => _sourceDigest.Value;

    public string RootObjectId { get; }

    public GitSnapshotInventory? FirstParentInventory { get; }

    public string? FirstParentObjectId { get; }

    public IReadOnlyList<string>? ChangedPathsFromFirstParent { get; }

    public static GitSnapshotInventory CreateIncremental(
        string objectId,
        string parentObjectId,
        GitSnapshotInventory parent,
        IReadOnlyList<string> changedPaths,
        IReadOnlyList<ChangeSnapshotFile> changedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentObjectId);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(changedPaths);
        ArgumentNullException.ThrowIfNull(changedFiles);
        return new GitSnapshotInventory(
            parent,
            parentObjectId,
            changedPaths,
            changedFiles);
    }

    private static Lazy<IReadOnlyList<ChangeSnapshotFile>> CreateFiles(
        ImmutableSortedDictionary<string, ChangeSnapshotFile> filesByPath) =>
        new(() => [.. filesByPath.Values]);

    private static Lazy<string> CreatePathSetIdentity(IEnumerable<string> paths)
    {
        string[] canonicalPaths = [.. paths.Order(StringComparer.Ordinal)];
        return new Lazy<string>(() =>
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData("efforthours:repository-path-set:1\0"u8);
            foreach (string path in canonicalPaths)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(path));
                hash.AppendData([0]);
            }

            return $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
        });
    }

    private static ImmutableDictionary<string, int> CreateContentObjectCounts(
        IEnumerable<ChangeSnapshotFile> files)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (ChangeSnapshotFile file in files)
        {
            if (!file.IsLink && !file.IsSubmodule)
            {
                counts[file.ObjectId] = counts.GetValueOrDefault(file.ObjectId) + 1;
            }
        }

        return counts.ToImmutableDictionary(StringComparer.Ordinal);
    }

    private static ImmutableDictionary<string, int> AddContentObject(
        ImmutableDictionary<string, int> counts,
        ChangeSnapshotFile file)
    {
        if (file.IsLink || file.IsSubmodule)
        {
            return counts;
        }

        return counts.SetItem(file.ObjectId, counts.GetValueOrDefault(file.ObjectId) + 1);
    }

    private static ImmutableDictionary<string, int> RemoveContentObject(
        ImmutableDictionary<string, int> counts,
        ChangeSnapshotFile file)
    {
        if (file.IsLink || file.IsSubmodule ||
            !counts.TryGetValue(file.ObjectId, out int count))
        {
            return counts;
        }

        return count == 1
            ? counts.Remove(file.ObjectId)
            : counts.SetItem(file.ObjectId, count - 1);
    }

    private static bool SameContentObject(
        ChangeSnapshotFile? before,
        ChangeSnapshotFile? after) => before is not null && after is not null &&
        before.IsLink == after.IsLink &&
        before.IsSubmodule == after.IsSubmodule &&
        string.Equals(before.ObjectId, after.ObjectId, StringComparison.Ordinal);

    private static IReadOnlyList<string>? CanonicalPaths(IEnumerable<string>? paths) => paths is null
        ? null
        : [.. paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
