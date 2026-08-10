using System.Text.Json;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class ChangeCalibrationArtifactTests
{
    private static readonly HashSet<string> ExactZeroCases =
    [
        "change:js-lib-formatting",
        "change:js-lib-move",
        "change:mixed-revert-range",
        "change:ts-package-generated",
    ];

    [Fact]
    public void PublicSyntheticArtifactsPreserveFrozenLineageAndWithheldTestBoundary()
    {
        string root = Path.Combine(
            FindRepositoryRoot(),
            "calibration",
            "changes",
            "public-synthetic");
        using JsonDocument index = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "0.1.0", "index.json")));
        JsonElement[] cases = [.. index.RootElement.GetProperty("cases").EnumerateArray()];
        Assert.Equal(24, cases.Length);
        Assert.Equal(8, cases.Select(item => item.GetProperty("repositoryFamilyId").GetString()).Distinct().Count());
        Assert.Equal(12, cases.Count(item => item.GetProperty("partition").GetString() == "development"));
        Assert.Equal(6, cases.Count(item => item.GetProperty("partition").GetString() == "validation"));
        Assert.Equal(6, cases.Count(item => item.GetProperty("partition").GetString() == "test"));

        foreach (JsonElement fixture in cases)
        {
            string id = fixture.GetProperty("id").GetString()!;
            string reportJson = ReadIndexed(root, fixture, "estimatePath");
            string packetJson = ReadIndexed(root, fixture, "blindPacketPath");
            ChangeEstimateReport report = ContractJson.Deserialize<ChangeEstimateReport>(reportJson);
            CalibrationAuthoringPacket packet = ContractJson.Deserialize<CalibrationAuthoringPacket>(packetJson);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.Empty(ContractValidation.Validate(packet));
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, reportJson).IsValid);
            Assert.True(ContractSchemaValidator.Validate(
                SchemaNames.CalibrationAuthoringPacket,
                packetJson).IsValid);
            Assert.Equal(fixture.GetProperty("estimateDigest").GetString(), CalibrationDigest.Compute(report));
            Assert.Equal(fixture.GetProperty("finalDeltaDigest").GetString(), packet.Change!.FinalDeltaDigest);
            Assert.Null(packet.Candidate.TotalHours);
            Assert.All(packet.Targets, target => Assert.Null(target.Candidate.Hours));
            if (ExactZeroCases.Contains(id))
            {
                Assert.Equal(new EffortRange { Low = 0m, Expected = 0m, High = 0m }, report.TotalEffort);
                Assert.Empty(report.WorkItems);
                Assert.Empty(packet.Targets);
            }
        }

        AssertTeacherArtifacts(root);
        Assert.False(File.Exists(Path.Combine(root, "0.1.0.teacher-test-evaluation.json")));
    }

    [Fact]
    public void PublicRealPilotPreservesCurrentEstimatorLineageAndBlindBoundary()
    {
        string repositoryRoot = FindRepositoryRoot();
        string root = Path.Combine(repositoryRoot, "calibration", "changes", "public-real");
        string reportJson = File.ReadAllText(Path.Combine(
            root,
            "0.1.0",
            "reports",
            "guardclauses-pr-333.change-estimate.json"));
        string authoringJson = File.ReadAllText(Path.Combine(
            root,
            "0.1.0",
            "authoring-packets",
            "guardclauses-pr-333.reference-authoring.json"));
        string planJson = File.ReadAllText(Path.Combine(root, "0.1.0.teacher-review-plan.json"));
        string corpusJson = File.ReadAllText(Path.Combine(root, "0.1.0.teacher-corpus.json"));
        string blindJson = File.ReadAllText(Path.Combine(root, "0.1.0.independent-review-packet.json"));
        string evaluationJson = File.ReadAllText(Path.Combine(
            root,
            "0.1.0.teacher-development-evaluation.json"));

        ChangeEstimateReport report = ContractJson.Deserialize<ChangeEstimateReport>(reportJson);
        CalibrationAuthoringPacket authoring = ContractJson.Deserialize<CalibrationAuthoringPacket>(
            authoringJson);
        CalibrationReviewPlan plan = ContractJson.Deserialize<CalibrationReviewPlan>(planJson);
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        CalibrationCorpusReviewPacket blind = ContractJson.Deserialize<CalibrationCorpusReviewPacket>(
            blindJson);
        CalibrationEvaluationReport evaluation = ContractJson.Deserialize<CalibrationEvaluationReport>(
            evaluationJson);

        Assert.Empty(ContractValidation.Validate(report));
        Assert.Empty(ContractValidation.Validate(authoring));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(corpus));
        Assert.Empty(ContractValidation.Validate(blind));
        Assert.Empty(ContractValidation.Validate(evaluation));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, reportJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            authoringJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationReviewPlan, planJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationCorpus, corpusJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPacket,
            blindJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationEvaluation,
            evaluationJson).IsValid);

        Assert.Equal("change-seed/0.2.0+seed-rules/0.2.1", report.EstimatorVersion);
        Assert.Equal(
            new EffortRange { Low = 1.75m, Expected = 4.25m, High = 6.75m },
            report.TotalEffort);
        Assert.Equal("36051ca70d97183e2ad2091a14b65a384f298c47", report.Selection.Base.ObjectId);
        Assert.Equal("967e717bb88fa0ee0b87d054ca255934105e0f7e", report.Selection.Head.ObjectId);
        Assert.Equal(CalibrationDigest.Compute(report), authoring.Candidate.EstimateDigest);
        Assert.Equal(CalibrationCandidateVisibility.Reference, authoring.CandidateVisibility);

        CalibrationReviewPlanRecord planned = Assert.Single(plan.Records);
        CalibrationRecord record = Assert.Single(corpus.Records);
        Assert.Equal("repository:github.com/ardalis/guardclauses", record.Repository.Id);
        Assert.Equal(CalibrationPartition.Development, record.Partition);
        Assert.Equal(CalibrationDataClassification.PublicRedistributable, record.Source.DataClassification);
        Assert.Equal("MIT", record.Source.LicenseExpression);
        Assert.True(record.Source.RedistributionAllowed);
        Assert.Equal(report.EstimatorVersion, record.SourceEstimatorVersion);
        Assert.Equal(CalibrationDigest.Compute(report), record.SourceEstimateDigest);
        Assert.Equal(authoring.Change!.FinalDeltaDigest, record.Change!.FinalDeltaDigest);
        Assert.Equal(planned.Change!.FinalDeltaDigest, record.Change.FinalDeltaDigest);
        Assert.Equal(CalibrationReviewStatus.TeacherEstimate, record.Review.Status);
        CalibrationReviewer reviewer = Assert.Single(record.Review.Reviewers);
        Assert.Equal(CalibrationReviewerKind.HostAi, reviewer.Kind);
        Assert.Equal("sol-max", reviewer.ModelVersion);

        Assert.Equal(5, record.Targets.Count);
        Assert.Equal(2.25m, record.Targets.Sum(target => target.Hours.Low));
        Assert.Equal(4.00m, record.Targets.Sum(target => target.Hours.Expected));
        Assert.Equal(6.25m, record.Targets.Sum(target => target.Hours.High));
        Assert.Equal(CalibrationDigest.Compute(corpus), blind.SourceCorpus.Digest);
        Assert.Equal(CalibrationCandidateVisibility.Blind, blind.CandidateVisibility);
        Assert.All(blind.Records.SelectMany(item => item.Targets), target =>
        {
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Rationale);
            Assert.Null(target.Candidate.SizeException);
        });

        Assert.Equal(CalibrationPartition.Development, evaluation.Partition);
        Assert.Equal(4.00m, evaluation.RepositoryTotals.Expected.ReviewedHours);
        Assert.Equal(4.25m, evaluation.RepositoryTotals.Expected.CandidateHours);
        Assert.Equal(0.0625m, evaluation.RepositoryTotals.Expected.WeightedAbsolutePercentageError);

        string publicPilotJson = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "calibration",
            "corpora",
            "public-pilot",
            "0.1.0.corpus.json"));
        CalibrationCorpus publicPilot = ContractJson.Deserialize<CalibrationCorpus>(publicPilotJson);
        CalibrationRecord familyRecord = Assert.Single(
            publicPilot.Records,
            item => item.Repository.Id == record.Repository.Id);
        Assert.Equal(familyRecord.Partition, record.Partition);

        string combined = reportJson + authoringJson + planJson + corpusJson + blindJson + evaluationJson;
        Assert.DoesNotContain("G:\\\\", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\\\", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertTeacherArtifacts(string root)
    {
        string planJson = File.ReadAllText(Path.Combine(root, "0.1.0.teacher-review-plan.json"));
        string corpusJson = File.ReadAllText(Path.Combine(root, "0.1.0.teacher-corpus.json"));
        string packetJson = File.ReadAllText(Path.Combine(root, "0.1.0.independent-review-packet.json"));
        CalibrationReviewPlan plan = ContractJson.Deserialize<CalibrationReviewPlan>(planJson);
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        CalibrationCorpusReviewPacket packet = ContractJson.Deserialize<CalibrationCorpusReviewPacket>(packetJson);
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(corpus));
        Assert.Empty(ContractValidation.Validate(packet));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationReviewPlan, planJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationCorpus, corpusJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPacket,
            packetJson).IsValid);
        Assert.Equal(24, plan.Records.Count);
        Assert.Equal(24, corpus.Records.Count);
        CalibrationTarget[] targets = [.. corpus.Records.SelectMany(record => record.Targets)];
        Assert.Equal(121, targets.Length);
        Assert.Equal(22, targets.Count(target => target.Hours.Expected == 0m));
        Assert.Equal(CalibrationDigest.Compute(corpus), packet.SourceCorpus.Digest);
        Assert.Equal(121, packet.Records.Sum(record => record.Targets.Count));
        Assert.All(packet.Records.SelectMany(record => record.Targets), target =>
        {
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Rationale);
            Assert.Null(target.Candidate.SizeException);
        });

        AssertEvaluation(root, "development", 12, 38.50m, 41.25m, 0.3701m);
        AssertEvaluation(root, "validation", 6, 26.00m, 27.50m, 0.0962m);
    }

    private static void AssertEvaluation(
        string root,
        string partition,
        int recordCount,
        decimal reviewedHours,
        decimal candidateHours,
        decimal wape)
    {
        string json = File.ReadAllText(Path.Combine(root, $"0.1.0.teacher-{partition}-evaluation.json"));
        CalibrationEvaluationReport report = ContractJson.Deserialize<CalibrationEvaluationReport>(json);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationEvaluation, json).IsValid);
        Assert.Equal(recordCount, report.RecordCount);
        Assert.Equal(reviewedHours, report.RepositoryTotals.Expected.ReviewedHours);
        Assert.Equal(candidateHours, report.RepositoryTotals.Expected.CandidateHours);
        Assert.Equal(wape, report.RepositoryTotals.Expected.WeightedAbsolutePercentageError);
    }

    private static string ReadIndexed(string root, JsonElement fixture, string property)
    {
        string relativePath = fixture.GetProperty(property).GetString()!
            .Replace('/', Path.DirectorySeparatorChar);
        return File.ReadAllText(Path.Combine(root, "0.1.0", relativePath));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EffortHours.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
