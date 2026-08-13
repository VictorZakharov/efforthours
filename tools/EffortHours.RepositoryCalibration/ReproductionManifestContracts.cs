using System.Text.Json.Serialization;

namespace EffortHours.RepositoryCalibration;

internal sealed record ReproductionManifest
{
    public string SchemaVersion { get; init; } = "repository-calibration-reproduction/1.0.0";

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required ReproductionPlanReference SamplingPlan { get; init; }

    public string ToolVersion { get; init; } = RepositoryCalibrationReproducer.ToolVersion;

    public string JsonArtifactDigestPolicy { get; init; } = JsonArtifactDigest.Policy;

    public required string Profile { get; init; }

    public string HoldoutBoundary { get; init; } =
        "Validation and test snapshots are provenance-verified only. They are not scanned, estimated, or projected by this checkpoint.";

    public IReadOnlyList<ReproductionFamily> Families { get; init; } = [];
}

internal sealed record ReproductionPlanReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Digest { get; init; }
}

internal sealed record ReproductionFamily
{
    public required string Id { get; init; }

    public required string RepositoryName { get; init; }

    public required string PrimaryStratum { get; init; }

    public required string Partition { get; init; }

    public required string CommitSha { get; init; }

    public required string GitTreeSha1 { get; init; }

    public required string ArchiveSha256 { get; init; }

    public required long ArchiveBytes { get; init; }

    public required int VerifiedBlobCount { get; init; }

    public required long VerifiedBlobBytes { get; init; }

    public required int SubmoduleCount { get; init; }

    public required string LicenseBlobSha1 { get; init; }

    public required string LicenseContentSha256 { get; init; }

    public required string VerificationStatus { get; init; }

    public required string AnalysisStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DevelopmentAnalysis? Analysis { get; init; }
}

internal sealed record DevelopmentAnalysis
{
    public required string EvidenceSourceDigest { get; init; }

    public required string EvidenceArtifactSha256 { get; init; }

    public required string EstimatorVersion { get; init; }

    public required string EstimateDigest { get; init; }

    public required string EstimateArtifactSha256 { get; init; }

    public required string AuthoringVersion { get; init; }

    public required string Rubric { get; init; }

    public required string PacketFile { get; init; }

    public required string PacketSha256 { get; init; }

    public required int PacketTargetCount { get; init; }
}
