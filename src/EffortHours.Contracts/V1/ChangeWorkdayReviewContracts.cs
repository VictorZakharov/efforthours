namespace EffortHours.Contracts.V1;

public static class ChangeWorkdayReviewPolicies
{
    public const string Review = "retained-workday-review/1.0.0";
    public const string EqualEntries = "equal-matched-entries/1.0.0";
    public const string Boundary = "Experimental, uncalibrated retained-date EHE review; original workdays remain unresolved. Entry contributions are optional allocations, not actual labor, causal credit, or timesheet values. Logged hours never weight EHE; the reference denominator is eight hours.";
}

public sealed record ChangeWorkRecordManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public required string SourceSemanticDigest { get; init; }
    public IReadOnlyList<ChangeWorkRecord> Records { get; init; } = [];
}

public sealed record ChangeWorkRecord
{
    public required string RecordId { get; init; }
    public required string Date { get; init; }
    public required string Kind { get; init; }
    public IReadOnlyList<string> RepositoryIds { get; init; } = [];
    public decimal? LoggedHours { get; init; }
}

public sealed record ChangeWorkdayReviewReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string Policy { get; init; } = ChangeWorkdayReviewPolicies.Review;
    public required string Status { get; init; }
    public required string SourceSemanticDigest { get; init; }
    public required string SourcePortfolioDigest { get; init; }
    public required string WorkRecordInputDigest { get; init; }
    public required string TimeZone { get; init; }
    public required string ContributorId { get; init; }
    public string? EntryPolicy { get; init; }
    public ChangePortfolioAttributionCompleteness? AttributionCompleteness { get; init; }
    public decimal ReferenceHoursPerDay { get; init; } = 8m;
    public IReadOnlyList<string> RepositoryIds { get; init; } = [];
    public IReadOnlyList<ChangeWorkdayReviewDay> Days { get; init; } = [];
    public string Boundary { get; init; } = ChangeWorkdayReviewPolicies.Boundary;
}

public sealed record ChangeWorkdayReviewDay
{
    public required string Date { get; init; }
    public required string BucketId { get; init; }
    public required string Status { get; init; }
    public required string RetainedEvidenceStatus { get; init; }
    public string OriginalWorkdayStatus { get; init; } = ChangeWorkdayPolicies.Unresolved;
    public required decimal SourceAttributedExpectedHours { get; init; }
    public decimal? MatchedDailyMultiplier { get; init; }
    public IReadOnlyList<ChangeWorkRecordReview> Records { get; init; } = [];
}

public sealed record ChangeWorkRecordReview
{
    public required string RecordId { get; init; }
    public required string Kind { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<string> RepositoryIds { get; init; } = [];
    public decimal? AllocatedMultiplierContribution { get; init; }
}
