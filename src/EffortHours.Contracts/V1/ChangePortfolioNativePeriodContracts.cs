namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioNativePeriod
{
    public string Protocol { get; init; } =
        ChangePortfolioComparisonPolicies.NativePeriodReportV1;

    public required ChangePortfolioNativePeriodKind Kind { get; init; }

    public required ChangePortfolioNativeBreakdown Breakdown { get; init; }

    public decimal CapacityHoursPerDay { get; init; }

    public required ChangePortfolioContributorSelection ContributorSelection { get; init; }
}

public sealed record ChangePortfolioContributorSelection
{
    public string Protocol { get; init; } =
        ChangePortfolioComparisonPolicies.ContributorSampleV1;

    public required ChangePortfolioContributorSelectionMode Mode { get; init; }

    public bool Complete { get; init; }

    public string? SampleSeed { get; init; }

    public int RequestedSampleSize { get; init; }

    public int? EligiblePopulationCount { get; init; }

    public IReadOnlyList<string> SampledContributorIds { get; init; } = [];

    public IReadOnlyList<string> IncludedContributorIds { get; init; } = [];

    public required string InputDigest { get; init; }
}
