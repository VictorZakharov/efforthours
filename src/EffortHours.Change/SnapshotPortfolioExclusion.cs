using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>Applies reviewed path exclusion once at admission, before inventory planning, scanning, or areas.</summary>
internal static class SnapshotPortfolioExclusion
{
    public const string EmptyIssue = "excluded-all-files";

    public static IReadOnlyList<ChangeSnapshotFile> Admit(IReadOnlyList<ChangeSnapshotFile> inventory, RepositoryPathExclusion? exclusion)
    {
        if (exclusion is null) return inventory;
        ChangeSnapshotFile[] admitted = [.. inventory.Where(file => !exclusion.Excludes(file.Path))];
        return admitted.Length > 0 ? admitted : throw Empty();
    }

    public static (GitArchiveSnapshot Snapshot, SnapshotExclusionSummary? Summary) Admit(GitArchiveSnapshot archive,
        RepositoryPathExclusion? exclusion)
    {
        if (exclusion is null) return (archive, null);
        List<string> admitted = [];
        int excludedFiles = 0;
        long excludedBytes = 0;
        foreach ((string path, byte[] bytes) in archive.Files)
        {
            if (!exclusion.Excludes(path)) { admitted.Add(path); continue; }
            excludedFiles++;
            excludedBytes += bytes.LongLength;
        }
        if (admitted.Count == 0) throw Empty();
        // Projection keeps the archive's content IDs; excluded bytes never reach the scanner.
        return (archive.Select(admitted), new()
        {
            Digest = exclusion.Digest,
            Exclude = exclusion.Globs,
            ExcludedFileCount = excludedFiles,
            ExcludedBytes = excludedBytes,
        });
    }

    public static bool Same(SnapshotExclusionSummary? left, SnapshotExclusionSummary? right) =>
        left is null || right is null ? left is null && right is null :
        SnapshotMeasurementIdentity.Digest(left) == SnapshotMeasurementIdentity.Digest(right);

    private static SnapshotPlanningException Empty() => new(EmptyIssue,
        "Path exclusion admits no archived file in a selected snapshot; narrow 'exclude' rather than estimating zero.");
}
