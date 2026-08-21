namespace EffortHours.Contracts.V1;

public static class ChangeAuthorPeriodManifestLimits
{
    public const int MaximumRepositories = 64;

    public const int MaximumHeadsPerRepository = 32;

    public const int MaximumHeads = 128;

    public const int MaximumContributors = 64;

    public const int MaximumAliasesPerContributor = 16;

    public const int MaximumAliases = 128;

    public const int SelectionChunkSize = 1_024;

    public const int AnalysisChunkSize = 16;

    public const long MaximumCandidateLedgerBytesPerRepository = 128L * 1024 * 1024;

    // This ceiling is a final circuit breaker after the byte, output, cache,
    // queue, and concurrency budgets. It is not a calendar or semantic limit.
    public const int EmergencyMaximumIdentityCandidatesPerRepository = 100_000;

    // Preserve the former complete manifest envelope as a last-resort global
    // circuit breaker. Ordinary selection is governed by retained bytes.
    public const int EmergencyMaximumSelectedCommits = 640_000;

    public const int MaximumSelectedCommits = EmergencyMaximumSelectedCommits;

    public const int MaximumIdLength = 128;

    public const int MaximumAliasLength = 320;
}

public sealed record ChangeAuthorPeriodManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required ChangeAuthorPeriodManifestSelection Selection { get; init; }

    public IReadOnlyList<ChangeAuthorPeriodManifestContributor> Contributors { get; init; } = [];

    public IReadOnlyList<ChangeAuthorPeriodManifestRepository> Repositories { get; init; } = [];
}

public sealed record ChangeAuthorPeriodManifestSelection
{
    public required DateTimeOffset SinceInclusive { get; init; }

    public required DateTimeOffset UntilExclusive { get; init; }

    public required string TimeZone { get; init; }

    public required ChangePortfolioDateField DateField { get; init; }

    public required ChangePortfolioMergePolicy MergePolicy { get; init; }

    public required ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; }

    public string IntervalSemantics { get; init; } = "since-inclusive-until-exclusive";
}

public sealed record ChangeAuthorPeriodManifestContributor
{
    public required string Id { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];
}

public sealed record ChangeAuthorPeriodManifestRepository
{
    public required string Id { get; init; }

    public required string RepositoryPath { get; init; }

    public IReadOnlyList<ChangeAuthorPeriodManifestHead> Heads { get; init; } = [];
}

public sealed record ChangeAuthorPeriodManifestHead
{
    public required string Id { get; init; }

    public required string ObjectId { get; init; }
}
