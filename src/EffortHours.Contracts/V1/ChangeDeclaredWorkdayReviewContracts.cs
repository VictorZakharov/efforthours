namespace EffortHours.Contracts.V1;

public static class ChangeDeclaredWorkdayReviewPolicies
{
    public const string Review = "declared-workday-review/1.0.0";
    public const string EqualEntries = "equal-declared-day-entries/1.0.0";
    public const string Boundary = "Experimental, uncalibrated EHE projection over externally declared work-record dates. Dates are not recovered Git workdays or verified labor; source event and intermediate-history uncertainty remain. Logged hours never weight EHE; the reference denominator is eight hours and entry values conserve one rounded period multiplier.";
}

public sealed record ChangeDeclaredWorkdayResolution
{
    public string Policy { get; init; } = "declared-workday-resolution/1.0.0";
    public required ChangeWorkdayManifest Manifest { get; init; }
    public required ChangeWorkdayAllocationReport Allocation { get; init; }
    public decimal? ExpectedMultiplierTotal { get; init; }
}
