using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioTopology
{
    public static void MarkExactDuplicates(IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        foreach (IGrouping<string, ChangePortfolioItemDraft> duplicateGroup in drafts
            .Where(draft => draft.Effects.Count > 0)
            .GroupBy(draft => draft.PatchDigest, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            ChangePortfolioItemDraft keeper = duplicateGroup
                .OrderByDescending(draft => draft.Candidate.Report.TotalEffort.Expected)
                .ThenByDescending(draft => draft.Candidate.Report.TotalEffort.High)
                .ThenBy(draft => draft.Id, StringComparer.Ordinal)
                .First();
            foreach (ChangePortfolioItemDraft duplicate in duplicateGroup.Where(draft => draft != keeper))
            {
                duplicate.DuplicateOfItemId = keeper.Id;
                duplicate.UncertaintyReasons.Add(
                    "This exact represented patch duplicates another selected change and receives zero normalized allocation.");
            }
        }
    }

    public static HashSet<string> FindExactRevertedPaths(
        IReadOnlyDictionary<string, ChangePortfolioItemDraft[]> touches) =>
        touches.Where(pair => IsExactChronologicalRevert(pair.Key, pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

    public static void AddOverlapUncertainty(
        ChangePortfolioSelection selection,
        IReadOnlyDictionary<string, ChangePortfolioItemDraft[]> touches,
        HashSet<string> revertedPaths,
        HashSet<string> groupUncertainty)
    {
        foreach ((string path, ChangePortfolioItemDraft[] pathDrafts) in touches.Where(pair => pair.Value.Length > 1))
        {
            bool exactChain = FormsChronologicalChain(path, pathDrafts);
            string? reason = selection.Kind == ChangePortfolioSelectionKind.PullRequests
                ? OpposingEffects(path, pathDrafts)
                    ? "Opposing pull-request effects have no semantic application order; the path is retained once with attribution ambiguity."
                    : "Overlapping pull-request path effects are retained once by an order-independent maximum; causal credit is unresolved."
                : revertedPaths.Contains(path)
                    ? null
                    : exactChain
                        ? "Sequential selected commits touch the same path; final represented work is retained once."
                        : "Selected commits overlap without one exact object chain, usually because interleaved work shares the path; attribution is unresolved.";
            if (reason is null)
            {
                continue;
            }

            groupUncertainty.Add(reason);
            foreach (ChangePortfolioItemDraft draft in pathDrafts)
            {
                draft.UncertaintyReasons.Add(reason);
            }
        }
    }

    public static List<IReadOnlyList<ChangePortfolioItemDraft>> ConnectedComponents(
        IReadOnlyList<ChangePortfolioItemDraft> drafts,
        IReadOnlyDictionary<string, ChangePortfolioItemDraft[]> touches)
    {
        Dictionary<ChangePortfolioItemDraft, int> indexes = drafts
            .Select((draft, index) => (draft, index))
            .ToDictionary(entry => entry.draft, entry => entry.index);
        int[] parents = [.. Enumerable.Range(0, drafts.Count)];
        foreach (ChangePortfolioItemDraft[] pathDrafts in touches.Values)
        {
            if (pathDrafts.Length < 2)
            {
                continue;
            }

            int first = indexes[pathDrafts[0]];
            foreach (ChangePortfolioItemDraft draft in pathDrafts.Skip(1))
            {
                Union(parents, first, indexes[draft]);
            }
        }

        Dictionary<int, List<ChangePortfolioItemDraft>> components = [];
        foreach (ChangePortfolioItemDraft draft in drafts)
        {
            int root = Find(parents, indexes[draft]);
            if (!components.TryGetValue(root, out List<ChangePortfolioItemDraft>? component))
            {
                component = [];
                components.Add(root, component);
            }

            component.Add(draft);
        }

        return [.. components.Values
            .Select(component => (IReadOnlyList<ChangePortfolioItemDraft>)[.. component
                .OrderBy(draft => draft.Id, StringComparer.Ordinal)])
            .OrderBy(component => component[0].Id, StringComparer.Ordinal)];
    }

    private static bool IsExactChronologicalRevert(
        string path,
        ChangePortfolioItemDraft[] drafts) =>
        drafts.Length > 1 && FormsChronologicalChain(path, drafts) &&
        State(drafts.Order(ChronologicalComparer.Instance).First(), path).BaseState ==
        State(drafts.Order(ChronologicalComparer.Instance).Last(), path).HeadState;

    private static bool FormsChronologicalChain(
        string path,
        IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        if (drafts.Any(draft => draft.Candidate.Attribution.SelectedTimestamp is null))
        {
            return false;
        }

        ChangePortfolioItemDraft[] ordered = [.. drafts.Order(ChronologicalComparer.Instance)];
        return ordered.Zip(ordered.Skip(1), (left, right) =>
            State(left, path).HeadState == State(right, path).BaseState).All(value => value);
    }

    private static bool OpposingEffects(string path, IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        HashSet<(string? Base, string? Head)> seen = [];
        foreach (ChangePortfolioItemDraft draft in drafts)
        {
            ChangePortfolioPathEffect effect = State(draft, path);
            if (seen.Contains((effect.HeadState, effect.BaseState)))
            {
                return true;
            }

            seen.Add((effect.BaseState, effect.HeadState));
        }

        return false;
    }

    private static int Find(int[] parents, int item)
    {
        while (parents[item] != item)
        {
            parents[item] = parents[parents[item]];
            item = parents[item];
        }

        return item;
    }

    private static void Union(int[] parents, int left, int right)
    {
        int leftRoot = Find(parents, left);
        int rightRoot = Find(parents, right);
        if (leftRoot != rightRoot)
        {
            parents[rightRoot] = leftRoot;
        }
    }

    private static ChangePortfolioPathEffect State(ChangePortfolioItemDraft draft, string path) =>
        draft.Effects[path];

    private sealed class ChronologicalComparer : IComparer<ChangePortfolioItemDraft>
    {
        public static ChronologicalComparer Instance { get; } = new();

        public int Compare(ChangePortfolioItemDraft? left, ChangePortfolioItemDraft? right)
        {
            int timestamp = Nullable.Compare(
                left?.Candidate.Attribution.SelectedTimestamp,
                right?.Candidate.Attribution.SelectedTimestamp);
            return timestamp != 0
                ? timestamp
                : StringComparer.Ordinal.Compare(left?.Id, right?.Id);
        }
    }
}
