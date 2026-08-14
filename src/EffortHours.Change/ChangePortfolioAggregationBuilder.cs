using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangePortfolioAggregationBuilder
{
    private const string SharedContributorReason =
        "Multiple requested contributor IDs match these selected commits; the shared group is counted once and no personal effort split is inferred.";
    private const string SharedHeadReason =
        "Selected commits are reachable from multiple pinned heads; the shared workstream group is counted once.";

    public static ChangePortfolioAggregation Build(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioItemEstimate> items,
        IReadOnlyList<ChangePortfolioRepositoryGroup> repositoryGroups,
        IReadOnlyList<ChangePortfolioAdjustment> adjustments)
    {
        ChangePortfolioAuthorPeriodManifestSelection manifest = selection.AuthorPeriodManifest ??
            throw new ArgumentException(
                "Aggregate attribution requires a manifest author-period selection.",
                nameof(selection));
        Dictionary<string, ChangePortfolioRepositoryGroup> groupsByRepository = repositoryGroups
            .ToDictionary(group => group.RepositoryId, StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioAdjustment> adjustmentsById = adjustments
            .ToDictionary(adjustment => adjustment.Id, StringComparer.Ordinal);
        Dictionary<string, List<ChangePortfolioContributorRepositoryAllocation>> contributorAllocations =
            new(StringComparer.Ordinal);
        List<ChangePortfolioRepositorySummary> repositories = [];

        foreach (ChangePortfolioAuthorPeriodManifestRepository repository in manifest.Repositories)
        {
            ChangePortfolioItemEstimate[] repositoryItems = [.. items
                .Where(item => item.RepositoryId == repository.Id)
                .OrderBy(item => item.Id, StringComparer.Ordinal)];
            groupsByRepository.TryGetValue(repository.Id, out ChangePortfolioRepositoryGroup? group);
            if (repositoryItems.Length > 0 && group is null)
            {
                throw new InvalidOperationException(
                    $"Repository '{repository.Id}' has selected items but no normalized repository group.");
            }

            AddContributorAllocations(
                repository.Id,
                repositoryItems,
                group,
                adjustmentsById,
                contributorAllocations);
            repositories.Add(Repository(
                repository,
                repositoryItems,
                group,
                adjustmentsById));
        }

        ChangePortfolioContributorGroup[] contributorGroups = ContributorGroups(
            manifest.ContributorIds,
            contributorAllocations,
            items);
        return new ChangePortfolioAggregation
        {
            Contributors = [.. manifest.ContributorIds.Select(contributorId =>
                Contributor(contributorId, items, contributorGroups))],
            ContributorGroups = contributorGroups,
            Repositories = repositories,
        };
    }

    private static void AddContributorAllocations(
        string repositoryId,
        ChangePortfolioItemEstimate[] items,
        ChangePortfolioRepositoryGroup? repositoryGroup,
        IReadOnlyDictionary<string, ChangePortfolioAdjustment> adjustments,
        Dictionary<string, List<ChangePortfolioContributorRepositoryAllocation>> destination)
    {
        if (items.Length == 0)
        {
            return;
        }

        ContributorSetDraft[] drafts = [.. items
            .GroupBy(item => ContributorKey(ContributorIds(item)), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ContributorSetDraft(
                group.Key,
                ContributorIds(group.First()),
                [.. group.OrderBy(item => item.Id, StringComparer.Ordinal)]))];
        IReadOnlyDictionary<string, EffortRange> normalized = Allocate(
            repositoryGroup!.NormalizedEffort,
            drafts.Select(draft => (draft.Key, draft.Items)));
        foreach (ContributorSetDraft draft in drafts)
        {
            EffortRange isolated = ContractValidation.Sum(draft.Items.Select(item => item.IsolatedEffort));
            EffortRange allocated = normalized[draft.Key];
            string[] itemIds = [.. draft.Items.Select(item => item.Id)];
            ChangePortfolioContributorRepositoryAllocation allocation = new()
            {
                Id = ChangePortfolioIdentity.ContributorRepositoryAllocationId(
                    draft.ContributorIds,
                    repositoryId),
                RepositoryId = repositoryId,
                RepositoryGroupId = repositoryGroup.Id,
                ItemIds = itemIds,
                IsolatedEffort = isolated,
                NormalizedEffort = allocated,
                ReconciliationDelta = Difference(allocated, isolated),
                AdjustmentIds = InfluencingAdjustments(
                    repositoryGroup.AdjustmentIds,
                    itemIds,
                    adjustments),
                UncertaintyReasons = Uncertainty(
                    draft.Items,
                    repositoryGroup.UncertaintyReasons,
                    draft.ContributorIds.Count > 1 ? SharedContributorReason : null),
            };
            if (!destination.TryGetValue(draft.Key, out List<ChangePortfolioContributorRepositoryAllocation>? list))
            {
                list = [];
                destination.Add(draft.Key, list);
            }

            list.Add(allocation);
        }
    }

    private static ChangePortfolioContributorGroup[] ContributorGroups(
        IReadOnlyList<string> requestedContributorIds,
        IReadOnlyDictionary<string, List<ChangePortfolioContributorRepositoryAllocation>> allocations,
        IReadOnlyList<ChangePortfolioItemEstimate> items)
    {
        SortedSet<string> keys = new(StringComparer.Ordinal);
        foreach (string contributorId in requestedContributorIds)
        {
            keys.Add(ContributorKey([contributorId]));
        }

        keys.UnionWith(allocations.Keys);
        List<ChangePortfolioContributorGroup> groups = [];
        foreach (string key in keys)
        {
            string[] contributorIds = ContributorIds(key);
            ChangePortfolioContributorRepositoryAllocation[] repositoryAllocations = allocations.TryGetValue(
                key,
                out List<ChangePortfolioContributorRepositoryAllocation>? values)
                ? [.. values.OrderBy(value => value.RepositoryId, StringComparer.Ordinal)]
                : [];
            string[] itemIds = [.. repositoryAllocations
                .SelectMany(allocation => allocation.ItemIds)
                .Order(StringComparer.Ordinal)];
            ChangePortfolioItemEstimate[] groupItems = [.. items
                .Where(item => itemIds.Contains(item.Id, StringComparer.Ordinal))];
            EffortRange isolated = ContractValidation.Sum(
                repositoryAllocations.Select(allocation => allocation.IsolatedEffort));
            EffortRange normalized = ContractValidation.Sum(
                repositoryAllocations.Select(allocation => allocation.NormalizedEffort));
            groups.Add(new ChangePortfolioContributorGroup
            {
                Id = ChangePortfolioIdentity.ContributorGroupId(contributorIds),
                Kind = contributorIds.Length == 1
                    ? ChangePortfolioContributorGroupKind.SingleContributor
                    : ChangePortfolioContributorGroupKind.SharedContributors,
                ContributorIds = contributorIds,
                ItemIds = itemIds,
                SelectedCommitCount = itemIds.Length,
                DirectAuthorMatchCount = MatchCount(
                    groupItems,
                    ChangePortfolioContributorMatchKind.DirectAuthor),
                CoauthorMatchCount = MatchCount(
                    groupItems,
                    ChangePortfolioContributorMatchKind.Coauthor),
                IsolatedEffort = isolated,
                NormalizedEffort = normalized,
                ReconciliationDelta = Difference(normalized, isolated),
                RepositoryAllocations = repositoryAllocations,
                UncertaintyReasons = [.. repositoryAllocations
                    .SelectMany(allocation => allocation.UncertaintyReasons)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)],
            });
        }

        return [.. groups];
    }

    private static ChangePortfolioContributorSummary Contributor(
        string contributorId,
        IReadOnlyList<ChangePortfolioItemEstimate> items,
        IReadOnlyList<ChangePortfolioContributorGroup> groups)
    {
        ChangePortfolioItemEstimate[] selected = [.. items.Where(item =>
            ContributorMatches(item).Any(match => match.ContributorId == contributorId))];
        ChangePortfolioContributorGroup single = groups.Single(group =>
            group.ContributorIds.Count == 1 && group.ContributorIds[0] == contributorId);
        string[] sharedGroupIds = [.. groups
            .Where(group => group.Kind == ChangePortfolioContributorGroupKind.SharedContributors &&
                group.ContributorIds.Contains(contributorId, StringComparer.Ordinal))
            .Select(group => group.Id)
            .Order(StringComparer.Ordinal)];
        return new ChangePortfolioContributorSummary
        {
            ContributorId = contributorId,
            SelectedCommitCount = selected.Length,
            DirectAuthorMatchCount = ContributorMatchCount(
                selected,
                contributorId,
                ChangePortfolioContributorMatchKind.DirectAuthor),
            CoauthorMatchCount = ContributorMatchCount(
                selected,
                contributorId,
                ChangePortfolioContributorMatchKind.Coauthor),
            SingleContributorSelectedCommitCount = selected.Count(item => ContributorIds(item).Count == 1),
            SharedContributorSelectedCommitCount = selected.Count(item => ContributorIds(item).Count > 1),
            NoSelectedCommits = selected.Length == 0,
            SingleContributorGroupId = single.Id,
            SharedContributorGroupIds = sharedGroupIds,
            UncertaintyReasons = Uncertainty(selected, [], null),
        };
    }

    private sealed record ContributorSetDraft(
        string Key,
        IReadOnlyList<string> ContributorIds,
        IReadOnlyList<ChangePortfolioItemEstimate> Items);
}
