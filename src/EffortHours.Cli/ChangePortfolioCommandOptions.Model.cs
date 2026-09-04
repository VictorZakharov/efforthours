using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioCommandOptions
{
    public string? RepositoryPath { get; init; }

    public IReadOnlyList<string> PullRequests { get; init; } = [];

    public string? GitHubRepository { get; init; }

    public bool FetchMissing { get; init; }

    public string? ManifestPath { get; init; }

    public string? AuthorPeriodManifestPath { get; init; }

    public string? Owner { get; init; }

    public string? WorkspacePath { get; init; }

    public string? Scope { get; init; }

    public bool Today { get; init; }

    public bool NativePeriod { get; init; }

    public bool TeamComparison { get; init; }

    public ChangePortfolioNativePeriodKind? Period { get; init; }

    public ChangePortfolioNativeBreakdown Breakdown { get; init; } =
        ChangePortfolioNativeBreakdown.Total;

    public decimal? CapacityHoursPerDay { get; init; }

    public string? ContributorsFrom { get; init; }

    public int? SampleSize { get; init; }

    public string? SampleSeed { get; init; }

    public IReadOnlyList<string> IncludedAuthors { get; init; } = [];

    public bool NativeOptionsProvided { get; init; }

    public bool IncludeOpenPullRequests { get; init; }

    public decimal? CapacityHours { get; init; }

    public bool Preflight { get; init; }

    public string? Bucket { get; init; }

    public string? BucketManifestPath { get; init; }

    public string? CapacityManifestPath { get; init; }

    public ChangePortfolioComparisonView ComparisonView { get; init; } =
        ChangePortfolioComparisonView.Trend;

    public ChangePortfolioContributorNormalization ContributorNormalization { get; init; } =
        ChangePortfolioContributorNormalization.Joint;

    public DateTimeOffset? GeneratedAt { get; init; }

    public string? ReportTitle { get; init; }

    public string? CheckpointPath { get; init; }

    public bool NoCheckpoint { get; init; }

    public IReadOnlyList<string> AuthorAliases { get; init; } = [];

    public DateTimeOffset? SinceInclusive { get; init; }

    public DateTimeOffset? UntilExclusive { get; init; }

    public string TimeZone { get; init; } = "UTC";

    public ChangePortfolioDateField DateField { get; init; } = ChangePortfolioDateField.Author;

    public ChangePortfolioMergePolicy MergePolicy { get; init; } = ChangePortfolioMergePolicy.Exclude;

    public ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; } =
        ChangePortfolioCoauthorPolicy.Include;

    public string HeadRevision { get; init; } = "HEAD";

    public EstimationProfile Profile { get; init; } = EstimationProfile.Implementation;

    public string Format { get; init; } = "json";

    public bool Compact { get; init; }

    public bool NoRate { get; init; }

    public decimal? HourlyRate { get; init; }

    public string Currency { get; init; } = "USD";

    public string? OutputPath { get; init; }

    public bool IsManifest => ManifestPath is not null;

    public bool IsAuthorPeriodManifest => AuthorPeriodManifestPath is not null;

    public bool IsAuthorPeriod => AuthorAliases.Count > 0;

    public bool IsNativePeriod => NativePeriod || TeamComparison;

    public bool IsComparison =>
        Today || IsNativePeriod || Bucket is not null || BucketManifestPath is not null;
}

internal readonly record struct ChangePortfolioCommandParseResult(
    ChangePortfolioCommandOptions? Options,
    string? Error,
    bool ShowHelp = false);
