namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioComparisonFailure
{
    public required string RepositoryId { get; init; }

    public string? BucketId { get; init; }

    public required string Phase { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public required string MessageDigest { get; init; }

    public EffortHoursAgentAction? AgentAction { get; init; }
}

public sealed record EffortHoursAgentAction
{
    public string Schema { get; init; } = "efforthours-agent-action/1.0";

    public required string FailureCode { get; init; }

    public required string Phase { get; init; }

    public required string SuggestedAction { get; init; }

    public IReadOnlyList<string> SuggestedApprovalPrefix { get; init; } = [];

    public int RetryLimit { get; init; }
}
