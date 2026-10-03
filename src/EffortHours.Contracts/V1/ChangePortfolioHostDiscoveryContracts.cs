namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioHostDiscovery
{
    public string Protocol { get; init; } =
        ChangePortfolioComparisonPolicies.GitHubManagedCacheDiscoveryV1;

    public string Provider { get; init; } = "github";

    public string Scope { get; init; } = "owner-provider-discovery";

    public required string ScopeDigest { get; init; }

    public required string IdentitySources { get; init; }

    public bool Complete { get; init; }

    public int ProviderRepositoryCount { get; init; }

    public int ConsideredRepositoryCount { get; init; }

    public int ActiveRepositoryCount { get; init; }

    public int DefaultHeadCount { get; init; }

    public int OpenPullRequestHeadCount { get; init; }

    public int OpenPullRequestCount { get; init; }

    public int? HistoricalPullRequestHeadCount { get; init; }

    public int? HistoricalPullRequestCount { get; init; }

    public int ProviderQueryCount { get; init; }

    public int ProviderPageCount { get; init; }

    public int ProviderProcessCount { get; init; }

    public decimal ProviderProcessStartupMilliseconds { get; init; }

    public bool ProviderMetadataCacheHit { get; init; }

    public ChangePortfolioProviderDiagnostics? ProviderDiagnostics { get; init; }

    public int LocalObjectCount { get; init; }

    public int AcquiredObjectCount { get; init; }

    public long AcquiredBytes { get; init; }

    public decimal ElapsedMilliseconds { get; init; }
}
