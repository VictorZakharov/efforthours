using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class ValidationSelectionArtifactTests
{
    private const string SelectionDigest =
        "sha256:fff040cdb13aba610c410d8e101e7770a99e0365d4d0ccf3b915f7de99ab5c56";

    [Fact]
    public async Task FrozenValidationRejectsCandidateAndKeepsTestSealed()
    {
        string root = FindRepositoryRoot();
        string directory = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "1.3.0");
        string decisionJson = await File.ReadAllTextAsync(
            Path.Combine(directory, "1.3.0.validation-selection.json"));
        ValidationSelectionReport decision =
            ContractJson.Deserialize<ValidationSelectionReport>(decisionJson);
        CalibrationEvaluationReport seed = ContractJson.Deserialize<CalibrationEvaluationReport>(
            await File.ReadAllTextAsync(
                Path.Combine(directory, "1.3.0.seed-validation-evaluation.json")));
        CalibrationEvaluationReport candidate =
            ContractJson.Deserialize<CalibrationEvaluationReport>(
                await File.ReadAllTextAsync(
                    Path.Combine(directory, "1.3.0.candidate-validation-evaluation.json")));

        Assert.Equal(SelectionDigest, JsonArtifactDigest.Compute(decisionJson));
        Assert.Equal("validation-rejected-all-challengers-test-sealed", decision.Status);
        Assert.Equal("rejected-all", decision.Decision.Status);
        Assert.Null(decision.Decision.SelectedCandidateId);
        Assert.False(decision.Decision.TestAuthorized);
        Assert.False(decision.Decision.CandidateAdmitted);
        Assert.Equal("seed-rules/0.4.0", decision.Decision.ShippedEstimatorVersion);
        Assert.False(decision.Boundary.TestSourceAccessed);
        Assert.False(decision.Boundary.TestLabelsAuthored);
        Assert.False(decision.Boundary.TestCandidateOutputsGenerated);

        ValidationSelectionCandidate challenger = Assert.Single(decision.Challengers);
        Assert.False(challenger.Eligible);
        Assert.Equal(0.2279m, decision.Baseline.RepositoryExpectedWape);
        Assert.Equal(0.0940m, challenger.Metrics.RepositoryExpectedWape);
        Assert.Equal(0.5875m, challenger.Metrics.RelativeWapeImprovement);
        Assert.Equal(0.6667m, challenger.Metrics.RepositoryExpectedCoverage);
        Assert.Equal(0.7150m, challenger.Metrics.MatchedTargetExpectedCoverage);
        Assert.Equal(0.8452m, challenger.Metrics.MatchedTargetMeanNormalizedWidth);
        Assert.Equal(4, challenger.BoundaryGates.Count(gate => gate.Passed));
        Assert.Equal(15, challenger.ValidationGates.Count(gate => gate.Passed));
        Assert.Equal(6, challenger.ValidationGates.Count(gate => !gate.Passed));
        Assert.Equal(12, challenger.FrozenOperationalGates.Count(gate => gate.Passed));
        Assert.Equal(
            [
                "matched-target-expected-coverage",
                "matched-target-normalized-width",
                "material-category-agreement",
                "median-repository-error",
                "per-family-ordinary-error",
                "repository-expected-coverage",
            ],
            challenger.ValidationGates.Where(gate => !gate.Passed)
                .Select(gate => gate.Id)
                .Order(StringComparer.Ordinal));

        Assert.Equal(CalibrationPartition.Validation, seed.Partition);
        Assert.Equal(CalibrationPartition.Validation, candidate.Partition);
        Assert.Equal("seed-rules/0.4.0", Assert.Single(seed.CandidateEstimatorVersions));
        Assert.Equal(
            "candidate-logical-capability/0.3.0+seed-rules/0.4.0",
            Assert.Single(candidate.CandidateEstimatorVersions));
        Assert.Equal(
            decision.Inputs.SeedEvaluation.Digest,
            JsonArtifactDigest.Compute(ContractJson.SerializeDocument(seed)));
        Assert.Equal(
            decision.Inputs.CandidateEvaluation.Digest,
            JsonArtifactDigest.Compute(ContractJson.SerializeDocument(candidate)));
        Assert.DoesNotContain("G:\\", decisionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users\\", decisionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/home/", decisionJson, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
