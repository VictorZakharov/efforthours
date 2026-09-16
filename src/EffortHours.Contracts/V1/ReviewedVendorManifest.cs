namespace EffortHours.Contracts.V1;

/// <summary>Explicit reviewed ownership decisions for complete, unmodified third-party bodies.</summary>
public sealed record ReviewedVendorManifest
{
    public const string Protocol = "reviewed-vendor-manifest/1.0.0";
    public const int MaximumEntries = 4096;
    public const int MaximumBytes = 1024 * 1024;

    public string SchemaVersion { get; init; } = ContractVersions.V1;
    public string ProtocolVersion { get; init; } = Protocol;
    public IReadOnlyList<ReviewedVendorEntry> Files { get; init; } = [];
}

public sealed record ReviewedVendorEntry
{
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public string Classification { get; init; } = "third-party-body";
    public required string Library { get; init; }
    public required string Provenance { get; init; }
    public required string Rationale { get; init; }
}
