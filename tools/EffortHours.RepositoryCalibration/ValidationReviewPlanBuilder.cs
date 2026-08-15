using System.Text.Json;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class ValidationReviewPlanBuilder
{
    public const string ExpectedOpeningDigest =
        "sha256:ff7ff99ff97104b01f6306751f60d05b399bbc334c9d436537f2d23e267a12b5";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(
        ValidationReviewOptions options,
        CancellationToken cancellationToken)
    {
        await RequireDigestAsync(
            options.OpeningPath,
            ExpectedOpeningDigest,
            cancellationToken).ConfigureAwait(false);
        SamplingPlan plan = await ReadAsync<SamplingPlan>(options.PlanPath, cancellationToken)
            .ConfigureAwait(false);
        ValidationOpeningManifest opening = await ReadAsync<ValidationOpeningManifest>(
            options.OpeningPath,
            cancellationToken).ConfigureAwait(false);
        ValidateBoundary(plan, opening);

        List<CalibrationReviewPlanRecord> records = [];
        foreach (ReproductionFamily reproduced in opening.Families
                     .OrderBy(family => family.Id, StringComparer.Ordinal))
        {
            SamplingFamily family = plan.Families.Single(item => item.Id == reproduced.Id);
            string packetPath = Path.Combine(
                options.PacketDirectory,
                reproduced.Analysis!.PacketFile);
            await RequireDigestAsync(
                packetPath,
                reproduced.Analysis.PacketSha256,
                cancellationToken).ConfigureAwait(false);
            string evidencePath = Path.Combine(
                options.OutputDirectory,
                Slug(family.RepositoryName),
                "repository-evidence.json");
            await RequireDigestAsync(
                evidencePath,
                reproduced.Analysis.EvidenceArtifactSha256,
                cancellationToken).ConfigureAwait(false);

            CalibrationAuthoringPacket packet = ContractJson.Deserialize<CalibrationAuthoringPacket>(
                await File.ReadAllTextAsync(packetPath, cancellationToken).ConfigureAwait(false));
            RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(
                await File.ReadAllTextAsync(evidencePath, cancellationToken).ConfigureAwait(false));
            ValidateBlindPacket(packet, family, reproduced);
            DevelopmentReviewEvidence reviewEvidence = new(evidence);
            reviewEvidence.RequireCompleteLineage(packet);
            records.Add(CreateRecord(family, reproduced, packet, reviewEvidence));
        }

        CalibrationReviewPlan reviewPlan = new()
        {
            CompilerVersion = CalibrationReviewCompiler.CompilerVersion,
            Id = "efforthours-public-readiness-validation",
            Version = "1.3.0",
            Description =
                "Complete nine-family validation-only host-AI teacher cohort from the frozen public-readiness matrix. " +
                "These logical weak-supervision labels were authored from strict-blind capability packets, verified " +
                "static evidence, and pinned public source after the finite candidate and selection rule were frozen. " +
                "No seed or challenger values were inspected or supplied to this command during judgment; " +
                "test remains sealed.",
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
                "Validation review plan is invalid: " +
                string.Join("; ", errors.Concat(schema.Errors)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(options.ReviewPlanPath)!);
        await File.WriteAllTextAsync(
            options.ReviewPlanPath,
            ContractJson.ToCanonicalDocument(json),
            cancellationToken).ConfigureAwait(false);
    }

    private static CalibrationReviewPlanRecord CreateRecord(
        SamplingFamily family,
        ReproductionFamily reproduction,
        CalibrationAuthoringPacket packet,
        DevelopmentReviewEvidence evidence) => new()
        {
            Id = $"record:{Slug(family.RepositoryName)}:implementation",
            Repository = packet.Repository,
            Profile = packet.Profile,
            BaselineId = packet.BaselineId,
            Partition = CalibrationPartition.Validation,
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
                CompletedOn = new DateOnly(2026, 8, 15),
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
                    "Single host-AI teacher; no independent correction or adjudication. Seed and challenger totals, " +
                    "category totals, hours, confidence, explanations, work-item IDs, and partition counts were not " +
                    "inspected or supplied to the review compiler. The teacher inspected pinned public source and " +
                    "digest-verified static evidence, then " +
                    "applied the disclosed bounded logical-review scale. Tests were not executed and are assumed passing.",
            },
            Capabilities = [.. packet.Targets.Select(target =>
                DevelopmentReviewPolicy.Review(family.RepositoryName, target, evidence))],
        };

    private static void ValidateBoundary(
        SamplingPlan plan,
        ValidationOpeningManifest opening)
    {
        string[] expectedIds = [.. plan.Families
            .Where(family => family.Partition == "validation")
            .Select(family => family.Id)
            .Order(StringComparer.Ordinal)];
        string[] actualIds = [.. opening.Families.Select(family => family.Id)
            .Order(StringComparer.Ordinal)];
        if (plan.Id != "efforthours-public-readiness" ||
            plan.Version != "0.1.0" ||
            opening.SchemaVersion != "repository-validation-opening/1.0.0" ||
            opening.Id != "efforthours-public-readiness-validation" ||
            opening.Version != "1.3.0" ||
            opening.Status != "strict-blind-validation-packets-generated-candidate-values-unavailable" ||
            opening.OpeningImplementationCommit != "efad050c3b0985ec4d8fdf9de7673222103a12fd" ||
            opening.CandidateManifest.Digest != ValidationBoundaryVerifier.ExpectedCandidateManifestDigest ||
            opening.SamplingPlan.Digest != ValidationBoundaryVerifier.ExpectedPlanDigest ||
            opening.ReproductionManifest.Digest != ValidationBoundaryVerifier.ExpectedReproductionDigest ||
            opening.HoldoutCustody.Digest != ValidationBoundaryVerifier.ExpectedCustodyDigest ||
            opening.Failures.Count != 0 ||
            opening.ContaminatedFamilies.Count != 0 ||
            opening.Boundary.ValidationCandidateOutputsGenerated ||
            opening.Boundary.ValidationLabelsAuthored ||
            opening.Boundary.TestSourceAccess != "not-performed" ||
            opening.Boundary.TestCandidateOutputsGenerated ||
            opening.Boundary.TestLabelsAuthored ||
            !actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal) ||
            opening.Families.Any(family =>
                family.Partition != "validation" ||
                family.AnalysisStatus != "generated-blind-validation" ||
                family.Analysis is null ||
                family.Analysis.EstimatorVersion != "seed-rules/0.4.0"))
        {
            throw new InvalidDataException("Validation opening is not the exact frozen blind boundary.");
        }
    }

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
        string actual = await JsonArtifactDigest.ComputeFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Artifact digest mismatch for '{path}'.");
        }
    }

    private static string Slug(string repositoryName) => string.Concat(
        repositoryName.ToLowerInvariant().Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');
}
