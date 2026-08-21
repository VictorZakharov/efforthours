using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateAggregationRepositories(
        ChangePortfolioReport report,
        IReadOnlyList<ChangePortfolioRepositorySummary> summaries,
        ChangePortfolioAuthorPeriodManifestSelection manifest,
        HashSet<string> reportItemIds,
        List<string> errors)
    {
        if (!summaries.Select(summary => summary.RepositoryId).SequenceEqual(
                manifest.Repositories.Select(repository => repository.Id),
                StringComparer.Ordinal))
        {
            errors.Add("Repository summaries must cover manifest repositories in canonical order.");
        }

        foreach (ChangePortfolioRepositorySummary summary in summaries)
        {
            ChangePortfolioAuthorPeriodManifestRepository? repository = manifest.Repositories.FirstOrDefault(
                candidate => candidate.Id == summary.RepositoryId);
            if (repository is null)
            {
                errors.Add($"Repository summary '{summary.RepositoryId}' is not declared by the manifest.");
                continue;
            }

            ChangePortfolioItemEstimate[] items = [.. report.Items.Where(item =>
                item.RepositoryId == summary.RepositoryId)];
            ChangePortfolioRepositoryGroup? repositoryGroup = report.RepositoryGroups.FirstOrDefault(group =>
                group.RepositoryId == summary.RepositoryId);
            EffortRange expectedIsolated = repositoryGroup?.IsolatedEffort ?? PortfolioZero();
            EffortRange expectedNormalized = repositoryGroup?.NormalizedEffort ?? PortfolioZero();
            ValidatePortfolioCounts(
                [
                    summary.SelectedCommitCount,
                    summary.DirectAuthorMatchCount,
                    summary.CoauthorMatchCount,
                    summary.SharedContributorSelectedCommitCount,
                    summary.SharedHeadSelectedCommitCount,
                ],
                $"Repository summary '{summary.RepositoryId}'",
                errors);
            if ((items.Length > 0) != (repositoryGroup is not null) ||
                summary.SelectedCommitCount != items.Length ||
                summary.DirectAuthorMatchCount != PortfolioMatchCount(
                    items,
                    ChangePortfolioContributorMatchKind.DirectAuthor) ||
                summary.CoauthorMatchCount != PortfolioMatchCount(
                    items,
                    ChangePortfolioContributorMatchKind.Coauthor) ||
                summary.SharedContributorSelectedCommitCount != items.Count(item =>
                    PortfolioContributorIds(item).Length > 1) ||
                summary.SharedHeadSelectedCommitCount != items.Count(item =>
                    PortfolioHeadIds(item).Count > 1) ||
                summary.NoSelectedCommits != (items.Length == 0) ||
                summary.IsolatedEffort != expectedIsolated ||
                summary.NormalizedEffort != expectedNormalized ||
                !summary.AdjustmentIds.SequenceEqual(
                    repositoryGroup?.AdjustmentIds ?? [],
                    StringComparer.Ordinal))
            {
                errors.Add($"Repository summary '{summary.RepositoryId}' is inconsistent with its normalized ledger.");
            }

            ValidateRange(
                summary.IsolatedEffort,
                $"repositorySummary[{summary.RepositoryId}].isolatedEffort",
                errors);
            ValidateRange(
                summary.NormalizedEffort,
                $"repositorySummary[{summary.RepositoryId}].normalizedEffort",
                errors);
            RequireUniqueText(
                summary.AdjustmentIds,
                $"repositorySummary[{summary.RepositoryId}].adjustmentIds",
                errors);
            RequireUniqueText(
                summary.UncertaintyReasons,
                $"repositorySummary[{summary.RepositoryId}].uncertaintyReasons",
                errors);
            if (!PortfolioCanonical(summary.UncertaintyReasons))
            {
                errors.Add($"Repository summary '{summary.RepositoryId}' uncertainty reasons are not canonical.");
            }

            HashSet<string> headGroupIds = ValidateHeadGroups(
                report,
                summary,
                repository,
                items,
                repositoryGroup,
                reportItemIds,
                errors);
            ValidateHeadSummaries(summary, repository, items, headGroupIds, errors);
        }
    }

    private static HashSet<string> ValidateHeadGroups(
        ChangePortfolioReport report,
        ChangePortfolioRepositorySummary summary,
        ChangePortfolioAuthorPeriodManifestRepository repository,
        ChangePortfolioItemEstimate[] repositoryItems,
        ChangePortfolioRepositoryGroup? repositoryGroup,
        HashSet<string> reportItemIds,
        List<string> errors)
    {
        HashSet<string> groupIds = new(StringComparer.Ordinal);
        HashSet<string> coveredItems = new(StringComparer.Ordinal);
        HashSet<string> configuredHeads = repository.Heads.Select(head => head.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> availableAdjustments = (repositoryGroup?.AdjustmentIds ?? [])
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioItemEstimate> itemsById =
            PortfolioItemIndex(report.Items);
        Dictionary<string, ChangePortfolioAdjustment> adjustmentsById =
            PortfolioAdjustmentIndex(report.Adjustments);
        List<string> keys = [];
        foreach (ChangePortfolioHeadGroup group in summary.HeadGroups)
        {
            HashSet<string> groupItemIds = group.ItemIds.ToHashSet(StringComparer.Ordinal);
            RequireText(group.Id, "aggregation.headGroup.id", errors);
            if (!groupIds.Add(group.Id))
            {
                errors.Add($"Head group ID '{group.Id}' is duplicated in repository '{summary.RepositoryId}'.");
            }

            if (group.HeadIds.Count is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository ||
                group.HeadIds.Distinct(StringComparer.Ordinal).Count() != group.HeadIds.Count ||
                group.HeadIds.Any(id => !configuredHeads.Contains(id)) ||
                !PortfolioCanonical(group.HeadIds))
            {
                errors.Add($"Head group '{group.Id}' has an invalid head set.");
            }

            ChangePortfolioHeadGroupKind expectedKind = group.HeadIds.Count == 1
                ? ChangePortfolioHeadGroupKind.SingleHead
                : ChangePortfolioHeadGroupKind.SharedHeads;
            if (!Enum.IsDefined(group.Kind) || group.Kind != expectedKind)
            {
                errors.Add($"Head group '{group.Id}' has an inconsistent kind.");
            }

            keys.Add(PortfolioSetKey(group.HeadIds));
            ValidateHeadGroupItems(
                summary.RepositoryId,
                group,
                itemsById,
                reportItemIds,
                coveredItems,
                errors);
            ValidateRange(group.IsolatedEffort, $"headGroup[{group.Id}].isolatedEffort", errors);
            ValidateRange(group.NormalizedEffort, $"headGroup[{group.Id}].normalizedEffort", errors);
            ValidatePortfolioDelta(
                group.ReconciliationDelta,
                group.NormalizedEffort,
                group.IsolatedEffort,
                $"Head group '{group.Id}'",
                errors);
            ValidatePortfolioReferences(
                group.AdjustmentIds,
                availableAdjustments,
                $"headGroup[{group.Id}].adjustmentIds",
                errors);
            string[] expectedAdjustmentIds = [.. (repositoryGroup?.AdjustmentIds ?? [])
                .Where(id => adjustmentsById.TryGetValue(id, out ChangePortfolioAdjustment? adjustment) &&
                    adjustment.ItemIds.Any(groupItemIds.Contains))
                .Order(StringComparer.Ordinal)];
            if (!group.AdjustmentIds.SequenceEqual(expectedAdjustmentIds, StringComparer.Ordinal))
            {
                errors.Add($"Head group '{group.Id}' has inconsistent adjustment lineage.");
            }

            RequireUniqueText(
                group.UncertaintyReasons,
                $"headGroup[{group.Id}].uncertaintyReasons",
                errors);
            if (!PortfolioCanonical(group.UncertaintyReasons))
            {
                errors.Add($"Head group '{group.Id}' uncertainty reasons are not canonical.");
            }
        }

        if (!keys.SequenceEqual(keys.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
        {
            errors.Add($"Repository '{summary.RepositoryId}' head groups must use unique canonical head-set order.");
        }

        foreach (string headId in configuredHeads)
        {
            if (summary.HeadGroups.Count(group => group.HeadIds.Count == 1 &&
                    group.HeadIds[0] == headId) != 1)
            {
                errors.Add($"Repository '{summary.RepositoryId}' head '{headId}' requires one single-head group.");
            }
        }

        if (!coveredItems.SetEquals(repositoryItems.Select(item => item.Id)) ||
            Sum(summary.HeadGroups.Select(group => group.IsolatedEffort)) != summary.IsolatedEffort ||
            Sum(summary.HeadGroups.Select(group => group.NormalizedEffort)) != summary.NormalizedEffort)
        {
            errors.Add($"Repository '{summary.RepositoryId}' head groups do not reconcile exactly.");
        }

        return groupIds;
    }

    private static void ValidateHeadGroupItems(
        string repositoryId,
        ChangePortfolioHeadGroup group,
        Dictionary<string, ChangePortfolioItemEstimate> itemsById,
        HashSet<string> reportItemIds,
        HashSet<string> coveredItems,
        List<string> errors)
    {
        RequireUniqueText(group.ItemIds, $"headGroup[{group.Id}].itemIds", errors);
        if (!PortfolioCanonical(group.ItemIds))
        {
            errors.Add($"Head group '{group.Id}' item references are not canonical.");
        }

        List<ChangePortfolioItemEstimate> items = [];
        foreach (string itemId in group.ItemIds)
        {
            itemsById.TryGetValue(itemId, out ChangePortfolioItemEstimate? item);
            if (!reportItemIds.Contains(itemId) || !coveredItems.Add(itemId) || item is null ||
                item.RepositoryId != repositoryId ||
                PortfolioSetKey(PortfolioHeadIds(item)) != PortfolioSetKey(group.HeadIds))
            {
                errors.Add($"Head group '{group.Id}' has an invalid or repeated item reference.");
            }

            if (item is not null)
            {
                items.Add(item);
            }
        }

        ValidatePortfolioCounts(
            [group.SelectedCommitCount],
            $"Head group '{group.Id}'",
            errors);
        if (group.SelectedCommitCount != group.ItemIds.Count ||
            Sum(items.Select(item => item.IsolatedEffort)) != group.IsolatedEffort ||
            items.Sum(item => item.AllocatedExpectedHours) != group.NormalizedEffort.Expected)
        {
            errors.Add($"Head group '{group.Id}' counts or arithmetic are inconsistent.");
        }

        if (group.ItemIds.Count == 0 && group.Kind != ChangePortfolioHeadGroupKind.SingleHead)
        {
            errors.Add($"Empty head group '{group.Id}' must be a configured single-head zero row.");
        }
    }

    private static void ValidateHeadSummaries(
        ChangePortfolioRepositorySummary summary,
        ChangePortfolioAuthorPeriodManifestRepository repository,
        ChangePortfolioItemEstimate[] repositoryItems,
        HashSet<string> groupIds,
        List<string> errors)
    {
        if (!summary.Heads.Select(head => head.HeadId).SequenceEqual(
                repository.Heads.Select(head => head.Id),
                StringComparer.Ordinal))
        {
            errors.Add($"Repository '{summary.RepositoryId}' head summaries are not canonical or complete.");
        }

        foreach (ChangePortfolioHeadSummary head in summary.Heads)
        {
            ChangePortfolioItemEstimate[] reachable = [.. repositoryItems.Where(item =>
                PortfolioHeadIds(item).Contains(head.HeadId, StringComparer.Ordinal))];
            int unique = reachable.Count(item => PortfolioHeadIds(item).Count == 1);
            ChangePortfolioHeadGroup? single = summary.HeadGroups.FirstOrDefault(group =>
                group.HeadIds.Count == 1 && group.HeadIds[0] == head.HeadId);
            string[] sharedIds = [.. summary.HeadGroups.Where(group =>
                    group.Kind == ChangePortfolioHeadGroupKind.SharedHeads &&
                    group.HeadIds.Contains(head.HeadId, StringComparer.Ordinal))
                .Select(group => group.Id)
                .Order(StringComparer.Ordinal)];
            ValidatePortfolioCounts(
                [head.ReachableSelectedCommitCount, head.UniqueSelectedCommitCount, head.SharedSelectedCommitCount],
                $"Head summary '{summary.RepositoryId}/{head.HeadId}'",
                errors);
            if (head.ReachableSelectedCommitCount != reachable.Length ||
                head.UniqueSelectedCommitCount != unique ||
                head.SharedSelectedCommitCount != reachable.Length - unique ||
                head.NoUniqueSelectedCommits != (unique == 0) ||
                single?.Id != head.SingleHeadGroupId ||
                !head.SharedHeadGroupIds.SequenceEqual(sharedIds, StringComparer.Ordinal))
            {
                errors.Add($"Head summary '{summary.RepositoryId}/{head.HeadId}' is inconsistent with reachability.");
            }

            if (!groupIds.Contains(head.SingleHeadGroupId))
            {
                errors.Add($"Head summary '{summary.RepositoryId}/{head.HeadId}' has an invalid single-group reference.");
            }

            ValidatePortfolioReferences(
                head.SharedHeadGroupIds,
                groupIds,
                $"headSummary[{summary.RepositoryId}/{head.HeadId}].sharedHeadGroupIds",
                errors);
        }
    }
}
