using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class SnapshotPortfolioAreaPlanning
{
    public static string Digest(IReadOnlyList<ChangeSnapshotFile> inventory, IEnumerable<string> selectedPaths)
    {
        HashSet<string> paths = selectedPaths.ToHashSet(StringComparer.Ordinal);
        return SnapshotMeasurementIdentity.Digest(inventory.Where(f => paths.Contains(f.Path)).OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => new { f.Path, f.ObjectId, f.Mode }));
    }

    public static IReadOnlyList<SnapshotAreaPlan> Plan(IReadOnlyList<ChangeSnapshotFile> inventory,
        SnapshotProjectDefinition project, SnapshotProjectResult? previous, bool attributeSensitive)
    {
        GitArchiveSnapshot pathsOnly = new(inventory.Where(f => !f.IsLink && !f.IsSubmodule)
            .ToDictionary(f => f.Path, _ => Array.Empty<byte>(), StringComparer.Ordinal));
        IReadOnlyList<SnapshotAreaInput> inputs = SnapshotAreaPartition.Partition(pathsOnly, project.Areas);
        SnapshotPeriodResult? prior = previous?.Periods.LastOrDefault(p => p.WholeReceiptId is not null);
        return [.. inputs.Select(area => new SnapshotAreaPlan(area.Id,
            !attributeSensitive && prior?.Areas.FirstOrDefault(a => a.Id == area.Id)?.InventoryDigest == Digest(inventory, area.Snapshot.Files.Keys)
                ? "unchanged-input-receipt-expected" : attributeSensitive ? "archive-attributes-require-verification" : "changed-or-unmeasured-input"))];
    }
}
