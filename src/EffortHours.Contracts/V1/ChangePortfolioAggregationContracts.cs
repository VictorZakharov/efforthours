namespace EffortHours.Contracts.V1;

public static class ChangePortfolioAggregationPolicies
{
    public const string ExclusiveMatchSetsV1 =
        "exclusive-contributor-and-head-match-sets/1.0.0";
}

public sealed record ChangePortfolioAggregation
{
    public string Policy { get; init; } = ChangePortfolioAggregationPolicies.ExclusiveMatchSetsV1;

    public IReadOnlyList<ChangePortfolioContributorSummary> Contributors { get; init; } = [];

    public IReadOnlyList<ChangePortfolioContributorGroup> ContributorGroups { get; init; } = [];

    public IReadOnlyList<ChangePortfolioRepositorySummary> Repositories { get; init; } = [];
}

public sealed record ChangePortfolioContributorSummary
{
    public required string ContributorId { get; init; }

    public int SelectedCommitCount { get; init; }

    public int DirectAuthorMatchCount { get; init; }

    public int CoauthorMatchCount { get; init; }

    public int SingleContributorSelectedCommitCount { get; init; }

    public int SharedContributorSelectedCommitCount { get; init; }

    public bool NoSelectedCommits { get; init; }

    public required string SingleContributorGroupId { get; init; }

    public IReadOnlyList<string> SharedContributorGroupIds { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record ChangePortfolioContributorGroup
{
    public required string Id { get; init; }

    public required ChangePortfolioContributorGroupKind Kind { get; init; }

    public IReadOnlyList<string> ContributorIds { get; init; } = [];

    public IReadOnlyList<string> ItemIds { get; init; } = [];

    public int SelectedCommitCount { get; init; }

    public int DirectAuthorMatchCount { get; init; }

    public int CoauthorMatchCount { get; init; }

    public required EffortRange IsolatedEffort { get; init; }

    public required EffortRange NormalizedEffort { get; init; }

    public required SignedEffortRange ReconciliationDelta { get; init; }

    public IReadOnlyList<ChangePortfolioContributorRepositoryAllocation> RepositoryAllocations { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record ChangePortfolioContributorRepositoryAllocation
{
    public required string Id { get; init; }

    public required string RepositoryId { get; init; }

    public required string RepositoryGroupId { get; init; }

    public IReadOnlyList<string> ItemIds { get; init; } = [];

    public required EffortRange IsolatedEffort { get; init; }

    public required EffortRange NormalizedEffort { get; init; }

    public required SignedEffortRange ReconciliationDelta { get; init; }

    public IReadOnlyList<string> AdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record ChangePortfolioRepositorySummary
{
    public required string RepositoryId { get; init; }

    public int SelectedCommitCount { get; init; }

    public int DirectAuthorMatchCount { get; init; }

    public int CoauthorMatchCount { get; init; }

    public int SharedContributorSelectedCommitCount { get; init; }

    public int SharedHeadSelectedCommitCount { get; init; }

    public bool NoSelectedCommits { get; init; }

    public required EffortRange IsolatedEffort { get; init; }

    public required EffortRange NormalizedEffort { get; init; }

    public IReadOnlyList<string> AdjustmentIds { get; init; } = [];

    public IReadOnlyList<ChangePortfolioHeadSummary> Heads { get; init; } = [];

    public IReadOnlyList<ChangePortfolioHeadGroup> HeadGroups { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record ChangePortfolioHeadSummary
{
    public required string HeadId { get; init; }

    public int ReachableSelectedCommitCount { get; init; }

    public int UniqueSelectedCommitCount { get; init; }

    public int SharedSelectedCommitCount { get; init; }

    public bool NoUniqueSelectedCommits { get; init; }

    public required string SingleHeadGroupId { get; init; }

    public IReadOnlyList<string> SharedHeadGroupIds { get; init; } = [];
}

public sealed record ChangePortfolioHeadGroup
{
    public required string Id { get; init; }

    public required ChangePortfolioHeadGroupKind Kind { get; init; }

    public IReadOnlyList<string> HeadIds { get; init; } = [];

    public IReadOnlyList<string> ItemIds { get; init; } = [];

    public int SelectedCommitCount { get; init; }

    public required EffortRange IsolatedEffort { get; init; }

    public required EffortRange NormalizedEffort { get; init; }

    public required SignedEffortRange ReconciliationDelta { get; init; }

    public IReadOnlyList<string> AdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}
