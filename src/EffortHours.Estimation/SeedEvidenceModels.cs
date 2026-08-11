using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

internal sealed record SeedEstimationScope
{
    public required string Id { get; init; }

    public required string Scope { get; init; }

    public required string Directory { get; init; }

    public required string Ecosystem { get; init; }

    public required string Role { get; init; }

    public required EvidenceFact Fact { get; init; }

    public required bool IsTest { get; init; }

    public required bool IsRunnable { get; init; }

    public bool IsProduction => !IsTest;
}

internal sealed record SeedFileEvidence
{
    public required string Path { get; init; }

    public required string Role { get; init; }

    public required string RoleFamily { get; init; }

    public string? Language { get; init; }

    public required string Extension { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public string? Sha256 { get; init; }

    public string? DuplicateKey { get; init; }

    public required bool IsTest { get; init; }

    public required bool IsMaintained { get; init; }

    public required decimal PhysicalLines { get; init; }

    public required EvidenceFact Fact { get; init; }
}

internal sealed record NormalizedEvidenceFact
{
    public required string Key { get; init; }

    public required string Kind { get; init; }

    public required string Scope { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<EvidenceFact> Facts { get; init; } = [];

    public IReadOnlyDictionary<string, decimal> Measurements { get; init; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required bool HasExactDuplicates { get; init; }

    public static NormalizedEvidenceFact Single(EvidenceFact fact) => new()
    {
        Key = fact.Id,
        Kind = fact.Kind,
        Scope = fact.Scope,
        Summary = fact.Summary,
        Facts = [fact],
        Measurements = fact.Measurements
            .GroupBy(measurement => measurement.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(measurement => measurement.Value),
                StringComparer.Ordinal),
        Tags = fact.Tags,
        HasExactDuplicates = false,
    };
}

internal sealed record StructureNormalization(
    decimal Factor,
    int AnalyzedFiles,
    int ProductionFiles,
    int CanonicalProductionFiles,
    bool HasTests,
    bool HasDuplicates);
