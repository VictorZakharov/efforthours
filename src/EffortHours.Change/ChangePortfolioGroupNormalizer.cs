using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioGroupNormalizer
{
    private static readonly HashSet<EffortCategory> SharedCategories =
    [
        EffortCategory.SpecificationComprehensionAndDomainLearning,
        EffortCategory.RepositoryAndSolutionSetup,
        EffortCategory.ArchitectureAndTechnicalDesign,
        EffortCategory.ManualValidationDebuggingAndHardening,
        EffortCategory.SelfReviewAndSystemIntegration,
    ];

    public static ChangePortfolioGroupResult Normalize(
        ChangePortfolioSelection selection,
        string repositoryId,
        IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        if (selection.Kind == ChangePortfolioSelectionKind.PullRequests)
        {
            ChangePortfolioTopology.MarkExactDuplicates(drafts);
        }
        ChangePortfolioItemDraft[] active = [.. drafts.Where(draft => draft.DuplicateOfItemId is null)];
        Dictionary<string, ChangePortfolioItemDraft[]> touches = active
            .SelectMany(draft => draft.Effects.Keys.Select(path => (Path: path, Draft: draft)))
            .GroupBy(value => value.Path, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.Draft).Distinct().ToArray(),
                StringComparer.Ordinal);
        HashSet<string> overlapPaths = touches
            .Where(pair => pair.Value.Length > 1)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> revertedPaths = selection.Kind == ChangePortfolioSelectionKind.AuthorPeriod
            ? ChangePortfolioTopology.FindExactRevertedPaths(touches)
            : [];

        HashSet<string> groupUncertainty = new(StringComparer.Ordinal);
        if (drafts.Select(draft => draft.BaseContextId).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            groupUncertainty.Add(
                "The selected changes use multiple immutable base contexts; cross-context path attribution is structural rather than a replayed merge.");
        }

        AddAttributionUncertainty(active, groupUncertainty);
        ChangePortfolioTopology.AddOverlapUncertainty(
            selection,
            touches,
            revertedPaths,
            groupUncertainty);
        IReadOnlyList<IReadOnlyList<ChangePortfolioItemDraft>> components =
            ChangePortfolioTopology.ConnectedComponents(active, touches);
        Dictionary<EffortCategory, EffortRange> normalizedByCategory = [];
        List<EffortRange> componentTotals = [];
        foreach (IReadOnlyList<ChangePortfolioItemDraft> component in components)
        {
            Dictionary<EffortCategory, EffortRange> componentCategories =
                NormalizeComponent(component, revertedPaths);
            foreach ((EffortCategory category, EffortRange hours) in componentCategories)
            {
                Add(normalizedByCategory, category, hours);
            }

            componentTotals.Add(ContractValidation.Sum(componentCategories.Values));
        }

        CategoryEstimate[] categories = [.. normalizedByCategory
            .Where(pair => pair.Value.Low != 0m || pair.Value.Expected != 0m || pair.Value.High != 0m)
            .OrderBy(pair => pair.Key)
            .Select(pair => new CategoryEstimate { Category = pair.Key, Hours = Round(pair.Value) })];
        EffortRange normalized = ContractValidation.Sum(categories.Select(category => category.Hours));
        EffortRange isolated = ContractValidation.Sum(drafts.Select(draft => draft.Candidate.Report.TotalEffort));
        ChangePortfolioAllocation.Assign(
            drafts,
            components,
            componentTotals,
            revertedPaths,
            SharedCategories,
            normalized.Expected);

        List<ChangePortfolioAdjustmentCause> causes = BuildCauses(
            drafts,
            active,
            overlapPaths,
            revertedPaths,
            categories);
        IReadOnlyList<ChangePortfolioAdjustment> adjustments = ChangePortfolioAdjustmentBuilder.Build(
            repositoryId,
            isolated,
            normalized,
            causes,
            [.. drafts.Select(draft => draft.Id)]);
        decimal tolerance = Math.Max(1m, decimal.Round(
            isolated.Expected * 0.10m,
            2,
            MidpointRounding.AwayFromZero));
        decimal difference = normalized.Expected - isolated.Expected;
        string assessment = difference < -tolerance
            ? "normalized-below-isolated-items"
            : difference > tolerance
                ? "normalized-above-isolated-items"
                : "within-structural-tolerance";
        ChangePortfolioRepositoryGroup group = new()
        {
            Id = ChangePortfolioIdentity.RepositoryGroupId(repositoryId),
            RepositoryId = repositoryId,
            OrderPolicy = selection.Kind == ChangePortfolioSelectionKind.AuthorPeriod
                ? ChangePortfolioOrderPolicy.ChronologicalSelectedCommits
                : ChangePortfolioOrderPolicy.OrderIndependent,
            BaseContexts = [.. drafts
                .GroupBy(draft => draft.Candidate.Report.Selection.Base.ObjectId, StringComparer.Ordinal)
                .OrderBy(context => context.Key, StringComparer.Ordinal)
                .Select(context => new ChangePortfolioBaseContext
                {
                    Id = ChangePortfolioIdentity.BaseContextId(repositoryId, context.Key),
                    BaseObjectId = context.Key,
                    ItemIds = [.. context.Select(draft => draft.Id).Order(StringComparer.Ordinal)],
                })],
            ItemIds = [.. drafts.Select(draft => draft.Id).Order(StringComparer.Ordinal)],
            IsolatedEffort = isolated,
            NormalizedEffort = normalized,
            Categories = categories,
            Assessment = assessment,
            AdjustmentIds = [.. adjustments.Select(adjustment => adjustment.Id)],
            UncertaintyReasons = [.. groupUncertainty.Order(StringComparer.Ordinal)],
        };
        return new ChangePortfolioGroupResult(group, adjustments);
    }

    private static void AddAttributionUncertainty(
        IEnumerable<ChangePortfolioItemDraft> drafts,
        HashSet<string> groupUncertainty)
    {
        foreach (ChangePortfolioItemDraft draft in drafts)
        {
            foreach (string reason in draft.Candidate.Attribution.AmbiguityReasons)
            {
                draft.UncertaintyReasons.Add(reason);
                groupUncertainty.Add(reason);
            }
        }
    }

    private static Dictionary<EffortCategory, EffortRange> NormalizeComponent(
        IReadOnlyList<ChangePortfolioItemDraft> component,
        HashSet<string> revertedPaths)
    {
        Dictionary<EffortCategory, EffortRange> totals = [];
        bool exactNetZero = component.Count > 1 &&
            component.All(draft => draft.Effects.Count > 0 &&
                draft.Effects.Keys.All(revertedPaths.Contains));
        if (exactNetZero)
        {
            return totals;
        }

        if (component.Count == 1)
        {
            foreach (CategoryEstimate category in component[0].Candidate.Report.Categories)
            {
                Add(totals, category.Category, category.Hours);
            }

            return totals;
        }

        EffortCategory[] categories = [.. component
            .SelectMany(draft => draft.Candidate.Report.Categories)
            .Select(category => category.Category)
            .Distinct()
            .Order()];
        foreach (EffortCategory category in categories)
        {
            if (SharedCategories.Contains(category))
            {
                Add(totals, category, Max(component.Select(draft => Category(draft, category))));
                continue;
            }

            EffortRange normalized = Zero();
            foreach (ChangePortfolioItemDraft draft in component)
            {
                normalized = Add(normalized, PathlessCategory(draft, category));
            }

            string[] paths = [.. component.SelectMany(draft => draft.Effects.Keys)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            foreach (string path in paths.Where(path => !revertedPaths.Contains(path)))
            {
                normalized = Add(normalized, Max(component
                    .Where(draft => draft.Effects.ContainsKey(path))
                    .Select(draft => PathCategory(draft, category, path))));
            }

            Add(totals, category, normalized);
        }

        return totals;
    }

    private static List<ChangePortfolioAdjustmentCause> BuildCauses(
        IReadOnlyList<ChangePortfolioItemDraft> drafts,
        IReadOnlyList<ChangePortfolioItemDraft> active,
        HashSet<string> overlapPaths,
        HashSet<string> revertedPaths,
        IReadOnlyList<CategoryEstimate> normalizedCategories)
    {
        List<ChangePortfolioAdjustmentCause> causes = [];
        ChangePortfolioItemDraft[] duplicates = [.. drafts.Where(draft => draft.DuplicateOfItemId is not null)];
        if (duplicates.Length > 0)
        {
            causes.Add(new ChangePortfolioAdjustmentCause(
                ChangePortfolioAdjustmentKind.ExactDuplicate,
                Math.Max(0.01m, duplicates.Sum(draft => draft.Candidate.Report.TotalEffort.Expected)),
                "Exact represented patch identities, including cherry-picked equivalents, are counted once.",
                [.. duplicates.SelectMany(draft => new[] { draft.Id, draft.DuplicateOfItemId! }).Distinct().Order(StringComparer.Ordinal)],
                duplicates.SelectMany(draft => draft.Effects.Keys).Distinct(StringComparer.Ordinal).Count()));
        }

        if (overlapPaths.Count > 0)
        {
            causes.Add(new ChangePortfolioAdjustmentCause(
                ChangePortfolioAdjustmentKind.Overlap,
                overlapPaths.Count,
                "Overlapping represented path contributions are normalized once per repository.",
                [.. active.Where(draft => draft.Effects.Keys.Any(overlapPaths.Contains)).Select(draft => draft.Id).Order(StringComparer.Ordinal)],
                overlapPaths.Count));
        }

        if (revertedPaths.Count > 0)
        {
            causes.Add(new ChangePortfolioAdjustmentCause(
                ChangePortfolioAdjustmentKind.Revert,
                revertedPaths.Count,
                "Exact chronological object chains returning to their initial state are retained in the isolated ledger but excluded from normalized final effects.",
                [.. active.Where(draft => draft.Effects.Keys.Any(revertedPaths.Contains)).Select(draft => draft.Id).Order(StringComparer.Ordinal)],
                revertedPaths.Count));
        }

        decimal activeShared = active.Sum(draft => draft.Candidate.Report.Categories
            .Where(category => SharedCategories.Contains(category.Category))
            .Sum(category => category.Hours.Expected));
        decimal normalizedShared = normalizedCategories
            .Where(category => SharedCategories.Contains(category.Category))
            .Sum(category => category.Hours.Expected);
        decimal sharedExcess = Math.Max(0m, activeShared - normalizedShared);
        if (sharedExcess > 0m)
        {
            string[] sharedItemIds = [.. active
                .Where(draft => draft.Effects.Keys.Any(overlapPaths.Contains))
                .Select(draft => draft.Id)
                .Order(StringComparer.Ordinal)];
            causes.Add(new ChangePortfolioAdjustmentCause(
                ChangePortfolioAdjustmentKind.SharedContext,
                sharedExcess,
                "Specification, setup, validation, and review context shared by overlapping changes is represented once.",
                sharedItemIds,
                overlapPaths.Count));
        }

        return causes;
    }

    private static EffortRange Category(ChangePortfolioItemDraft draft, EffortCategory category) =>
        draft.Candidate.Report.Categories.FirstOrDefault(item => item.Category == category)?.Hours ??
        Zero();

    private static EffortRange PathCategory(
        ChangePortfolioItemDraft draft,
        EffortCategory category,
        string path) => draft.PathCategoryHours.GetValueOrDefault((category, path)) ?? Zero();

    private static EffortRange PathlessCategory(
        ChangePortfolioItemDraft draft,
        EffortCategory category) => draft.PathlessCategoryHours.GetValueOrDefault(category) ?? Zero();

    private static void Add(
        Dictionary<EffortCategory, EffortRange> totals,
        EffortCategory category,
        EffortRange value) => totals[category] = totals.TryGetValue(category, out EffortRange? current)
            ? Add(current, value)
            : value;

    private static EffortRange Add(EffortRange left, EffortRange right) => new()
    {
        Low = left.Low + right.Low,
        Expected = left.Expected + right.Expected,
        High = left.High + right.High,
    };

    private static EffortRange Max(IEnumerable<EffortRange> ranges)
    {
        EffortRange[] values = [.. ranges];
        return values.Length == 0 ? Zero() : new EffortRange
        {
            Low = values.Max(range => range.Low),
            Expected = values.Max(range => range.Expected),
            High = values.Max(range => range.High),
        };
    }

    private static EffortRange Round(EffortRange value) => new()
    {
        Low = decimal.Round(value.Low, 2, MidpointRounding.AwayFromZero),
        Expected = decimal.Round(value.Expected, 2, MidpointRounding.AwayFromZero),
        High = decimal.Round(value.High, 2, MidpointRounding.AwayFromZero),
    };

    private static EffortRange Zero() => new() { Low = 0m, Expected = 0m, High = 0m };

}
