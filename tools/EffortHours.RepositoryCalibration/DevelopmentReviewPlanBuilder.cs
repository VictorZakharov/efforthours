using System.Security.Cryptography;
using System.Text.Json;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class DevelopmentReviewPlanBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] SelectedRepositories =
    [
        "CarterCommunity/Carter",
        "tj/commander.js",
        "oqtane/oqtane.framework",
    ];

    public static async Task RunAsync(
        DevelopmentReviewOptions options,
        CancellationToken cancellationToken)
    {
        SamplingPlan plan = await ReadAsync<SamplingPlan>(options.PlanPath, cancellationToken)
            .ConfigureAwait(false);
        ReproductionManifest manifest = await ReadAsync<ReproductionManifest>(
            options.ManifestPath,
            cancellationToken).ConfigureAwait(false);
        List<CalibrationReviewPlanRecord> records = [];
        foreach (string repositoryName in SelectedRepositories)
        {
            SamplingFamily family = plan.Families.Single(item => item.RepositoryName == repositoryName);
            ReproductionFamily reproduction = manifest.Families.Single(item =>
                item.RepositoryName == repositoryName);
            if (family.Partition != "development" ||
                reproduction.AnalysisStatus != "generated-blind-development" ||
                reproduction.Analysis is null)
            {
                throw new InvalidDataException($"{repositoryName} is not a reproduced development family.");
            }

            string packetPath = Path.Combine(
                options.PacketDirectory,
                reproduction.Analysis.PacketFile);
            await RequireDigestAsync(
                packetPath,
                reproduction.Analysis.PacketSha256,
                cancellationToken).ConfigureAwait(false);
            string slug = Slug(repositoryName);
            string evidencePath = Path.Combine(
                options.OutputDirectory,
                slug,
                "repository-evidence.json");
            await RequireDigestAsync(
                evidencePath,
                reproduction.Analysis.EvidenceArtifactSha256,
                cancellationToken).ConfigureAwait(false);

            CalibrationAuthoringPacket packet = ContractJson.Deserialize<CalibrationAuthoringPacket>(
                await File.ReadAllTextAsync(packetPath, cancellationToken).ConfigureAwait(false));
            ValidateBlindPacket(packet, family, reproduction);
            records.Add(CreateRecord(family, reproduction, packet));
        }

        CalibrationReviewPlan reviewPlan = new()
        {
            CompilerVersion = CalibrationReviewCompiler.CompilerVersion,
            Id = "efforthours-public-readiness-development",
            Version = "0.2.0",
            Description =
                "First balanced development-only host-AI teacher slice from the frozen public-readiness cohort. " +
                "These logical weak-supervision labels were authored from strict-blind capability packets, " +
                "verified static evidence, and pinned public source; they are not independent or production accuracy evidence.",
            Rubric = new CalibrationRubricReference
            {
                Id = CalibrationAuthoring.RubricId,
                Version = CalibrationAuthoring.RubricVersion,
            },
            Records = records,
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(reviewPlan);
        string json = ContractJson.Serialize(reviewPlan);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            json);
        if (errors.Count > 0 || !schema.IsValid)
        {
            throw new InvalidDataException(
                "Development review plan is invalid: " +
                string.Join("; ", errors.Concat(schema.Errors)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(options.ReviewPlanPath)!);
        await File.WriteAllTextAsync(
            options.ReviewPlanPath,
            json + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);
    }

    private static CalibrationReviewPlanRecord CreateRecord(
        SamplingFamily family,
        ReproductionFamily reproduction,
        CalibrationAuthoringPacket packet) => new()
        {
            Id = $"record:{Slug(family.RepositoryName)}:implementation",
            Repository = packet.Repository,
            Profile = packet.Profile,
            BaselineId = packet.BaselineId,
            Partition = CalibrationPartition.Development,
            SourceEstimatorVersion = packet.Candidate.EstimatorVersion,
            SourceEstimateDigest = packet.Candidate.EstimateDigest,
            Source = new CalibrationSourceProvenance
            {
                DataClassification = CalibrationDataClassification.PublicRedistributable,
                SourceReference = family.RepositoryUrl,
                Revision = family.SourceSnapshot.CommitSha,
                LicenseExpression = family.License.Expression,
                RedistributionAllowed = family.License.RedistributionAllowed,
                Notes =
                    $"Exact tree {family.SourceSnapshot.GitTreeSha1}; archive {reproduction.ArchiveSha256}; " +
                    $"license blob {family.License.GitBlobSha1} and content {family.License.ContentSha256}. " +
                    "Source was reproduced outside the repository and was not committed.",
            },
            Review = new CalibrationReviewProvenance
            {
                Status = CalibrationReviewStatus.TeacherEstimate,
                CompletedOn = new DateOnly(2026, 8, 13),
                Reviewers =
                [
                    new CalibrationReviewer
                    {
                        Id = "teacher:openai-codex-gpt-5-2026-08",
                        Kind = CalibrationReviewerKind.HostAi,
                        Role = CalibrationReviewerRole.Teacher,
                        ModelId = "openai-codex",
                        ModelVersion = "gpt-5",
                    },
                ],
                Notes =
                    "Single host-AI teacher; no independent correction or adjudication. Candidate totals, category totals, " +
                    "hours, confidence, explanations, source work-item IDs, and partition counts were hidden. The teacher " +
                    "inspected pinned public source and digest-verified static evidence, then applied the disclosed bounded " +
                    "logical-review scale. Tests were not executed and are assumed passing under the default policy.",
            },
            Capabilities = [.. packet.Targets.Select(target =>
                DevelopmentReviewPolicy.Review(family.RepositoryName, target))],
        };

    private static void ValidateBlindPacket(
        CalibrationAuthoringPacket packet,
        SamplingFamily family,
        ReproductionFamily reproduction)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(packet);
        if (errors.Count > 0 ||
            packet.CandidateVisibility != CalibrationCandidateVisibility.Blind ||
            packet.Repository.Id != family.Id ||
            packet.Targets.Count != reproduction.Analysis!.PacketTargetCount ||
            packet.Targets.Any(target => target.SourceWorkItemIds.Count > 0 ||
                target.Candidate.Hours is not null ||
                target.Candidate.Confidence is not null ||
                target.Candidate.Reason is not null))
        {
            throw new InvalidDataException($"{family.RepositoryName} packet is not strictly blind and valid.");
        }
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<T>(
            await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
            JsonOptions) ?? throw new InvalidDataException($"'{path}' is empty.");

    private static async Task RequireDigestAsync(
        string path,
        string expected,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        string actual = $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Artifact digest mismatch for '{path}'.");
        }
    }

    private static string Slug(string repositoryName) => string.Concat(
        repositoryName.ToLowerInvariant().Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');
}
