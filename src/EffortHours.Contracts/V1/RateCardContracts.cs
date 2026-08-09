namespace EffortHours.Contracts.V1;

public sealed record RateRange
{
    public required decimal Low { get; init; }

    public required decimal Expected { get; init; }

    public required decimal High { get; init; }
}

public sealed record RateCard
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Currency { get; init; }

    public required decimal HourlyRate { get; init; }

    public RateRange? MarketRange { get; init; }

    public DateOnly? EffectiveDate { get; init; }

    public required string Methodology { get; init; }

    public Uri? SourceUri { get; init; }
}
