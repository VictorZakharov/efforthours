using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class SnapshotPortfolioDiagnostics
{
    public static IReadOnlyList<string> DirectoryIds(IEnumerable<string> paths)
    {
        HashSet<string> directories = new(StringComparer.Ordinal) { "." };
        foreach (string path in paths)
        {
            string parent = path;
            while (parent.Contains('/'))
            {
                parent = parent[..parent.LastIndexOf('/')];
                directories.Add(parent);
            }
        }
        return [.. directories.Select(SnapshotMeasurementIdentity.Digest).Order(StringComparer.Ordinal)];
    }

    public static IReadOnlyList<SnapshotBodyFingerprint> Bodies(GitArchiveSnapshot snapshot, RepositoryEvidence evidence) => [.. evidence.Facts
        .Where(f => f.Kind == EvidenceKinds.File && snapshot.Files.ContainsKey(f.Scope))
        .Where(f => !f.Tags.Any(t => t is "classification:generated" or "classification:vendored" or "classification:minified" or "classification:test" or "content:binary"))
        .Where(f => Path.GetExtension(f.Scope).ToLowerInvariant() is ".cs" or ".js" or ".jsx" or ".ts" or ".tsx" or ".gd" or
            ".py" or ".go" or ".java" or ".kt" or ".rs" or ".php" or ".c" or ".cpp" or ".h")
        .Select(f => new SnapshotBodyFingerprint(SnapshotMeasurementIdentity.Hash(snapshot.Files[f.Scope]), snapshot.Files[f.Scope].LongLength))
        .Distinct().OrderBy(b => b.Digest, StringComparer.Ordinal)];

    public static IReadOnlyList<SnapshotSharedSourceReview> SharedBodies(IReadOnlyList<SnapshotProjectResult> projects,
        IReadOnlyList<SnapshotMeasurementReceipt> receipts)
    {
        Dictionary<string, SnapshotMeasurementReceipt> byId = receipts.ToDictionary(r => r.Id, StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<SnapshotBodyFingerprint>> bodies = new(StringComparer.Ordinal);
        foreach (SnapshotProjectResult project in projects)
        {
            string? latest = project.Periods.LastOrDefault(p => p.WholeReceiptId is not null)?.WholeReceiptId;
            if (latest is not null) bodies[project.Id] = byId[latest].MaintainedBodies;
        }
        List<SnapshotSharedSourceReview> result = [];
        string[] ids = [.. bodies.Keys.Order(StringComparer.Ordinal)];
        for (int i = 0; i < ids.Length; i++)
        {
            Dictionary<string, long> first = bodies[ids[i]].ToDictionary(b => b.Digest, b => b.Bytes, StringComparer.Ordinal);
            for (int j = i + 1; j < ids.Length; j++)
            {
                SnapshotBodyFingerprint[] shared = [.. bodies[ids[j]].Where(b => first.ContainsKey(b.Digest))];
                if (shared.Length != 0) result.Add(new(ids[i], ids[j], shared.Length, shared.Sum(b => b.Bytes)));
            }
        }
        return result;
    }

    public static SnapshotProjectResult Unavailable(SnapshotPortfolioManifest manifest, SnapshotProjectDefinition project,
        DateTimeOffset asOf, string issue, string? head = null, DateTimeOffset? first = null, bool shallow = false) => new()
        {
            Id = project.Id,
            HeadObjectId = head,
            FirstAvailableCommitAt = first,
            ShallowHistory = shallow,
            AreasDigest = SnapshotMeasurementIdentity.Digest(project.Areas),
            PlanningIssue = issue,
            Periods = [.. SnapshotPortfolioSelection.Select(manifest.Year, TimeZoneInfo.FindSystemTimeZoneById(manifest.Timezone), asOf, []).Select(p => p.Status is "future" or "baseline-zero" ? p : p with { Status = "unavailable", Hours = null })],
        };
}
