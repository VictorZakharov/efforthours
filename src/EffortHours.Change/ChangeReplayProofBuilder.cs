using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangeReplayProofBuilder
{
    public const int MaximumInventoryFiles = 16384;

    public static ChangeReplayProof Verify(IChangeSnapshot oldBase, IChangeSnapshot original,
        IChangeSnapshot newBase, IChangeSnapshot replay, CancellationToken token)
    {
        if (new[] { oldBase, original, newBase, replay }.Any(snapshot => snapshot.Files.Count > MaximumInventoryFiles))
            throw new InvalidOperationException("Replay proof exceeded its 16,384-file per-snapshot bound; no evidence was truncated.");
        Dictionary<string, ChangeSnapshotFile> before = Index(oldBase), feature = Index(original),
            upstream = Index(newBase), result = Index(replay);
        int exact = 0, inherited = 0, conflicts = 0;
        foreach (string path in before.Keys.Concat(feature.Keys).Concat(upstream.Keys).Concat(result.Keys)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            ChangeSnapshotFile? b = before.GetValueOrDefault(path), o = feature.GetValueOrDefault(path),
                n = upstream.GetValueOrDefault(path), p = result.GetValueOrDefault(path);
            if (Same(b, o))
            {
                if (!Same(n, p)) throw new InvalidOperationException("Replay changed an original-untouched path instead of retaining the new upstream state.");
                if (!Same(b, n)) inherited++;
            }
            else if (Same(b, n) || Same(o, n))
            {
                if (!Same(o, p)) throw new InvalidOperationException("Replay failed an exact non-conflicting original delta proof.");
                exact++;
            }
            else conflicts++;
        }
        return new() { ExactReplayPathCount = exact, InheritedUpstreamPathCount = inherited, DeclaredConflictPathCount = conflicts };
    }

    private static Dictionary<string, ChangeSnapshotFile> Index(IChangeSnapshot snapshot) =>
        snapshot.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);

    private static bool Same(ChangeSnapshotFile? first, ChangeSnapshotFile? second) =>
        first is null ? second is null : second is not null && first.ObjectId == second.ObjectId && first.Mode == second.Mode;
}
