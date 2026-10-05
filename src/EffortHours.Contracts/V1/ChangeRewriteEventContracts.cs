namespace EffortHours.Contracts.V1;

public sealed record ChangeRewriteEvent
{
    public const string Policy = "declared-rewrite-event/1.0.0";
    public string AttributionPolicy { get; init; } = Policy;
    public required string OriginalObjectId { get; init; }
    public required string RewrittenObjectId { get; init; }
    public required string OldBaseObjectId { get; init; }
    public required string NewBaseObjectId { get; init; }
    public DateTimeOffset? EventTimestamp { get; init; }
}

public sealed record ChangeRewriteAttribution
{
    public required ChangeRewriteEvent Evidence { get; init; }
    public required DateTimeOffset OriginalAuthorTimestamp { get; init; }
    public required DateTimeOffset RewrittenCommitterTimestamp { get; init; }
    public required string Role { get; init; }
    public required bool SupportOnly { get; init; }
    public string Basis { get; init; } = "caller-declared-immutable-pair";
    public string Confidence { get; init; } = "declared-not-verified-workday";
    public string? Treatment { get; init; }
}
