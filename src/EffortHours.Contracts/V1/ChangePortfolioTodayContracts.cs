namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioScopeProfile
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Digest { get; init; }
    public required string Source { get; init; }
    public IReadOnlyList<string> ImportantExclusions { get; init; } = [];
}

public sealed record ChangePortfolioScopeSummary
{
    public int IdentitySelectedCommitCount { get; init; }
    public int AdmittedCommitCount { get; init; }
    public int ScopeEmptyCommitCount { get; init; }
}
