using EffortHours.Contracts.V1;

namespace EffortHours.Change;

// Acyclic blob-state chains establish final effects without treating dates as
// causal order. Merge edges may repeat an exactly retained non-merge chain.
internal static class ChangePortfolioRawEffectGraph
{
    public static bool TryCompose(IReadOnlyList<ChangePortfolioCandidate> candidates,
        out Dictionary<string, ChangePortfolioPathEffect> effects, out string? rejectedPath)
    {
        effects = new(StringComparer.Ordinal);
        rejectedPath = null;
        var paths = new Dictionary<string, List<(ChangePortfolioPathEffect Effect, bool Merge)>>(StringComparer.Ordinal);
        int transitions = 0;
        foreach (ChangePortfolioCandidate candidate in candidates)
        {
            foreach (ChangePathEvidence path in candidate.Report.Evidence.Paths)
            {
                if (++transitions > 1_000_000 || paths.Count > 16_384)
                    return false;
                if (path.Status == ChangePathStatus.Moved && path.PreviousPath is not null)
                {
                    Add(path.PreviousPath, path.BaseObjectId, null, candidate.Attribution.ParentCount > 1);
                    Add(path.Path, null, path.HeadObjectId, candidate.Attribution.ParentCount > 1);
                }
                else Add(path.Path, path.BaseObjectId, path.HeadObjectId, candidate.Attribution.ParentCount > 1);
            }
        }
        if (paths.Count > 16_384) return false;
        int steps = 0;
        foreach (var (path, entries) in paths.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            rejectedPath = path;
            ChangePortfolioPathEffect[] retained = [.. entries.Where(entry => !entry.Merge).Select(entry => entry.Effect)];
            var retainedByBase = retained.GroupBy(edge => edge.BaseState ?? "<absent>", StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            List<ChangePortfolioPathEffect> edges = [.. retained];
            foreach (var (Effect, Merge) in entries.Where(entry => entry.Merge))
            {
                if (!Covered(Effect, retainedByBase, ref steps)) edges.Add(Effect);
                if (steps > 1_000_000) return false;
            }
            // Repeated non-merge effects can be reintroduction after unselected
            // removal. Never erase them as graph duplicates.
            if (edges.Distinct().Count() != edges.Count || !Chain(edges, out var composed))
                return false;
            if (composed is not null) effects[path] = composed;
        }
        rejectedPath = null;
        return true;

        void Add(string path, string? before, string? after, bool merge)
        {
            if (!paths.TryGetValue(path, out var entries)) paths[path] = entries = [];
            if (before != after) entries.Add((new(path, before, after), merge));
        }
    }

    private static bool Covered(ChangePortfolioPathEffect merge,
        Dictionary<string, ChangePortfolioPathEffect[]> retained, ref int steps)
    {
        string? state = merge.BaseState;
        HashSet<string> visited = new(StringComparer.Ordinal);
        for (int step = 0; step < retained.Count; step++)
        {
            if (!visited.Add(state ?? "<absent>")) return false;
            if (++steps > 1_000_000 || !retained.TryGetValue(state ?? "<absent>", out var next) || next.Length != 1) return false;
            state = next[0].HeadState;
            if (state == merge.HeadState) return true;
        }
        return false;
    }

    private static bool Chain(List<ChangePortfolioPathEffect> edges, out ChangePortfolioPathEffect? composed)
    {
        composed = null;
        if (edges.Count == 0) return true;
        var byBase = edges.GroupBy(edge => edge.BaseState ?? "<absent>", StringComparer.Ordinal);
        if (byBase.Any(group => group.Count() != 1) ||
            edges.GroupBy(edge => edge.HeadState ?? "<absent>", StringComparer.Ordinal).Any(group => group.Count() != 1))
            return false;
        HashSet<string?> heads = [.. edges.Select(edge => edge.HeadState)];
        ChangePortfolioPathEffect[] roots = [.. edges.Where(edge => !heads.Contains(edge.BaseState))];
        if (roots.Length != 1) return false;
        var next = edges.ToDictionary(edge => edge.BaseState ?? "<absent>", StringComparer.Ordinal);
        ChangePortfolioPathEffect current = roots[0];
        int count = 1;
        while (next.TryGetValue(current.HeadState ?? "<absent>", out var successor))
        {
            if (++count > edges.Count) return false;
            current = successor;
        }
        if (count != edges.Count) return false;
        composed = roots[0] with { HeadState = current.HeadState };
        return true;
    }
}
