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
