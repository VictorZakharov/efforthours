namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioReplayEvent
{
    public const string Policy = "declared-replay-range/1.0.0";
    public string AttributionPolicy { get; init; } = Policy;
    public required string Id { get; init; }
    public required string OldBaseObjectId { get; init; }
    public required string OriginalObjectId { get; init; }
    public required string NewBaseObjectId { get; init; }
    public required string RewrittenObjectId { get; init; }
    public string? ReplayObjectId { get; init; }
    public string? ReplayProvenanceId { get; init; }
    public DateTimeOffset? EventTimestamp { get; init; }
    public string? EventProvenanceId { get; init; }
}

public sealed record ChangePortfolioReplayAttribution
{
    public required string EventId { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset OriginalSelectedTimestamp { get; init; }
    public bool SupportOnly { get; init; }
}

public sealed record ChangePortfolioReplayEvidence
{
    public required ChangePortfolioReplayEvent Event { get; init; }
    public required ChangeRewriteReviewReport Review { get; init; }
    public required IReadOnlyList<string> OriginalObjectIds { get; init; }
    public required IReadOnlyList<string> RetainedObjectIds { get; init; }
}

public sealed record ChangePortfolioReplayAllocation
{
    public string Policy { get; init; } = ChangePortfolioReplayEvent.Policy;
    public required ChangePortfolioReplayEvidence Evidence { get; init; }
    public required string Status { get; init; }
    public required decimal AvailableJointExpectedHours { get; init; }
    public decimal? StandaloneNovelExpectedHours { get; init; }
    public decimal? AllocatedEventExpectedHours { get; init; }
    public bool AllocationCapped { get; init; }
}
