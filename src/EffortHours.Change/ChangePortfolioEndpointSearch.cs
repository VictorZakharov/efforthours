using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioEndpointSearch
{
    internal sealed record Pair(ChangeSnapshotReference Before, ChangeSnapshotReference After,
        IChangeSnapshot BaseSnapshot, IChangeSnapshot HeadSnapshot);

    internal sealed record Result(Pair? Pair, string Code, string? Path);

    public static async Task<Result> FindAsync(string repository, IReadOnlyList<ChangePortfolioCandidate> active,
        IReadOnlyDictionary<string, ChangePortfolioPathEffect> effects,
        Func<string, string, CancellationToken, Task<IChangeSnapshot>> open,
        ChangePathAdmission? admission, CancellationToken cancellationToken)
    {
        // First retain the inexpensive chronological pair, then search at most
        // eight distinct bases and heads (64 complete-inventory comparisons).
        HashSet<string> selectedHeads = [.. active.Select(candidate => candidate.Report.Selection.Head.ObjectId)];
        HashSet<string> selectedBases = [.. active.Select(candidate => candidate.Report.Selection.Base.ObjectId)];
        ChangeSnapshotReference[] bases = [.. active.Select(candidate => candidate.Report.Selection.Base)
            .Where(reference => reference.ObjectId == active[0].Report.Selection.Base.ObjectId || !selectedHeads.Contains(reference.ObjectId))
            .DistinctBy(reference => reference.ObjectId)];
        ChangeSnapshotReference[] heads = [.. active.Reverse().Select(candidate => candidate.Report.Selection.Head)
            .Where(reference => reference.ObjectId == active[^1].Report.Selection.Head.ObjectId || !selectedBases.Contains(reference.ObjectId))
            .DistinctBy(reference => reference.ObjectId)];
        string code = "inventory-mismatch";
        string? path = null;
        foreach (ChangeSnapshotReference before in bases.Take(bases.Length > 8 || heads.Length > 8 ? 1 : 8))
        {
            foreach (ChangeSnapshotReference after in heads.Take(bases.Length > 8 || heads.Length > 8 ? 1 : 8))
            {
                cancellationToken.ThrowIfCancellationRequested();
                IChangeSnapshot left = await open(repository, before.ObjectId, cancellationToken).ConfigureAwait(false);
                IChangeSnapshot? right = null;
                bool keep = false;
                try
                {
                    right = await open(repository, after.ObjectId, cancellationToken).ConfigureAwait(false);
                    if (left.ObjectId != before.ObjectId || right.ObjectId != after.ObjectId)
                        throw new InvalidOperationException("Final-delta snapshot identity does not match its immutable selector.");
                    if (ChangePortfolioFinalDeltaProof.MatchesInventories(effects, left, right, admission, out code, out path))
                    {
                        keep = true;
                        return new(new(before, after, left, right), "proven", null);
                    }
                }
                finally
                {
                    if (!keep)
                    {
                        if (right is not null) await right.DisposeAsync().ConfigureAwait(false);
                        await left.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
        return new(null, bases.Length > 8 || heads.Length > 8 ? "anchor-bound" : code, path);
    }
}
