using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangePortfolioAggregationBuilder
{
    private const char SetSeparator = '\u001f';

    private static IReadOnlyDictionary<string, EffortRange> Allocate(
        EffortRange total,
        IEnumerable<(string Id, IReadOnlyList<ChangePortfolioItemEstimate> Items)> groups)
    {
        (string Id, IReadOnlyList<ChangePortfolioItemEstimate> Items)[] materialized = [.. groups];
        return ChangePortfolioAggregateAllocation.Allocate(
            total,
            [.. materialized.Select(group => new ChangePortfolioAggregateAllocationInput(
                group.Id,
                group.Items.Sum(item => item.AllocatedExpectedHours),
                group.Items.Sum(item => item.IsolatedEffort.High)))]);
    }

    private static IReadOnlyList<ChangePortfolioContributorMatch> ContributorMatches(
        ChangePortfolioItemEstimate item) => item.Attribution.ContributorMatches ??
        throw new InvalidOperationException(
            $"Manifest portfolio item '{item.Id}' has no contributor matches.");

    private static IReadOnlyList<string> ContributorIds(ChangePortfolioItemEstimate item) =>
        [.. ContributorMatches(item).Select(match => match.ContributorId)];

    private static string[] ContributorIds(string key) => key.Split(SetSeparator);

    private static string ContributorKey(IReadOnlyList<string> contributorIds) =>
        string.Join(SetSeparator, contributorIds.Order(StringComparer.Ordinal));

    private static IReadOnlyList<string> HeadIds(ChangePortfolioItemEstimate item) =>
        item.Attribution.HeadIds ?? throw new InvalidOperationException(
            $"Manifest portfolio item '{item.Id}' has no head reachability.");

    private static string[] HeadIds(string key) => key.Split(SetSeparator);

    private static string HeadKey(IReadOnlyList<string> headIds) =>
        string.Join(SetSeparator, headIds.Order(StringComparer.Ordinal));

    private static int MatchCount(
        IEnumerable<ChangePortfolioItemEstimate> items,
        ChangePortfolioContributorMatchKind kind) => items.Sum(item =>
            ContributorMatches(item).Count(match => match.Kind == kind));

    private static int ContributorMatchCount(
        IEnumerable<ChangePortfolioItemEstimate> items,
        string contributorId,
        ChangePortfolioContributorMatchKind kind) => items.Sum(item =>
            ContributorMatches(item).Count(match =>
                match.ContributorId == contributorId && match.Kind == kind));

    private static string[] InfluencingAdjustments(
        IReadOnlyList<string> repositoryAdjustmentIds,
        IReadOnlyList<string> itemIds,
        IReadOnlyDictionary<string, ChangePortfolioAdjustment> adjustments) =>
        [.. repositoryAdjustmentIds
            .Where(id => adjustments.TryGetValue(id, out ChangePortfolioAdjustment? adjustment) &&
                adjustment.ItemIds.Any(itemId => itemIds.Contains(itemId, StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)];

    private static string[] Uncertainty(
        IEnumerable<ChangePortfolioItemEstimate> items,
        IEnumerable<string> repositoryReasons,
        string? additionalReason) =>
        [.. repositoryReasons
            .Concat(items.SelectMany(item => item.UncertaintyReasons))
            .Concat(items.SelectMany(item => item.Attribution.AmbiguityReasons))
            .Concat(additionalReason is null ? [] : [additionalReason])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static SignedEffortRange Difference(EffortRange normalized, EffortRange isolated) => new()
    {
        Low = normalized.Low - isolated.Low,
        Expected = normalized.Expected - isolated.Expected,
        High = normalized.High - isolated.High,
    };

    private static EffortRange Zero() => new() { Low = 0m, Expected = 0m, High = 0m };
}
