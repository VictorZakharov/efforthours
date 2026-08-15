using System.Text.Json;
using System.Text.Json.Serialization;
using EffortHours.Contracts;

namespace EffortHours.RepositoryCalibration;

internal static class ValidationOpeningRunner
{
    public const string ToolVersion = "repository-validation-opener/0.1.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task RunAsync(
        ValidationOpenOptions options,
        TextWriter diagnostics,
        CancellationToken cancellationToken)
    {
        VerifiedValidationBoundary boundary = await ValidationBoundaryVerifier.VerifyAsync(
            options,
            cancellationToken).ConfigureAwait(false);
        await diagnostics.WriteLineAsync(
                "Frozen candidate, selection, custody, source, and artifact boundaries verified; opening validation only.")
            .ConfigureAwait(false);

        Directory.CreateDirectory(options.WorkspacePath);
        Directory.CreateDirectory(options.PacketDirectory);
        ReproductionOptions reproductionOptions = new()
        {
            PlanPath = options.PlanPath,
            WorkspacePath = options.WorkspacePath,
            CliPath = options.CliPath,
            PacketDirectory = options.PacketDirectory,
            OutputPath = options.OutputPath,
            CustodyPath = options.CustodyPath,
            GitHubCliPath = options.GitHubCliPath,
        };
        List<ReproductionFamily> opened = [];
        List<string> failures = [];
        foreach (SamplingFamily family in boundary.ValidationFamilies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await diagnostics.WriteLineAsync(
                        $"Verifying and opening blind validation source {family.RepositoryName}.")
                    .ConfigureAwait(false);
                ReproductionFamily current = await RepositoryCalibrationReproducer.ReproduceFamilyAsync(
                    reproductionOptions,
                    boundary.Plan.SizeMetric,
                    family,
                    "validation",
                    diagnostics,
                    cancellationToken).ConfigureAwait(false);
                ValidateOpenedFamily(boundary, current);
                opened.Add(current);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add($"{family.Id}: {exception.GetType().Name}: {exception.Message}");
                await WriteAsync(
                    options,
                    boundary,
                    opened,
                    failures,
                    "validation-opening-failed-closed",
                    cancellationToken).ConfigureAwait(false);
                throw new InvalidDataException(
                    $"Validation opening failed closed for {family.RepositoryName}; see the opening record.",
                    exception);
            }
        }

        if (opened.Count != 9 ||
            Directory.GetFiles(options.PacketDirectory, "*.json", SearchOption.TopDirectoryOnly).Length != 9)
        {
            failures.Add("The validation opener did not produce exactly nine blind packets.");
            await WriteAsync(
                options,
                boundary,
                opened,
                failures,
                "validation-opening-failed-closed",
                cancellationToken).ConfigureAwait(false);
            throw new InvalidDataException("Validation opening did not produce the complete frozen partition.");
        }

        await WriteAsync(
            options,
            boundary,
            opened,
            failures,
            "strict-blind-validation-packets-generated-candidate-values-unavailable",
            cancellationToken).ConfigureAwait(false);
        await diagnostics.WriteLineAsync(
                "Wrote nine strict-blind validation packets; frozen challenger output and all test access remain unopened.")
            .ConfigureAwait(false);
    }

    private static void ValidateOpenedFamily(
        VerifiedValidationBoundary boundary,
        ReproductionFamily current)
    {
        ReproductionFamily prior = boundary.Reproduction.Families.Single(family => family.Id == current.Id);
        HoldoutFamily held = boundary.Custody.Families.Single(family => family.Id == current.Id);
        if (current.Partition != "validation" ||
            current.CommitSha != prior.CommitSha ||
            current.GitTreeSha1 != prior.GitTreeSha1 ||
            current.ArchiveSha256 != prior.ArchiveSha256 ||
            current.ArchiveSha256 != held.ArchiveSha256 ||
            current.ArchiveBytes != prior.ArchiveBytes ||
            current.VerifiedBlobCount != prior.VerifiedBlobCount ||
            current.VerifiedBlobBytes != prior.VerifiedBlobBytes ||
            current.SubmoduleCount != prior.SubmoduleCount ||
            current.LicenseBlobSha1 != prior.LicenseBlobSha1 ||
            current.LicenseContentSha256 != prior.LicenseContentSha256 ||
            current.VerificationStatus != "verified-commit-tree-blobs-license" ||
            current.AnalysisStatus != "generated-blind-validation" ||
            current.Analysis is null ||
            current.Analysis.EstimatorVersion != "seed-rules/0.4.0" ||
            current.Analysis.AuthoringVersion != "calibration-authoring/0.2.0" ||
            current.Analysis.Rubric != "ehe-work-item/1.1.0" ||
            current.Analysis.PacketTargetCount <= 0)
        {
            throw new InvalidDataException(
                $"Reopened validation source does not match frozen custody for '{current.Id}'.");
        }
    }

    private static async Task WriteAsync(
        ValidationOpenOptions options,
        VerifiedValidationBoundary boundary,
        IReadOnlyList<ReproductionFamily> families,
        IReadOnlyList<string> failures,
        string status,
        CancellationToken cancellationToken)
    {
        ValidationOpeningManifest manifest = new()
        {
            Status = status,
            OpeningImplementationCommit = options.SourceCommit,
            CandidateManifest = new FrozenValidationArtifact
            {
                Id = "efforthours-public-readiness-candidate-freeze",
                Version = "1.2.0",
                Digest = boundary.CandidateManifestDigest,
            },
            SamplingPlan = new FrozenValidationArtifact
            {
                Id = boundary.Plan.Id,
                Version = boundary.Plan.Version,
                Digest = boundary.PlanDigest,
            },
            ReproductionManifest = new FrozenValidationArtifact
            {
                Id = boundary.Reproduction.Id,
                Version = boundary.Reproduction.Version,
                Digest = boundary.ReproductionDigest,
            },
            HoldoutCustody = new FrozenValidationArtifact
            {
                Id = boundary.Custody.Id,
                Version = boundary.Custody.Version,
                Digest = boundary.CustodyDigest,
            },
            Families = [.. families.OrderBy(family => family.Id, StringComparer.Ordinal)],
            Failures = failures,
        };
        string json = ContractJson.ToCanonicalDocument(JsonSerializer.Serialize(manifest, JsonOptions));
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
        await File.WriteAllTextAsync(options.OutputPath, json, cancellationToken).ConfigureAwait(false);
    }
}
