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

    public IReadOnlyList<ChangePortfolioProviderRepositoryObservation>? RepositoryObservations { get; init; }

    public ChangePortfolioHistoricalPullRequestPlan? HistoricalPullRequests { get; init; }

    public ChangePortfolioProviderRequestObservation? LastRequest { get; init; }

    public IReadOnlyList<ChangePortfolioProviderFallback> Fallbacks { get; init; } = [];
}

public sealed record ChangePortfolioProviderFallback
{
    public required string Phase { get; init; }

    public required string Reason { get; init; }

    public int RepositoryCount { get; init; }
}

public sealed record ChangePortfolioProviderRepositoryObservation
{
    public required string RepositoryDigest { get; init; }
    public required string Phase { get; init; }
    public required string State { get; init; }
    public int QueryCount { get; init; }
    public int PageCount { get; init; }
    public decimal ElapsedMilliseconds { get; init; }
    public string? ElapsedKind { get; init; }
    public decimal? WallElapsedMilliseconds { get; init; }
}

public sealed record ChangePortfolioHistoricalPullRequestPlan
{
    public int CandidateCount { get; init; }
    public int CacheHitCount { get; init; }
    public int BatchCount { get; init; }
    public int FallbackCount { get; init; }
    public int CompletedCount { get; init; }
    public int SelectedCount { get; init; }
    public int PendingCount { get; init; }
}

public sealed record ChangePortfolioProviderRequestObservation
{
    public string? Api { get; init; }
    public string? Outcome { get; init; }
    public int? ExitCode { get; init; }
    public int? HttpStatus { get; init; }
    public string? TimeoutOwner { get; init; }
    public required string Phase { get; init; }
    public required string Operation { get; init; }
    public string? RepositoryDigest { get; init; }
    public required string State { get; init; }
    public int PageCount { get; init; }
    public decimal ElapsedMilliseconds { get; init; }
}
