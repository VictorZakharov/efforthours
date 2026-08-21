namespace EffortHours.Contracts.V1;

public static class ChangePortfolioLimits
{
    public const int MaximumManifestItems = 128;

    public const int MaximumReportItems = ChangeAuthorPeriodManifestLimits.MaximumSelectedCommits;

    public const long MaximumRenderedOutputBytes = 512L * 1024 * 1024;

    public const long MaximumCheckpointBytesPerRepository = 512L * 1024 * 1024;
}

public sealed record ChangePortfolioManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public IReadOnlyList<ChangePortfolioManifestItem> Items { get; init; } = [];
}

public sealed record ChangePortfolioManifestItem
{
    public required string Id { get; init; }

    public required string RepositoryId { get; init; }

    public required string RepositoryPath { get; init; }

    public required string PullRequest { get; init; }

    public string? GitHubRepository { get; init; }
}

public sealed record ChangePortfolioAuthorPeriodSelection
{
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }

    public required string TimeZone { get; init; }

    public required ChangePortfolioDateField DateField { get; init; }

    public required ChangePortfolioMergePolicy MergePolicy { get; init; }

    public required ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; }

    public required string HeadSelector { get; init; }

    public required string HeadObjectId { get; init; }

    public string IntervalSemantics { get; init; } = "since-inclusive-until-exclusive";
}

public sealed record ChangePortfolioSelection
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required ChangePortfolioSelectionKind Kind { get; init; }

    public bool ManifestBased { get; init; }

    public ChangePortfolioAuthorPeriodSelection? AuthorPeriod { get; init; }

    public ChangePortfolioAuthorPeriodManifestSelection? AuthorPeriodManifest { get; init; }
}

public sealed record ChangePortfolioAuthorPeriodManifestSelection
{
    public required string ManifestDigest { get; init; }

    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }

    public required string TimeZone { get; init; }

    public required ChangePortfolioDateField DateField { get; init; }

    public required ChangePortfolioMergePolicy MergePolicy { get; init; }

    public required ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; }

    public IReadOnlyList<string> ContributorIds { get; init; } = [];

    public IReadOnlyList<ChangePortfolioAuthorPeriodManifestRepository> Repositories { get; init; } = [];

    public string IntervalSemantics { get; init; } = "since-inclusive-until-exclusive";
}

public sealed record ChangePortfolioAuthorPeriodManifestRepository
{
    public required string Id { get; init; }

    public IReadOnlyList<ChangePortfolioAuthorPeriodManifestHead> Heads { get; init; } = [];
}

public sealed record ChangePortfolioAuthorPeriodManifestHead
{
    public required string Id { get; init; }

    public required string ObjectId { get; init; }
}

public sealed record ChangePortfolioAttribution
{
    public required ChangePortfolioAttributionKind Kind { get; init; }

    public DateTimeOffset? SelectedTimestamp { get; init; }

    public bool MergeCommit { get; init; }

    public int ParentCount { get; init; }

    public IReadOnlyList<ChangePortfolioContributorMatch>? ContributorMatches { get; init; }

    public IReadOnlyList<string>? HeadIds { get; init; }

    public IReadOnlyList<string> AmbiguityReasons { get; init; } = [];
}

public sealed record ChangePortfolioContributorMatch
{
    public required string ContributorId { get; init; }

    public required ChangePortfolioContributorMatchKind Kind { get; init; }
}

public sealed record ChangePortfolioItemEstimate
{
    public required string Id { get; init; }

    public required string SelectorId { get; init; }

    public required string RepositoryId { get; init; }

    public required string BaseContextId { get; init; }

    public required ChangeSelection Selection { get; init; }

    public required string EvidenceDigest { get; init; }

    public required string PatchDigest { get; init; }

    public int RepresentedPathCount { get; init; }

    public required EffortRange IsolatedEffort { get; init; }

    public required decimal AllocatedExpectedHours { get; init; }

    public decimal? AllocatedExpectedCost { get; init; }

    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];

    public required ChangePortfolioAttribution Attribution { get; init; }

    public string? DuplicateOfItemId { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record ChangePortfolioBaseContext
{
    public required string Id { get; init; }

    public required string BaseObjectId { get; init; }

    public IReadOnlyList<string> ItemIds { get; init; } = [];
}

public sealed record ChangePortfolioRepositoryGroup
{
    public required string Id { get; init; }

    public required string RepositoryId { get; init; }

    public required ChangePortfolioOrderPolicy OrderPolicy { get; init; }

    public IReadOnlyList<ChangePortfolioBaseContext> BaseContexts { get; init; } = [];

    public IReadOnlyList<string> ItemIds { get; init; } = [];

    public required EffortRange IsolatedEffort { get; init; }

    public required EffortRange NormalizedEffort { get; init; }

    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];

    public required string Assessment { get; init; }

    public IReadOnlyList<string> AdjustmentIds { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record ChangePortfolioAdjustment
{
    public required string Id { get; init; }

    public required ChangePortfolioAdjustmentKind Kind { get; init; }

    public required SignedEffortRange EffortDelta { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string> ItemIds { get; init; } = [];

    public int AffectedPathCount { get; init; }
}

public sealed record ChangePortfolioReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string EstimatorVersion { get; init; }

    public required string SourceChangeEstimatorVersion { get; init; }

    public required ChangePortfolioSelection Selection { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required EstimationBaseline Baseline { get; init; }

    public required EffortRange IsolatedEffort { get; init; }

    public required EffortRange TotalEffort { get; init; }

    public RateCard? RateCard { get; init; }

    public CostRange? TotalCost { get; init; }

    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];

    public IReadOnlyList<ChangePortfolioRepositoryGroup> RepositoryGroups { get; init; } = [];

    public ChangePortfolioAggregation? Aggregation { get; init; }

    public IReadOnlyList<ChangePortfolioItemEstimate> Items { get; init; } = [];

    public IReadOnlyList<ChangePortfolioAdjustment> Adjustments { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public required VerificationSummary Verification { get; init; }
}
