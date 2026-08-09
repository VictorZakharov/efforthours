namespace EffortHours.Contracts.V1;

public sealed record Diagnostic
{
    public required string Code { get; init; }

    public required DiagnosticSeverity Severity { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public IReadOnlyList<EvidenceLocation> Locations { get; init; } = [];
}
