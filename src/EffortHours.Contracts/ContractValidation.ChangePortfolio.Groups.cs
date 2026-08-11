using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidatePortfolioGroups(
        ChangePortfolioReport report,
        HashSet<string> itemIds,
        List<string> errors)
    {
        HashSet<string> groupIds = new(StringComparer.Ordinal);
        HashSet<string> repositoryIds = new(StringComparer.Ordinal);
        HashSet<string> groupedItems = new(StringComparer.Ordinal);
        HashSet<string> adjustmentIds = report.Adjustments.Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> groupedAdjustments = new(StringComparer.Ordinal);
        foreach (ChangePortfolioRepositoryGroup group in report.RepositoryGroups)
        {
            RequireText(group.Id, "repositoryGroup.id", errors);
            RequireText(group.RepositoryId, $"repositoryGroup[{group.Id}].repositoryId", errors);
            ValidateRange(group.IsolatedEffort, $"repositoryGroup[{group.Id}].isolatedEffort", errors);
            ValidateRange(group.NormalizedEffort, $"repositoryGroup[{group.Id}].normalizedEffort", errors);
            RequireText(group.Assessment, $"repositoryGroup[{group.Id}].assessment", errors);
            RequireUniqueText(
                group.UncertaintyReasons,
                $"repositoryGroup[{group.Id}].uncertaintyReasons",
                errors);
            if (!groupIds.Add(group.Id))
            {
                errors.Add($"Portfolio repository group ID '{group.Id}' is duplicated.");
            }

            if (!repositoryIds.Add(group.RepositoryId))
            {
                errors.Add($"Portfolio repository ID '{group.RepositoryId}' has more than one group.");
            }

            foreach (string itemId in group.ItemIds)
            {
                if (!itemIds.Contains(itemId) || !groupedItems.Add(itemId))
                {
                    errors.Add($"Portfolio repository group '{group.Id}' has an invalid or repeated item reference.");
                }

                ChangePortfolioItemEstimate? item = report.Items.FirstOrDefault(candidate => candidate.Id == itemId);
                if (item is not null && item.RepositoryId != group.RepositoryId)
                {
                    errors.Add($"Portfolio repository group '{group.Id}' contains an item from another repository.");
                }
            }

            if (group.AdjustmentIds.Any(id => !adjustmentIds.Contains(id) || !groupedAdjustments.Add(id)))
            {
                errors.Add($"Portfolio repository group '{group.Id}' has an invalid or repeated adjustment reference.");
            }

            foreach (ChangePortfolioAdjustment adjustment in report.Adjustments.Where(adjustment =>
                group.AdjustmentIds.Contains(adjustment.Id, StringComparer.Ordinal)))
            {
                if (adjustment.ItemIds.Any(itemId =>
                    !group.ItemIds.Contains(itemId, StringComparer.Ordinal)))
                {
                    errors.Add($"Portfolio adjustment '{adjustment.Id}' crosses repository groups.");
                }
            }

            ValidatePortfolioCategories(
                group.Categories,
                group.NormalizedEffort,
                $"repositoryGroup[{group.Id}].categories",
                errors);
            EffortRange groupItems = Sum(report.Items
                .Where(item => group.ItemIds.Contains(item.Id, StringComparer.Ordinal))
                .Select(item => item.IsolatedEffort));
            if (groupItems != group.IsolatedEffort)
            {
                errors.Add($"Portfolio repository group '{group.Id}' isolated effort does not equal its item sum.");
            }

            ChangePortfolioOrderPolicy expectedOrder = report.Selection.Kind ==
                ChangePortfolioSelectionKind.AuthorPeriod
                ? ChangePortfolioOrderPolicy.ChronologicalSelectedCommits
                : ChangePortfolioOrderPolicy.OrderIndependent;
            if (group.OrderPolicy != expectedOrder)
            {
                errors.Add($"Portfolio repository group '{group.Id}' has an order policy inconsistent with the selector.");
            }

            ValidateBaseContexts(report, group, errors);
        }

        if (!groupedItems.SetEquals(itemIds))
        {
            errors.Add("Portfolio repository groups do not cover item rows exactly once.");
        }

        if (!groupedAdjustments.SetEquals(adjustmentIds))
        {
            errors.Add("Portfolio repository groups do not cover adjustments exactly once.");
        }

        if (Sum(report.RepositoryGroups.Select(group => group.IsolatedEffort)) != report.IsolatedEffort ||
            Sum(report.RepositoryGroups.Select(group => group.NormalizedEffort)) != report.TotalEffort)
        {
            errors.Add("Portfolio repository-group totals do not reconcile with report totals.");
        }

        Dictionary<EffortCategory, EffortRange> groupedCategories = report.RepositoryGroups
            .SelectMany(group => group.Categories)
            .GroupBy(category => category.Category)
            .ToDictionary(group => group.Key, group => Sum(group.Select(category => category.Hours)));
        Dictionary<EffortCategory, EffortRange> reportCategories = report.Categories
            .GroupBy(category => category.Category)
            .ToDictionary(group => group.Key, group => Sum(group.Select(category => category.Hours)));
        if (groupedCategories.Count != reportCategories.Count || groupedCategories.Any(pair =>
            !reportCategories.TryGetValue(pair.Key, out EffortRange? hours) || hours != pair.Value))
        {
            errors.Add("Portfolio categories do not equal the repository-group category ledger.");
        }
    }

    private static void ValidateBaseContexts(
        ChangePortfolioReport report,
        ChangePortfolioRepositoryGroup group,
        List<string> errors)
    {
        HashSet<string> contextItems = [];
        HashSet<string> contextIds = new(StringComparer.Ordinal);
        foreach (ChangePortfolioBaseContext context in group.BaseContexts)
        {
            RequireText(context.Id, "baseContext.id", errors);
            RequireText(context.BaseObjectId, $"baseContext[{context.Id}].baseObjectId", errors);
            if (!contextIds.Add(context.Id))
            {
                errors.Add($"Portfolio repository group '{group.Id}' repeats base context '{context.Id}'.");
            }

            foreach (string itemId in context.ItemIds)
            {
                if (!group.ItemIds.Contains(itemId, StringComparer.Ordinal) || !contextItems.Add(itemId))
                {
                    errors.Add($"Base context '{context.Id}' has an invalid or repeated item reference.");
                }

                ChangePortfolioItemEstimate? item = report.Items.FirstOrDefault(candidate => candidate.Id == itemId);
                if (item is not null && (item.BaseContextId != context.Id ||
                    item.Selection.Base.ObjectId != context.BaseObjectId))
                {
                    errors.Add($"Base context '{context.Id}' does not match item '{itemId}' immutable base identity.");
                }
            }
        }

        if (!contextItems.SetEquals(group.ItemIds))
        {
            errors.Add($"Portfolio repository group '{group.Id}' base contexts do not cover its items exactly.");
        }
    }
}
