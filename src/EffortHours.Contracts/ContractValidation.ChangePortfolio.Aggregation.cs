using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidatePortfolioAggregation(
        ChangePortfolioReport report,
        HashSet<string> itemIds,
        List<string> errors)
    {
        bool supportsAggregation = report.Selection.ManifestBased &&
            report.Selection.Kind == ChangePortfolioSelectionKind.AuthorPeriod &&
            report.Selection.AuthorPeriodManifest is not null;
        if (!supportsAggregation)
        {
            if (report.Aggregation is not null)
            {
                errors.Add("Only a manifest author-period portfolio may contain aggregate attribution.");
            }

            return;
        }

        if (report.Aggregation is null)
        {
            if (!report.EstimatorVersion.StartsWith(
                "change-portfolio/0.1.0+",
                StringComparison.Ordinal))
            {
                errors.Add("This manifest author-period portfolio requires aggregate attribution.");
            }

            return;
        }

        ChangePortfolioAggregation aggregation = report.Aggregation;
        ChangePortfolioAuthorPeriodManifestSelection manifest = report.Selection.AuthorPeriodManifest!;
        if (aggregation.Policy != ChangePortfolioAggregationPolicies.ExclusiveMatchSetsV1)
        {
            errors.Add("Portfolio aggregation uses an unsupported allocation policy.");
        }

        HashSet<string> groupIds = ValidateContributorGroups(
            report,
            aggregation.ContributorGroups,
            manifest,
            itemIds,
            errors);
        ValidateContributorSummaries(
            report,
            aggregation.Contributors,
            aggregation.ContributorGroups,
            manifest,
            groupIds,
            errors);
        ValidateAggregationRepositories(
            report,
            aggregation.Repositories,
            manifest,
            itemIds,
            errors);
    }

    private static HashSet<string> ValidateContributorGroups(
        ChangePortfolioReport report,
        IReadOnlyList<ChangePortfolioContributorGroup> groups,
        ChangePortfolioAuthorPeriodManifestSelection manifest,
        HashSet<string> reportItemIds,
        List<string> errors)
    {
        HashSet<string> groupIds = new(StringComparer.Ordinal);
        HashSet<string> coveredItems = new(StringComparer.Ordinal);
        HashSet<string> contributorIds = manifest.ContributorIds.ToHashSet(StringComparer.Ordinal);
        List<string> keys = [];
        foreach (ChangePortfolioContributorGroup group in groups)
        {
            RequireText(group.Id, "aggregation.contributorGroup.id", errors);
            if (!groupIds.Add(group.Id))
            {
                errors.Add($"Contributor group ID '{group.Id}' is duplicated.");
            }

            if (group.ContributorIds.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumContributors ||
                group.ContributorIds.Distinct(StringComparer.Ordinal).Count() != group.ContributorIds.Count ||
                group.ContributorIds.Any(id => !contributorIds.Contains(id)) ||
                !PortfolioCanonical(group.ContributorIds))
            {
                errors.Add($"Contributor group '{group.Id}' has an invalid contributor set.");
            }

            ChangePortfolioContributorGroupKind expectedKind = group.ContributorIds.Count == 1
                ? ChangePortfolioContributorGroupKind.SingleContributor
                : ChangePortfolioContributorGroupKind.SharedContributors;
            if (!Enum.IsDefined(group.Kind) || group.Kind != expectedKind)
            {
                errors.Add($"Contributor group '{group.Id}' has an inconsistent kind.");
            }

            keys.Add(PortfolioSetKey(group.ContributorIds));
            ValidateContributorGroupItems(report, group, reportItemIds, coveredItems, errors);
            ValidateRange(group.IsolatedEffort, $"contributorGroup[{group.Id}].isolatedEffort", errors);
            ValidateRange(group.NormalizedEffort, $"contributorGroup[{group.Id}].normalizedEffort", errors);
            ValidatePortfolioDelta(
                group.ReconciliationDelta,
                group.NormalizedEffort,
                group.IsolatedEffort,
                $"Contributor group '{group.Id}'",
                errors);
            RequireUniqueText(
                group.UncertaintyReasons,
                $"contributorGroup[{group.Id}].uncertaintyReasons",
                errors);
            if (!PortfolioCanonical(group.UncertaintyReasons))
            {
                errors.Add($"Contributor group '{group.Id}' uncertainty reasons are not canonical.");
            }

            ValidateContributorRepositoryAllocations(report, group, errors);
        }

        if (!keys.SequenceEqual(keys.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
        {
            errors.Add("Contributor groups must use unique canonical contributor-set order.");
        }

        foreach (string contributorId in manifest.ContributorIds)
        {
            if (groups.Count(group => group.ContributorIds.Count == 1 &&
                    group.ContributorIds[0] == contributorId) != 1)
            {
                errors.Add($"Contributor '{contributorId}' requires exactly one single-contributor group.");
            }
        }

        if (!coveredItems.SetEquals(reportItemIds))
        {
            errors.Add("Contributor groups do not cover portfolio items exactly once.");
        }

        if (Sum(groups.Select(group => group.IsolatedEffort)) != report.IsolatedEffort ||
            Sum(groups.Select(group => group.NormalizedEffort)) != report.TotalEffort)
        {
            errors.Add("Contributor-group totals do not reconcile with the portfolio total.");
        }

        return groupIds;
    }

    private static void ValidateContributorGroupItems(
        ChangePortfolioReport report,
        ChangePortfolioContributorGroup group,
        HashSet<string> reportItemIds,
        HashSet<string> coveredItems,
        List<string> errors)
    {
        Dictionary<string, ChangePortfolioItemEstimate> itemsById =
            PortfolioItemIndex(report.Items);
        RequireUniqueText(group.ItemIds, $"contributorGroup[{group.Id}].itemIds", errors);
        if (!PortfolioCanonical(group.ItemIds))
        {
            errors.Add($"Contributor group '{group.Id}' item references are not canonical.");
        }

        List<ChangePortfolioItemEstimate> items = [];
        foreach (string itemId in group.ItemIds)
        {
            itemsById.TryGetValue(itemId, out ChangePortfolioItemEstimate? item);
            if (!reportItemIds.Contains(itemId) || !coveredItems.Add(itemId) || item is null ||
                PortfolioSetKey(PortfolioContributorIds(item)) != PortfolioSetKey(group.ContributorIds))
            {
                errors.Add($"Contributor group '{group.Id}' has an invalid or repeated item reference.");
            }

            if (item is not null)
            {
                items.Add(item);
            }
        }

        ValidatePortfolioCounts(
            [group.SelectedCommitCount, group.DirectAuthorMatchCount, group.CoauthorMatchCount],
            $"Contributor group '{group.Id}'",
            errors);
        if (group.SelectedCommitCount != group.ItemIds.Count ||
            group.DirectAuthorMatchCount != PortfolioMatchCount(
                items,
                ChangePortfolioContributorMatchKind.DirectAuthor) ||
            group.CoauthorMatchCount != PortfolioMatchCount(
                items,
                ChangePortfolioContributorMatchKind.Coauthor) ||
            Sum(items.Select(item => item.IsolatedEffort)) != group.IsolatedEffort)
        {
            errors.Add($"Contributor group '{group.Id}' counts or isolated effort are inconsistent.");
        }

        if (group.ItemIds.Count == 0 && group.Kind != ChangePortfolioContributorGroupKind.SingleContributor)
        {
            errors.Add($"Empty contributor group '{group.Id}' must be a requested single-contributor zero row.");
        }
    }

    private static void ValidateContributorRepositoryAllocations(
        ChangePortfolioReport report,
        ChangePortfolioContributorGroup group,
        List<string> errors)
    {
        HashSet<string> allocationIds = new(StringComparer.Ordinal);
        HashSet<string> repositoryIds = new(StringComparer.Ordinal);
        HashSet<string> allocatedItems = new(StringComparer.Ordinal);
        HashSet<string> groupItemIds = group.ItemIds.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioItemEstimate> itemsById =
            PortfolioItemIndex(report.Items);
        Dictionary<string, ChangePortfolioAdjustment> adjustmentsById =
            PortfolioAdjustmentIndex(report.Adjustments);
        foreach (ChangePortfolioContributorRepositoryAllocation allocation in group.RepositoryAllocations)
        {
            RequireText(allocation.Id, "contributorRepositoryAllocation.id", errors);
            RequireText(
                allocation.RepositoryId,
                $"contributorRepositoryAllocation[{allocation.Id}].repositoryId",
                errors);
            RequireText(
                allocation.RepositoryGroupId,
                $"contributorRepositoryAllocation[{allocation.Id}].repositoryGroupId",
                errors);
            if (!allocationIds.Add(allocation.Id) || !repositoryIds.Add(allocation.RepositoryId))
            {
                errors.Add($"Contributor group '{group.Id}' repeats a repository allocation.");
            }

            ChangePortfolioRepositoryGroup? repositoryGroup = report.RepositoryGroups.FirstOrDefault(
                candidate => candidate.RepositoryId == allocation.RepositoryId);
            HashSet<string> repositoryAdjustmentIds = (repositoryGroup?.AdjustmentIds ?? [])
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> allocationItemIds = allocation.ItemIds.ToHashSet(StringComparer.Ordinal);
            ChangePortfolioItemEstimate[] items = [.. allocation.ItemIds
                .Where(itemsById.ContainsKey)
                .Select(itemId => itemsById[itemId])];
            RequireUniqueText(
                allocation.ItemIds,
                $"contributorRepositoryAllocation[{allocation.Id}].itemIds",
                errors);
            if (allocation.ItemIds.Count == 0 || !PortfolioCanonical(allocation.ItemIds) ||
                allocation.ItemIds.Any(itemId =>
                    !groupItemIds.Contains(itemId) || !allocatedItems.Add(itemId)) ||
                items.Any(item => item.RepositoryId != allocation.RepositoryId) ||
                repositoryGroup?.Id != allocation.RepositoryGroupId)
            {
                errors.Add($"Contributor repository allocation '{allocation.Id}' has invalid item or repository lineage.");
            }

            ValidateRange(
                allocation.IsolatedEffort,
                $"contributorRepositoryAllocation[{allocation.Id}].isolatedEffort",
                errors);
            ValidateRange(
                allocation.NormalizedEffort,
                $"contributorRepositoryAllocation[{allocation.Id}].normalizedEffort",
                errors);
            ValidatePortfolioDelta(
                allocation.ReconciliationDelta,
                allocation.NormalizedEffort,
                allocation.IsolatedEffort,
                $"Contributor repository allocation '{allocation.Id}'",
                errors);
            if (Sum(items.Select(item => item.IsolatedEffort)) != allocation.IsolatedEffort ||
                items.Sum(item => item.AllocatedExpectedHours) != allocation.NormalizedEffort.Expected)
            {
                errors.Add($"Contributor repository allocation '{allocation.Id}' arithmetic is inconsistent.");
            }

            ValidatePortfolioReferences(
                allocation.AdjustmentIds,
                repositoryAdjustmentIds,
                $"contributorRepositoryAllocation[{allocation.Id}].adjustmentIds",
                errors);
            string[] expectedAdjustmentIds = [.. (repositoryGroup?.AdjustmentIds ?? [])
                .Where(id => adjustmentsById.TryGetValue(id, out ChangePortfolioAdjustment? adjustment) &&
                    adjustment.ItemIds.Any(allocationItemIds.Contains))
                .Order(StringComparer.Ordinal)];
            if (!allocation.AdjustmentIds.SequenceEqual(expectedAdjustmentIds, StringComparer.Ordinal))
            {
                errors.Add($"Contributor repository allocation '{allocation.Id}' has inconsistent adjustment lineage.");
            }

            RequireUniqueText(
                allocation.UncertaintyReasons,
                $"contributorRepositoryAllocation[{allocation.Id}].uncertaintyReasons",
                errors);
            if (!PortfolioCanonical(allocation.UncertaintyReasons))
            {
                errors.Add($"Contributor repository allocation '{allocation.Id}' uncertainty reasons are not canonical.");
            }
        }

        if (!group.RepositoryAllocations.Select(allocation => allocation.RepositoryId).SequenceEqual(
                group.RepositoryAllocations.Select(allocation => allocation.RepositoryId)
                    .Order(StringComparer.Ordinal),
                StringComparer.Ordinal) ||
            !allocatedItems.SetEquals(group.ItemIds) ||
            Sum(group.RepositoryAllocations.Select(allocation => allocation.IsolatedEffort)) !=
                group.IsolatedEffort ||
            Sum(group.RepositoryAllocations.Select(allocation => allocation.NormalizedEffort)) !=
                group.NormalizedEffort)
        {
            errors.Add($"Contributor group '{group.Id}' repository allocations do not reconcile exactly.");
        }
    }

    private static void ValidateContributorSummaries(
        ChangePortfolioReport report,
        IReadOnlyList<ChangePortfolioContributorSummary> summaries,
        IReadOnlyList<ChangePortfolioContributorGroup> groups,
        ChangePortfolioAuthorPeriodManifestSelection manifest,
        HashSet<string> groupIds,
        List<string> errors)
    {
        if (!summaries.Select(summary => summary.ContributorId).SequenceEqual(
                manifest.ContributorIds,
                StringComparer.Ordinal))
        {
            errors.Add("Contributor summaries must cover requested contributors in canonical order.");
        }

        foreach (ChangePortfolioContributorSummary summary in summaries)
        {
            ChangePortfolioItemEstimate[] selected = [.. report.Items.Where(item =>
                item.Attribution.ContributorMatches?.Any(match =>
                    match.ContributorId == summary.ContributorId) == true)];
            ValidatePortfolioCounts(
                [
                    summary.SelectedCommitCount,
                    summary.DirectAuthorMatchCount,
                    summary.CoauthorMatchCount,
                    summary.SingleContributorSelectedCommitCount,
                    summary.SharedContributorSelectedCommitCount,
                ],
                $"Contributor summary '{summary.ContributorId}'",
                errors);
            ChangePortfolioContributorGroup? single = groups.FirstOrDefault(group =>
                group.ContributorIds.Count == 1 && group.ContributorIds[0] == summary.ContributorId);
            string[] sharedIds = [.. groups.Where(group =>
                    group.Kind == ChangePortfolioContributorGroupKind.SharedContributors &&
                    group.ContributorIds.Contains(summary.ContributorId, StringComparer.Ordinal))
                .Select(group => group.Id)
                .Order(StringComparer.Ordinal)];
            if (summary.SelectedCommitCount != selected.Length ||
                summary.DirectAuthorMatchCount != PortfolioContributorMatchCount(
                    selected,
                    summary.ContributorId,
                    ChangePortfolioContributorMatchKind.DirectAuthor) ||
                summary.CoauthorMatchCount != PortfolioContributorMatchCount(
                    selected,
                    summary.ContributorId,
                    ChangePortfolioContributorMatchKind.Coauthor) ||
                summary.SingleContributorSelectedCommitCount != selected.Count(item =>
                    PortfolioContributorIds(item).Length == 1) ||
                summary.SharedContributorSelectedCommitCount != selected.Count(item =>
                    PortfolioContributorIds(item).Length > 1) ||
                summary.NoSelectedCommits != (selected.Length == 0) ||
                single?.Id != summary.SingleContributorGroupId ||
                !summary.SharedContributorGroupIds.SequenceEqual(sharedIds, StringComparer.Ordinal))
            {
                errors.Add($"Contributor summary '{summary.ContributorId}' is inconsistent with item attribution.");
            }

            if (!groupIds.Contains(summary.SingleContributorGroupId))
            {
                errors.Add($"Contributor summary '{summary.ContributorId}' has an invalid single-group reference.");
            }

            ValidatePortfolioReferences(
                summary.SharedContributorGroupIds,
                groupIds,
                $"contributorSummary[{summary.ContributorId}].sharedContributorGroupIds",
                errors);
            RequireUniqueText(
                summary.UncertaintyReasons,
                $"contributorSummary[{summary.ContributorId}].uncertaintyReasons",
                errors);
            if (!PortfolioCanonical(summary.UncertaintyReasons))
            {
                errors.Add($"Contributor summary '{summary.ContributorId}' uncertainty reasons are not canonical.");
            }
        }
    }
}
