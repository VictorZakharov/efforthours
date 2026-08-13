using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class CalibrationReproductionArtifactTests
{
    private const string CohortDirectory = "calibration/corpora/public-readiness";

    [Fact]
    public void PublicReadinessReproductionKeepsHoldoutsUnanalyzedAndPacketsStrictlyBlind()
    {
        string root = FindRepositoryRoot();
        string cohortPath = Path.Combine(root, CohortDirectory);
        string manifestPath = Path.Combine(cohortPath, "0.2.0.reproduction-manifest.json");
        string custodyPath = Path.Combine(cohortPath, "0.2.0.holdout-custody.json");
        string packetDirectory = Path.Combine(cohortPath, "0.2.0", "authoring-packets");
        using JsonDocument plan = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(cohortPath, "0.1.0.sampling-plan.json")));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using JsonDocument custody = JsonDocument.Parse(File.ReadAllText(custodyPath));

        JsonElement[] plannedFamilies = [.. plan.RootElement.GetProperty("families").EnumerateArray()];
        JsonElement[] reproduced = [.. manifest.RootElement.GetProperty("families").EnumerateArray()];
        Assert.Equal("repository-calibration-reproduction/1.0.0",
            manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("0.2.0", manifest.RootElement.GetProperty("version").GetString());
        Assert.Equal(
            "sha256:utf8-lf-normalized-text",
            manifest.RootElement.GetProperty("jsonArtifactDigestPolicy").GetString());
        Assert.Equal(
            Sha256JsonArtifact(Path.Combine(cohortPath, "0.1.0.sampling-plan.json")),
            manifest.RootElement.GetProperty("samplingPlan").GetProperty("digest").GetString());
        Assert.Equal(33, reproduced.Length);
        Assert.Equal(
            plannedFamilies.Select(Id).Order(StringComparer.Ordinal),
            reproduced.Select(Id).Order(StringComparer.Ordinal));

        JsonElement[] development = [.. reproduced.Where(IsDevelopment)];
        JsonElement[] holdouts = [.. reproduced.Where(item => !IsDevelopment(item))];
        Assert.Equal(15, development.Length);
        Assert.Equal(18, holdouts.Length);
        Assert.All(holdouts, family =>
        {
            Assert.Equal("withheld-not-run", family.GetProperty("analysisStatus").GetString());
            Assert.False(family.TryGetProperty("analysis", out _));
        });

        string[] packetFiles = Directory.GetFiles(packetDirectory, "*.json", SearchOption.TopDirectoryOnly);
        Assert.Equal(15, packetFiles.Length);
        foreach (JsonElement family in development)
        {
            AssertDevelopmentPacket(family, packetDirectory);
        }

        AssertCustody(custody.RootElement, holdouts, manifestPath);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.Combine(cohortPath, "0.2.0"), "*", SearchOption.AllDirectories),
            path => Path.GetFileName(path).Contains("estimate", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Contains("evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicReadinessDevelopmentSliceIsTraceableWeakSupervisionOnly()
    {
        string root = FindRepositoryRoot();
        string cohortPath = Path.Combine(root, CohortDirectory);
        CalibrationReviewPlan plan = ContractJson.Deserialize<CalibrationReviewPlan>(File.ReadAllText(
            Path.Combine(cohortPath, "0.2.0.development-review-plan.json")));
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(File.ReadAllText(
            Path.Combine(cohortPath, "0.2.0.development-corpus.json")));
        string evaluationJson = File.ReadAllText(
            Path.Combine(cohortPath, "0.2.0.development-evaluation.json"));
        using JsonDocument evaluation = JsonDocument.Parse(evaluationJson);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(cohortPath, "0.2.0.reproduction-manifest.json")));

        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(corpus));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            ContractJson.Serialize(plan)).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            ContractJson.Serialize(corpus)).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationEvaluation,
            evaluationJson).IsValid);
        Assert.Equal(3, plan.Records.Count);
        Assert.Equal(3, corpus.Records.Count);

        string[] selected =
        [
            "repository:github.com/cartercommunity/carter",
            "repository:github.com/oqtane/oqtane.framework",
            "repository:github.com/tj/commander.js",
        ];
        Assert.Equal(selected, corpus.Records.Select(record => record.Repository.Id).Order(StringComparer.Ordinal));
        JsonElement[] reproduced = [.. manifest.RootElement.GetProperty("families").EnumerateArray()];
        foreach (CalibrationRecord record in corpus.Records)
        {
            Assert.Equal(CalibrationPartition.Development, record.Partition);
            Assert.Equal(CalibrationReviewStatus.TeacherEstimate, record.Review.Status);
            CalibrationReviewer reviewer = Assert.Single(record.Review.Reviewers);
            Assert.Equal(CalibrationReviewerKind.HostAi, reviewer.Kind);
            Assert.Equal(CalibrationReviewerRole.Teacher, reviewer.Role);
            JsonElement family = reproduced.Single(item => Id(item) == record.Repository.Id);
            Assert.Equal(
                family.GetProperty("analysis").GetProperty("estimateDigest").GetString(),
                record.SourceEstimateDigest);
        }

        CalibrationTarget[] targets = [.. corpus.Records.SelectMany(record => record.Targets)];
        Assert.Equal(169, targets.Length);
        Assert.Equal(7, targets.Count(target => target.Hours.Expected == 0m));
        Assert.All(targets.Where(target => target.Hours.Expected == 0m), target =>
            Assert.False(string.IsNullOrWhiteSpace(target.SizeException)));
        Assert.All(targets.Where(target => target.Hours.Expected > 8m), target =>
            Assert.False(string.IsNullOrWhiteSpace(target.SizeException)));

        JsonElement rootElement = evaluation.RootElement;
        Assert.Equal("development", rootElement.GetProperty("partition").GetString());
        Assert.Equal(3, rootElement.GetProperty("recordCount").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("ignoredCandidateCount").GetInt32());
        Assert.Equal(169, rootElement.GetProperty("match").GetProperty("targetCount").GetInt32());
        Assert.Equal(1m, rootElement.GetProperty("match").GetProperty("targetMatchRate").GetDecimal());
        JsonElement expected = rootElement.GetProperty("repositoryTotals").GetProperty("expected");
        Assert.Equal(4780.50m, expected.GetProperty("reviewedHours").GetDecimal());
        Assert.Equal(2843.50m, expected.GetProperty("candidateHours").GetDecimal());
        Assert.Equal(-0.4052m, expected.GetProperty("aggregateBiasRate").GetDecimal());
        Assert.Equal(
            0.6667m,
            rootElement.GetProperty("repositoryTotals").GetProperty("interval")
                .GetProperty("reviewedExpectedCoverage").GetDecimal());
    }

    private static void AssertDevelopmentPacket(JsonElement family, string packetDirectory)
    {
        Assert.Equal("generated-blind-development", family.GetProperty("analysisStatus").GetString());
        JsonElement analysis = family.GetProperty("analysis");
        string packetPath = Path.Combine(packetDirectory, analysis.GetProperty("packetFile").GetString()!);
        Assert.Equal(analysis.GetProperty("packetSha256").GetString(), Sha256JsonArtifact(packetPath));
        string json = File.ReadAllText(packetPath);
        CalibrationAuthoringPacket packet = ContractJson.Deserialize<CalibrationAuthoringPacket>(json);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(packet));
        Assert.Equal("calibration-authoring/0.2.0", packet.AuthoringVersion);
        Assert.Equal(CalibrationCandidateVisibility.Blind, packet.CandidateVisibility);
        Assert.Equal(family.GetProperty("id").GetString(), packet.Repository.Id);
        Assert.Equal(family.GetProperty("repositoryName").GetString(), packet.Repository.Name);
        Assert.Equal(analysis.GetProperty("evidenceSourceDigest").GetString(), packet.Repository.SourceDigest);
        Assert.Equal(analysis.GetProperty("estimateDigest").GetString(), packet.Candidate.EstimateDigest);
        Assert.Equal(analysis.GetProperty("packetTargetCount").GetInt32(), packet.Targets.Count);
        Assert.Null(packet.Candidate.TotalHours);
        Assert.Empty(packet.Candidate.Categories);
        Assert.Equal(
            packet.Targets.Count,
            packet.Targets.Select(target => target.SourceCapabilityId)
                .Distinct(StringComparer.Ordinal).Count());
        Assert.All(packet.Targets, target =>
        {
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Confidence);
            Assert.Null(target.Candidate.Reason);
            Assert.Empty(target.SourceWorkItemIds);
            Assert.NotEmpty(target.EvidenceIds);
        });
    }

    private static void AssertCustody(
        JsonElement custody,
        JsonElement[] holdouts,
        string manifestPath)
    {
        Assert.Equal("repository-calibration-holdout-custody/1.0.0",
            custody.GetProperty("schemaVersion").GetString());
        Assert.Equal("source-custody-only-labels-not-authored",
            custody.GetProperty("status").GetString());
        Assert.Equal(
            Sha256JsonArtifact(manifestPath),
            custody.GetProperty("reproductionManifest").GetProperty("digest").GetString());
        JsonElement[] custodyFamilies = [.. custody.GetProperty("families").EnumerateArray()];
        Assert.Equal(18, custodyFamilies.Length);
        Assert.Equal(
            holdouts.Select(Id).Order(StringComparer.Ordinal),
            custodyFamilies.Select(Id).Order(StringComparer.Ordinal));
        Assert.All(custodyFamilies, family =>
        {
            Assert.Equal("withheld-not-run", family.GetProperty("analysisStatus").GetString());
            Assert.Equal("not-authored", family.GetProperty("labelStatus").GetString());
            Assert.Equal(JsonValueKind.Null, family.GetProperty("labelDigest").ValueKind);
            Assert.Matches(Sha256Pattern(), family.GetProperty("archiveSha256").GetString()!);
        });
    }

    private static bool IsDevelopment(JsonElement family) =>
        string.Equals(family.GetProperty("partition").GetString(), "development", StringComparison.Ordinal);

    private static string Id(JsonElement family) => family.GetProperty("id").GetString()!;

    private static string Sha256JsonArtifact(string path)
    {
        string normalized = File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return $"sha256:{Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant()}";
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
