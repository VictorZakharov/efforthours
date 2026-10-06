namespace EffortHours.Contracts.V1;

public static class ChangeWorkdayPolicies
{
    public const string EqualDeclaredDaysV1 = "equal-declared-days/1.0.0";
    public const string Boundary = "Experimental, uncalibrated EHE allocation; declared dates are not recovered Git workdays, actual labor, productivity, or timesheet entries. Logged hours do not set EHE weights or reference capacity.";
    public const string Unresolved = "unresolved-original-workday";
}

public sealed record ChangeWorkdayManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public required string SourceSemanticDigest { get; init; }
    public IReadOnlyList<ChangeDeclaredWorkday> Workdays { get; init; } = [];
}

public sealed record ChangeDeclaredWorkday
{
    public required string RecordId { get; init; }
    public required string Date { get; init; }
    public decimal? LoggedHours { get; init; }
}

public sealed record ChangeWorkdayAllocationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string Policy { get; init; } = ChangeWorkdayPolicies.EqualDeclaredDaysV1;
    public string Status { get; init; } = "allocated";
    public required string SourceSemanticDigest { get; init; }
    public required string SourcePortfolioDigest { get; init; }
    public required string WorkdayInputDigest { get; init; }
    public required string TimeZone { get; init; }
    public required string ContributorId { get; init; }
    public required string SourceEstimatorVersion { get; init; }
    public required EffortRange TotalEffort { get; init; }
    public decimal? TotalCapacityHours { get; init; }
    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];
    public IReadOnlyList<ChangeWorkdayAllocationDay> Days { get; init; } = [];
    public string Boundary { get; init; } = ChangeWorkdayPolicies.Boundary;
}

public sealed record ChangeWorkdayAllocationDay
{
    public required string Date { get; init; }
    public required string BucketId { get; init; }
    public string? RecordId { get; init; }
    public string Status { get; init; } = "allocated";
    public string OriginalWorkdayStatus { get; init; } = ChangeWorkdayPolicies.Unresolved;
    public required EffortRange SourceAttributedEffort { get; init; }
    public required EffortRange AllocatedEffort { get; init; }
    public decimal? CapacityHours { get; init; }
    public ChangePortfolioRatioRange? CapacityRatio { get; init; }
    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];
}
