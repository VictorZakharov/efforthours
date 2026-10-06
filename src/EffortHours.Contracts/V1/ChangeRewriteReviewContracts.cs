namespace EffortHours.Contracts.V1;

public static class ChangeRewriteReviewPolicy
{
    public const string Version = "immutable-replay-review/1.0.0";
    public const string Boundary = "Experimental, uncalibrated counterfactual artifact review. Replay and event provenance are caller declarations, not verified historical causation or labor. Comparisons are non-additive; only the retained feature comparison values the final feature. Missing replay or event evidence never means zero work.";
}

public sealed record ChangeRewriteReviewManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string Policy { get; init; } = ChangeRewriteReviewPolicy.Version;
    public required string RepositoryId { get; init; }
    public required string RepositoryPath { get; init; }
    public string? ScopeRepository { get; init; }
    public required string OriginalObjectId { get; init; }
    public required string OldBaseObjectId { get; init; }
    public required string RewrittenObjectId { get; init; }
    public required string NewBaseObjectId { get; init; }
    public string? ReplayObjectId { get; init; }
    public string? ReplayProvenanceId { get; init; }
    public DateTimeOffset? EventTimestamp { get; init; }
    public string? EventProvenanceId { get; init; }
    public required DateTimeOffset SinceInclusive { get; init; }
    public required DateTimeOffset UntilExclusive { get; init; }
}

public sealed record ChangeRewriteReviewReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string Policy { get; init; } = ChangeRewriteReviewPolicy.Version;
    public required string Status { get; init; }
    public required string RepositoryId { get; init; }
    public required string InputDigest { get; init; }
    public required DateTimeOffset SinceInclusive { get; init; }
    public required DateTimeOffset UntilExclusive { get; init; }
    public DateTimeOffset? OriginalAuthorTimestamp { get; init; }
    public DateTimeOffset? RewrittenCommitterTimestamp { get; init; }
    public DateTimeOffset? EventTimestamp { get; init; }
    public string? ReplayProvenanceId { get; init; }
    public string? EventProvenanceId { get; init; }
    public required string ReplayConfidence { get; init; }
    public required string EventAttributionStatus { get; init; }
    public EffortRange? EventAttributedNovelEffort { get; init; }
    public int OriginalCommitCount { get; init; }
    public int RewrittenCommitCount { get; init; }
    public IReadOnlyList<string>? MissingObjectIds { get; init; }
    public ChangeReplayProof? ReplayProof { get; init; }
    public IReadOnlyList<ChangeRewriteComparison> Comparisons { get; init; } = [];
    public string Boundary { get; init; } = ChangeRewriteReviewPolicy.Boundary;
}

public sealed record ChangeReplayProof
{
    public int ExactReplayPathCount { get; init; }
    public int InheritedUpstreamPathCount { get; init; }
    public int DeclaredConflictPathCount { get; init; }
}

public sealed record ChangeRewriteComparison
{
    public required string Role { get; init; }
    public required ChangeSelection Selection { get; init; }
    public required EstimationProfile Profile { get; init; }
    public required string EstimatorVersion { get; init; }
    public required EffortRange Effort { get; init; }
    public required string SourceReportDigest { get; init; }
    public int RepresentedPathCount { get; init; }
}
