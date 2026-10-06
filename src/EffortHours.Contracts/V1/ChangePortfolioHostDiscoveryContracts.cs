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

    public ChangePortfolioRepositoryRestriction? RepositoryRestriction { get; init; }

    public ChangePortfolioAcquisitionSummary? Acquisition { get; init; }

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

public sealed record ChangePortfolioRepositoryRestriction
{
    public string Policy { get; init; } = "explicit-repositories/1.0.0";
    public required string InputDigest { get; init; }
    public int RequestedRepositoryCount { get; init; }
    public int ExcludedRepositoryCount { get; init; }
}

public sealed record ChangePortfolioAcquisitionSummary
{
    public string Policy { get; init; } = "native-acquisition-budget/1.0.0";
    public long MaximumBytes { get; init; }
    public int TimeoutSeconds { get; init; }
    public int RepositoryCount { get; init; }
    public int CacheHitHeadCount { get; init; }
    public int AcquiredObjectCount { get; init; }
    public long AcquiredBytes { get; init; }
}
