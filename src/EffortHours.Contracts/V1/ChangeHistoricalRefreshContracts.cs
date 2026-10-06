using System.Text.Json;

namespace EffortHours.Contracts.V1;

public sealed record ChangeHistoricalRefreshManifest
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public required string SourceSemanticDigest { get; init; }
    public required string WorkRecordInputDigest { get; init; }
    public required string SinceInclusiveDate { get; init; }
    public required string UntilExclusiveDate { get; init; }
    public IReadOnlyList<ChangeHistoricalRefreshEntry> Entries { get; init; } = [];
}

public sealed record ChangeHistoricalRefreshEntry
{
    public required string RecordId { get; init; }
    public required JsonElement Original { get; init; }
    public required string NotePermission { get; init; }
    public required string EhePermission { get; init; }
    public required string Restriction { get; init; }
}

public sealed record ChangeHistoricalRefreshPlan
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string Policy { get; init; } = "historical-note-refresh-plan/1.0.0";
    public bool DryRun { get; init; } = true;
    public bool RequiresEntryConfirmation { get; init; } = true;
    public required string RequestedFields { get; init; }
    public required ChangeHistoricalRefreshManifest Input { get; init; }
    public required ChangeWorkdayReviewReport Review { get; init; }
    public IReadOnlyList<ChangeHistoricalRefreshProposal> Proposals { get; init; } = [];
}

public sealed record ChangeHistoricalRefreshProposal
{
    public required string RecordId { get; init; }
    public required string Date { get; init; }
    public required string EvidenceStatus { get; init; }
    public required string OriginalRecordDigest { get; init; }
    public required string NoteStatus { get; init; }
    public string? ProposedDescription { get; init; }
    public required string EheStatus { get; init; }
    public decimal? ProposedMultiplierContribution { get; init; }
}
