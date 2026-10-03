namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioDailyNormalization
{
    public const string Policy = "independent-local-day-change/1.1.0";

    public string Protocol { get; init; } = Policy;

    public required string TimeZone { get; init; }

    public IReadOnlyList<ChangePortfolioDayEstimate> Days { get; init; } = [];
}

public sealed record ChangePortfolioDayEstimate
{
    public required string Date { get; init; }

    public IReadOnlyList<ChangePortfolioRepositoryGroup> RepositoryGroups { get; init; } = [];

    public IReadOnlyList<ChangePortfolioAdjustment> Adjustments { get; init; } = [];
}
