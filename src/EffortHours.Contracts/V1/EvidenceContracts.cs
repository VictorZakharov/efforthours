namespace EffortHours.Contracts.V1;

public sealed record RepositoryDescriptor
{
    public required string Name { get; init; }

    public string Scope { get; init; } = ".";

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public string? SourceDigest { get; init; }
}

public sealed record EvidenceLocation
{
    public required string Path { get; init; }

    public int? Line { get; init; }

    public string? Symbol { get; init; }
}

public sealed record EvidenceProvenance
{
    public required EvidenceSourceKind SourceKind { get; init; }

    public required string Analyzer { get; init; }

    public required string AnalyzerVersion { get; init; }

    public required string Method { get; init; }
}

public sealed record EvidenceMeasurement
{
    public required string Name { get; init; }

    public required decimal Value { get; init; }

    public string? Unit { get; init; }
}

public sealed record EvidenceFact
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required string Scope { get; init; }

    public required string Summary { get; init; }

    public required EvidenceProvenance Provenance { get; init; }

    public IReadOnlyList<EvidenceLocation> Locations { get; init; } = [];

    public IReadOnlyList<EvidenceMeasurement> Measurements { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record RepositoryEvidence
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required RepositoryDescriptor Repository { get; init; }

    public IReadOnlyList<EvidenceFact> Facts { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}
