using System.Collections.Immutable;

namespace EffortHours.Change;

internal sealed class ChangeAnalysisInventoryIndex
{
    private readonly ImmutableSortedSet<string> _contextPaths;
    private readonly ImmutableDictionary<string, ImmutableSortedSet<string>>
        _representativePathsByExtension;

    private ChangeAnalysisInventoryIndex(
        ImmutableSortedSet<string> contextPaths,
        ImmutableDictionary<string, ImmutableSortedSet<string>> representativePathsByExtension)
    {
        _contextPaths = contextPaths;
        _representativePathsByExtension = representativePathsByExtension;
    }

    public IReadOnlyCollection<string> ContextPaths => _contextPaths;

    public IReadOnlyList<string> RepresentativePaths =>
    [
        .. _representativePathsByExtension.Values
            .Where(paths => paths.Count > 0)
            .Select(paths => paths.Min!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    public static ChangeAnalysisInventoryIndex Create(IEnumerable<ChangeSnapshotFile> files)
    {
        string[] paths = [.. files.Select(file => file.Path)];
        ImmutableSortedSet<string> contextPaths = paths
            .Where(ChangeAnalysisScope.IsContextPath)
            .ToImmutableSortedSet(StringComparer.Ordinal);
        ImmutableDictionary<string, ImmutableSortedSet<string>> representativePaths = paths
            .Where(ChangeAnalysisScope.IsRepresentativePath)
            .GroupBy(
                path => Path.GetExtension(path) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.ToImmutableSortedSet(StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);

        return new ChangeAnalysisInventoryIndex(contextPaths, representativePaths);
    }

    public ChangeAnalysisInventoryIndex Apply(
        IEnumerable<(string Path, bool BeforeExists, bool AfterExists)> changes)
    {
        ImmutableSortedSet<string> contextPaths = _contextPaths;
        ImmutableDictionary<string, ImmutableSortedSet<string>> representativePaths =
            _representativePathsByExtension;
        foreach ((string path, bool beforeExists, bool afterExists) in changes)
        {
            if (beforeExists == afterExists)
            {
                continue;
            }

            if (beforeExists)
            {
                RemovePath(ref contextPaths, ref representativePaths, path);
            }

            if (afterExists)
            {
                AddPath(ref contextPaths, ref representativePaths, path);
            }
        }

        return ReferenceEquals(contextPaths, _contextPaths) &&
            ReferenceEquals(representativePaths, _representativePathsByExtension)
                ? this
                : new ChangeAnalysisInventoryIndex(contextPaths, representativePaths);
    }

    private static void AddPath(
        ref ImmutableSortedSet<string> contextPaths,
        ref ImmutableDictionary<string, ImmutableSortedSet<string>> representativePaths,
        string path)
    {
        if (ChangeAnalysisScope.IsContextPath(path))
        {
            contextPaths = contextPaths.Add(path);
        }

        if (!ChangeAnalysisScope.IsRepresentativePath(path))
        {
            return;
        }

        string extension = Path.GetExtension(path);
        ImmutableSortedSet<string> paths = representativePaths.GetValueOrDefault(extension) ??
            ImmutableSortedSet.Create<string>(StringComparer.Ordinal);
        representativePaths = representativePaths.SetItem(extension, paths.Add(path));
    }

    private static void RemovePath(
        ref ImmutableSortedSet<string> contextPaths,
        ref ImmutableDictionary<string, ImmutableSortedSet<string>> representativePaths,
        string path)
    {
        if (ChangeAnalysisScope.IsContextPath(path))
        {
            contextPaths = contextPaths.Remove(path);
        }

        if (!ChangeAnalysisScope.IsRepresentativePath(path))
        {
            return;
        }

        string extension = Path.GetExtension(path);
        if (!representativePaths.TryGetValue(
            extension,
            out ImmutableSortedSet<string>? paths))
        {
            return;
        }

        paths = paths.Remove(path);
        representativePaths = paths.Count == 0
            ? representativePaths.Remove(extension)
            : representativePaths.SetItem(extension, paths);
    }
}
