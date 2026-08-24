using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioComparisonBuildOptions
{
    public required ChangePortfolioComparisonView View { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset? AsOf { get; init; }
    public ChangePortfolioHostDiscovery? Discovery { get; init; }
    public ChangePortfolioScopeProfile? ScopeProfile { get; init; }
    public ChangePortfolioScopeSummary? ScopeSummary { get; init; }
    public required string CliVersion { get; init; }
    public required EstimationProfile Profile { get; init; }
    public required ChangePortfolioBucketPolicyKind BucketKind { get; init; }
    public required string BucketPolicy { get; init; }
    public ChangePortfolioContributorNormalization ContributorNormalization { get; init; } =
        ChangePortfolioContributorNormalization.Joint;
    public required ChangePortfolioBucketManifest BucketManifest { get; init; }
    public IReadOnlyList<ChangePortfolioComparisonBucket> Buckets { get; init; } = [];
    public ChangePortfolioCapacityManifest? CapacityManifest { get; init; }
    public required ChangeAuthorPeriodManifest SourceManifest { get; init; }
    public ChangePortfolioExecutionTelemetry? ExecutionTelemetry { get; init; }
    public ChangePortfolioExecutionStatistics? ExecutionStatistics { get; init; }
    public ChangePortfolioComparisonExecution? ExecutionOverride { get; init; }
}
