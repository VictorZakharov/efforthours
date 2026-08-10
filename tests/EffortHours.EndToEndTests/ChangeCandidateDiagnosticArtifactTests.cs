using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class ChangeCandidateDiagnosticArtifactTests
{
    [Fact]
    public void LogicalMarginalityCandidateKeepsFrozenAndHeldOutArtifactsSeparate()
    {
        string root = Path.Combine(
            FindRepositoryRoot(),
            "calibration",
            "changes",
            "public-real-expansion",
            "diagnostics",
            "change-seed-0.3.0");
        string reportsRoot = Path.Combine(root, "reports");
        string[] expectedNames =
        [
            "axios.change-estimate.json",
            "benchmarkdotnet.change-estimate.json",
            "p-limit.change-estimate.json",
            "spectre-console.change-estimate.json",
            "zod.change-estimate.json",
        ];
        Assert.Equal(
            expectedNames,
            Directory.GetFiles(reportsRoot, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal));

        Dictionary<string, decimal> expectedTotals = new(StringComparer.Ordinal)
        {
            ["axios"] = 11.25m,
            ["benchmarkdotnet"] = 5.75m,
            ["p-limit"] = 5.00m,
            ["spectre-console"] = 4.50m,
            ["zod"] = 10.00m,
        };
        List<string> artifactJson = [];
        foreach ((string name, decimal expected) in expectedTotals)
        {
            string json = File.ReadAllText(Path.Combine(reportsRoot, $"{name}.change-estimate.json"));
            ChangeEstimateReport report = ContractJson.Deserialize<ChangeEstimateReport>(json);
            artifactJson.Add(json);
            Assert.Empty(ContractValidation.Validate(report));
            Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeEstimateReport, json).IsValid);
            Assert.Equal("change-seed/0.3.0+seed-rules/0.2.1", report.EstimatorVersion);
            Assert.Equal(expected, report.TotalEffort.Expected);
        }

        CalibrationEvaluationReport development = ReadEvaluation(root, "development", artifactJson);
        CalibrationEvaluationReport validation = ReadEvaluation(root, "validation", artifactJson);
        AssertEvaluation(development, 3, 19.00m, 20.75m, 0.1447m, 0.0921m, 17, 14, 24, 14);
        AssertEvaluation(validation, 2, 16.00m, 15.75m, 0.0156m, -0.0156m, 12, 10, 16, 10);
        Assert.False(File.Exists(Path.Combine(root, "teacher-test-evaluation.json")));

        string combined = string.Concat(artifactJson);
        Assert.DoesNotContain("G:\\\\", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\\\", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static CalibrationEvaluationReport ReadEvaluation(
        string root,
        string partition,
        List<string> artifactJson)
    {
        string json = File.ReadAllText(Path.Combine(root, $"teacher-{partition}-evaluation.json"));
        CalibrationEvaluationReport report = ContractJson.Deserialize<CalibrationEvaluationReport>(json);
        artifactJson.Add(json);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.CalibrationEvaluation, json).IsValid);
        return report;
    }

    private static void AssertEvaluation(
        CalibrationEvaluationReport report,
        int records,
        decimal reviewed,
        decimal candidate,
        decimal wape,
        decimal bias,
        int targets,
        int matchedTargets,
        int references,
        int matchedReferences)
    {
        Assert.Equal(records, report.RecordCount);
        Assert.Equal(reviewed, report.RepositoryTotals.Expected.ReviewedHours);
        Assert.Equal(candidate, report.RepositoryTotals.Expected.CandidateHours);
        Assert.Equal(wape, report.RepositoryTotals.Expected.WeightedAbsolutePercentageError);
        Assert.Equal(bias, report.RepositoryTotals.Expected.AggregateBiasRate);
        Assert.Equal(targets, report.Match.TargetCount);
        Assert.Equal(matchedTargets, report.Match.MatchedTargetCount);
        Assert.Equal(references, report.Match.SourceWorkItemReferenceCount);
        Assert.Equal(matchedReferences, report.Match.MatchedSourceWorkItemReferenceCount);
        Assert.Equal(targets, report.Match.CandidateWorkItemCount);
        Assert.Equal(matchedTargets, report.Match.MatchedCandidateWorkItemCount);
        Assert.Equal(["change-seed/0.3.0+seed-rules/0.2.1"], report.CandidateEstimatorVersions);
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
