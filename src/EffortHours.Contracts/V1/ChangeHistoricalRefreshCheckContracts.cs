namespace EffortHours.Contracts.V1;

public sealed record ChangeHistoricalRefreshCheck
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string Policy { get; init; } = "historical-refresh-preflight/1.0.0";
    public bool DryRun { get; init; } = true;
    public bool RequiresEntryConfirmation { get; init; } = true;
    public required string Status { get; init; }
    public required string PlanDigest { get; init; }
    public required ChangeHistoricalRefreshPlan Plan { get; init; }
    public required ChangeHistoricalRefreshManifest Current { get; init; }
    public IReadOnlyList<string> UnexpectedRecordIds { get; init; } = [];
    public IReadOnlyList<ChangeHistoricalRefreshCheckEntry> Entries { get; init; } = [];
}

public sealed record ChangeHistoricalRefreshCheckEntry
{
    public required string RecordId { get; init; }
    public required string OriginalRecordDigest { get; init; }
    public string? CurrentRecordDigest { get; init; }
    public string? ProposedNoteRecordDigest { get; init; }
    public required string SnapshotStatus { get; init; }
    public required string NoteStatus { get; init; }
    public required string EheStatus { get; init; }
}
