using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioAllocation
{
    public static void Assign(
        IReadOnlyList<ChangePortfolioItemDraft> drafts,
        IReadOnlyList<IReadOnlyList<ChangePortfolioItemDraft>> components,
        IReadOnlyList<EffortRange> componentTotals,
        HashSet<string> revertedPaths,
        HashSet<EffortCategory> sharedCategories,
        decimal normalizedExpected)
    {
        foreach (ChangePortfolioItemDraft draft in drafts)
        {
            draft.AllocationWeight = 0m;
            draft.AllocatedExpectedHours = 0m;
        }

        decimal[] componentAllocations = AllocateExpected(
            [.. componentTotals.Select(total => total.Expected)],
            normalizedExpected,
            [.. components.Select(ComponentId)]);
        for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
        {
            IReadOnlyList<ChangePortfolioItemDraft> component = components[componentIndex];
            SetWeights(component, revertedPaths, sharedCategories);
            decimal[] itemAllocations = AllocateExpected(
                [.. component.Select(draft => draft.AllocationWeight)],
                componentAllocations[componentIndex],
                [.. component.Select(draft => draft.Id)]);
            for (int itemIndex = 0; itemIndex < component.Count; itemIndex++)
            {
                component[itemIndex].AllocatedExpectedHours = itemAllocations[itemIndex];
            }
        }
    }

    private static void SetWeights(
        IReadOnlyList<ChangePortfolioItemDraft> component,
        HashSet<string> revertedPaths,
        HashSet<EffortCategory> sharedCategories)
    {
        foreach (ChangePortfolioItemDraft draft in component)
        {
            draft.AllocationWeight = 0m;
        }

        EffortCategory[] categories = [.. component
            .SelectMany(draft => draft.Candidate.Report.Categories)
            .Select(category => category.Category)
            .Distinct()
            .Order()];
        foreach (EffortCategory category in categories)
        {
            if (sharedCategories.Contains(category))
            {
                IReadOnlyList<ChangePortfolioItemDraft> winners = Winners(
                    component,
                    draft => Category(draft, category));
                if (winners.Count > 0)
                {
                    decimal share = Category(winners[0], category) / winners.Count;
                    foreach (ChangePortfolioItemDraft winner in winners)
                    {
                        winner.AllocationWeight += share;
                    }
                }

                continue;
            }

            foreach (ChangePortfolioItemDraft draft in component)
            {
                draft.AllocationWeight += PathlessCategory(draft, category);
            }

            string[] paths = [.. component.SelectMany(draft => draft.Effects.Keys)
                .Where(path => !revertedPaths.Contains(path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            foreach (string path in paths)
            {
                IReadOnlyList<ChangePortfolioItemDraft> winners = Winners(
                    component,
                    draft => PathCategory(draft, category, path));
                if (winners.Count == 0)
                {
                    continue;
                }

                decimal share = PathCategory(winners[0], category, path) / winners.Count;
                foreach (ChangePortfolioItemDraft winner in winners)
                {
                    winner.AllocationWeight += share;
                }
            }
        }
    }

    private static IReadOnlyList<ChangePortfolioItemDraft> Winners(
        IReadOnlyList<ChangePortfolioItemDraft> component,
        Func<ChangePortfolioItemDraft, decimal> value)
    {
        decimal maximum = component.Max(value);
        return maximum <= 0m
            ? []
            : [.. component.Where(draft => value(draft) == maximum)];
    }

    private static decimal Category(ChangePortfolioItemDraft draft, EffortCategory category) =>
        draft.Candidate.Report.Categories.FirstOrDefault(item => item.Category == category)?
            .Hours.Expected ?? 0m;

    private static decimal PathCategory(
        ChangePortfolioItemDraft draft,
        EffortCategory category,
        string path) => draft.PathCategoryHours.GetValueOrDefault((category, path))?.Expected ?? 0m;

    private static decimal PathlessCategory(
        ChangePortfolioItemDraft draft,
        EffortCategory category) => draft.PathlessCategoryHours.GetValueOrDefault(category)?.Expected ?? 0m;

    private static string ComponentId(IReadOnlyList<ChangePortfolioItemDraft> component) =>
        string.Join('|', component.Select(draft => draft.Id).Order(StringComparer.Ordinal));

    private static decimal[] AllocateExpected(decimal[] weights, decimal normalized, string[] ids)
    {
        decimal[] allocations = new decimal[weights.Length];
        if (normalized == 0m || weights.Length == 0)
        {
            return allocations;
        }

        decimal total = weights.Sum();
        int[] eligible = [.. Enumerable.Range(0, weights.Length).Where(index => weights[index] > 0m)];
        if (eligible.Length == 0)
        {
            eligible = [.. Enumerable.Range(0, weights.Length)];
        }

        decimal[] remainders = new decimal[weights.Length];
        foreach (int index in eligible)
        {
            decimal raw = total > 0m
                ? normalized * weights[index] / total
                : normalized / eligible.Length;
            allocations[index] = decimal.Floor(raw * 100m) / 100m;
            remainders[index] = raw - allocations[index];
        }

        decimal residual = normalized - allocations.Sum();
        foreach (int index in eligible.OrderByDescending(index => remainders[index])
            .ThenBy(index => ids[index], StringComparer.Ordinal))
        {
            if (residual <= 0m)
            {
                break;
            }

            decimal increment = Math.Min(0.01m, residual);
            allocations[index] += increment;
            residual -= increment;
        }

        if (residual > 0m)
        {
            allocations[eligible[0]] += residual;
        }

        return allocations;
    }
}
