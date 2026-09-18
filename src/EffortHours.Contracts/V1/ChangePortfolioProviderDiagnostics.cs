namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioProviderDiagnostics
{
    public string MetadataCacheStatus { get; init; } = "not-observed";

    public string IdentityResolution { get; init; } = "not-observed";

    public int OpenPullRequestCandidateRepositoryCount { get; init; }

    public int DefaultHeadBatchCount { get; init; }

    public int DefaultHeadQueryCount { get; init; }

    public int OpenPullRequestAccountQueryCount { get; init; }

    public int OpenPullRequestQueryCount { get; init; }

    public IReadOnlyList<ChangePortfolioProviderFallback> Fallbacks { get; init; } = [];
}

public sealed record ChangePortfolioProviderFallback
{
    public required string Phase { get; init; }

    public required string Reason { get; init; }

    public int RepositoryCount { get; init; }
}
