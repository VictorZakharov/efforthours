using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangePortfolioAggregationBuilder
{
    private static ChangePortfolioRepositorySummary Repository(
        ChangePortfolioAuthorPeriodManifestRepository repository,
        ChangePortfolioItemEstimate[] items,
        ChangePortfolioRepositoryGroup? repositoryGroup,
        IReadOnlyDictionary<string, ChangePortfolioAdjustment> adjustments)
    {
        ChangePortfolioHeadGroup[] headGroups = HeadGroups(
            repository,
            items,
            repositoryGroup,
            adjustments);
        EffortRange zero = Zero();
        return new ChangePortfolioRepositorySummary
        {
            RepositoryId = repository.Id,
            SelectedCommitCount = items.Length,
            DirectAuthorMatchCount = MatchCount(items, ChangePortfolioContributorMatchKind.DirectAuthor),
            CoauthorMatchCount = MatchCount(items, ChangePortfolioContributorMatchKind.Coauthor),
            SharedContributorSelectedCommitCount = items.Count(item => ContributorIds(item).Count > 1),
            SharedHeadSelectedCommitCount = items.Count(item => HeadIds(item).Count > 1),
            NoSelectedCommits = items.Length == 0,
            IsolatedEffort = repositoryGroup?.IsolatedEffort ?? zero,
            NormalizedEffort = repositoryGroup?.NormalizedEffort ?? zero,
            AdjustmentIds = repositoryGroup?.AdjustmentIds ?? [],
            Heads = [.. repository.Heads.Select(head => Head(head.Id, items, headGroups))],
            HeadGroups = headGroups,
            UncertaintyReasons = Uncertainty(
                items,
                repositoryGroup?.UncertaintyReasons ?? [],
                null),
        };
    }

    private static ChangePortfolioHeadGroup[] HeadGroups(
        ChangePortfolioAuthorPeriodManifestRepository repository,
        IReadOnlyList<ChangePortfolioItemEstimate> items,
        ChangePortfolioRepositoryGroup? repositoryGroup,
        IReadOnlyDictionary<string, ChangePortfolioAdjustment> adjustments)
    {
        Dictionary<string, ChangePortfolioItemEstimate[]> itemsByKey = items
            .GroupBy(item => HeadKey(HeadIds(item)), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        foreach (ChangePortfolioAuthorPeriodManifestHead head in repository.Heads)
        {
            itemsByKey.TryAdd(HeadKey([head.Id]), []);
        }

        HeadSetDraft[] drafts = [.. itemsByKey
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new HeadSetDraft(pair.Key, HeadIds(pair.Key), pair.Value))];
        IReadOnlyDictionary<string, EffortRange> normalized = Allocate(
            repositoryGroup?.NormalizedEffort ?? Zero(),
            drafts.Select(draft => (draft.Key, draft.Items)));
        List<ChangePortfolioHeadGroup> groups = [];
        foreach (HeadSetDraft draft in drafts)
        {
            EffortRange isolated = ContractValidation.Sum(draft.Items.Select(item => item.IsolatedEffort));
            EffortRange allocated = normalized[draft.Key];
            string[] itemIds = [.. draft.Items.Select(item => item.Id)];
            groups.Add(new ChangePortfolioHeadGroup
            {
                Id = ChangePortfolioIdentity.HeadGroupId(repository.Id, draft.HeadIds),
                Kind = draft.HeadIds.Count == 1
                    ? ChangePortfolioHeadGroupKind.SingleHead
                    : ChangePortfolioHeadGroupKind.SharedHeads,
                HeadIds = draft.HeadIds,
                ItemIds = itemIds,
                SelectedCommitCount = itemIds.Length,
                IsolatedEffort = isolated,
                NormalizedEffort = allocated,
                ReconciliationDelta = Difference(allocated, isolated),
                AdjustmentIds = repositoryGroup is null
                    ? []
                    : InfluencingAdjustments(repositoryGroup.AdjustmentIds, itemIds, adjustments),
                UncertaintyReasons = Uncertainty(
                    draft.Items,
                    repositoryGroup?.UncertaintyReasons ?? [],
                    draft.HeadIds.Count > 1 ? SharedHeadReason : null),
            });
        }

        return [.. groups];
    }

    private static ChangePortfolioHeadSummary Head(
        string headId,
        IReadOnlyList<ChangePortfolioItemEstimate> items,
        IReadOnlyList<ChangePortfolioHeadGroup> groups)
    {
        ChangePortfolioHeadGroup single = groups.Single(group =>
            group.HeadIds.Count == 1 && group.HeadIds[0] == headId);
        string[] sharedGroupIds = [.. groups
            .Where(group => group.Kind == ChangePortfolioHeadGroupKind.SharedHeads &&
                group.HeadIds.Contains(headId, StringComparer.Ordinal))
            .Select(group => group.Id)
            .Order(StringComparer.Ordinal)];
        ChangePortfolioItemEstimate[] reachable = [.. items.Where(item =>
            HeadIds(item).Contains(headId, StringComparer.Ordinal))];
        int unique = reachable.Count(item => HeadIds(item).Count == 1);
        return new ChangePortfolioHeadSummary
        {
            HeadId = headId,
            ReachableSelectedCommitCount = reachable.Length,
            UniqueSelectedCommitCount = unique,
            SharedSelectedCommitCount = reachable.Length - unique,
            NoUniqueSelectedCommits = unique == 0,
            SingleHeadGroupId = single.Id,
            SharedHeadGroupIds = sharedGroupIds,
        };
    }

    private sealed record HeadSetDraft(
        string Key,
        IReadOnlyList<string> HeadIds,
        IReadOnlyList<ChangePortfolioItemEstimate> Items);
}
