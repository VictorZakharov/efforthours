namespace EffortHours.Change;

internal sealed class GitSnapshotInventory
{
    public GitSnapshotInventory(
        IReadOnlyList<ChangeSnapshotFile> files,
        string? firstParentObjectId = null,
        IReadOnlyList<string>? changedPathsFromFirstParent = null)
    {
        Files = [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)];
        FilesByPath = Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        ContentObjectIds = Files
            .Where(file => !file.IsLink && !file.IsSubmodule)
            .Select(file => file.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
        AnalysisIndex = ChangeAnalysisScope.CreateInventoryIndex(Files);
        SourceDigest = ChangeAnalysisScope.ComputeInventoryDigest(Files);
        FirstParentObjectId = firstParentObjectId?.ToLowerInvariant();
        ChangedPathsFromFirstParent = changedPathsFromFirstParent is null
            ? null
            : [.. changedPathsFromFirstParent
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public IReadOnlyDictionary<string, ChangeSnapshotFile> FilesByPath { get; }

    public IReadOnlySet<string> ContentObjectIds { get; }

    public ChangeAnalysisInventoryIndex AnalysisIndex { get; }

    public string SourceDigest { get; }

    public string? FirstParentObjectId { get; }

    public IReadOnlyList<string>? ChangedPathsFromFirstParent { get; }
}
