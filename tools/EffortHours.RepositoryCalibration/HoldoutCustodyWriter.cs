using System.Text.Json;
using System.Text.Json.Serialization;

namespace EffortHours.RepositoryCalibration;

internal static class HoldoutCustodyWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static async Task WriteAsync(
        ReproductionManifest manifest,
        string manifestDigest,
        string outputPath,
        CancellationToken cancellationToken)
    {
        HoldoutCustody custody = new()
        {
            Id = manifest.Id,
            Version = manifest.Version,
            SamplingPlan = manifest.SamplingPlan,
            ReproductionManifest = new ReproductionArtifactReference
            {
                Version = manifest.Version,
                Digest = manifestDigest,
            },
            Families = [.. manifest.Families
                .Where(family => !string.Equals(
                    family.Partition,
                    "development",
                    StringComparison.Ordinal))
                .Select(family => new HoldoutFamily
                {
                    Id = family.Id,
                    RepositoryName = family.RepositoryName,
                    PrimaryStratum = family.PrimaryStratum,
                    Partition = family.Partition,
                    CommitSha = family.CommitSha,
                    GitTreeSha1 = family.GitTreeSha1,
                    ArchiveSha256 = family.ArchiveSha256,
                    LicenseBlobSha1 = family.LicenseBlobSha1,
                    LicenseContentSha256 = family.LicenseContentSha256,
                    SourceVerificationStatus = family.VerificationStatus,
                    AnalysisStatus = family.AnalysisStatus,
                })],
        };
        string json = JsonSerializer.Serialize(custody, JsonOptions) + Environment.NewLine;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record HoldoutCustody
{
    public string SchemaVersion { get; init; } = "repository-calibration-holdout-custody/1.0.0";

    public required string Id { get; init; }

    public required string Version { get; init; }

    public string Status { get; init; } = "source-custody-only-labels-not-authored";

    public required ReproductionPlanReference SamplingPlan { get; init; }

    public required ReproductionArtifactReference ReproductionManifest { get; init; }

    public HoldoutPolicy Policy { get; init; } = new();

    public IReadOnlyList<HoldoutFamily> Families { get; init; } = [];
}

internal sealed record ReproductionArtifactReference
{
    public required string Version { get; init; }

    public required string Digest { get; init; }
}

internal sealed record HoldoutPolicy
{
    public string Validation { get; init; } =
        "Labels are not authored. Author blind only after the complete finite candidate manifest is frozen; do not publish or inspect candidate guidance during authoring.";

    public string Test { get; init; } =
        "Labels are not authored. When authored, seal the label body and digest outside the repository and reveal only after validation selects one frozen candidate.";

    public string Contamination { get; init; } =
        "Any holdout disagreement used to alter an analyzer, feature, prior, threshold, or uncertainty width makes that family diagnostic-only for that attempt.";
}

internal sealed record HoldoutFamily
{
    public required string Id { get; init; }

    public required string RepositoryName { get; init; }

    public required string PrimaryStratum { get; init; }

    public required string Partition { get; init; }

    public required string CommitSha { get; init; }

    public required string GitTreeSha1 { get; init; }

    public required string ArchiveSha256 { get; init; }

    public required string LicenseBlobSha1 { get; init; }

    public required string LicenseContentSha256 { get; init; }

    public required string SourceVerificationStatus { get; init; }

    public required string AnalysisStatus { get; init; }

    public string LabelStatus { get; init; } = "not-authored";

    public string? LabelDigest { get; init; }

    public string Custodian { get; init; } =
        "maintainer-external-custody-required-before-authoring";
}
