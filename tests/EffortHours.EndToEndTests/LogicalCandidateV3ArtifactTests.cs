using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class LogicalCandidateV3ArtifactTests
{
    private const string ModelPath =
        "calibration/corpora/public-readiness/1.0.0.logical-capability-model.json";
    private const string NumericalPath =
        "calibration/corpora/public-readiness/1.0.0.candidate-preflight.json";
    private const string OperationalPath =
        "calibration/corpora/public-readiness/1.1.0.candidate-operational-preflight.json";
    private const string MutationPath =
        "calibration/corpora/public-readiness/1.1.0/0.8.0.candidate-mutation-report.json";
    private const string ImplementationCommit =
        "7e5451c807cef3ce22bedd1bc374ab519882b21c";
    private const string ModelDigest =
        "sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea";
    private const string NumericalDigest =
        "sha256:c5614f11513619b7293f454d58cda87efdc192e181ef3551270b1d3bc2a9ba97";
    private const string OperationalDigest =
        "sha256:eb091d5de07399a169dcef4bc9a08f9feb129563dd61d4101eed6c40cab7ddc8";
    private const string MutationDigest =
        "sha256:59fb04a4e677795198a2b4c81d5aacf871eef0fdbd67c0bbc577f7992b5f79c0";

    [Fact]
    public void V3ModelAndNumericalPreflightFreezeTheDevelopmentPass()
    {
        JsonDocument modelDocument = Read(ModelPath, ModelDigest);
        using (modelDocument)
        {
            JsonElement model = modelDocument.RootElement;
            Assert.Equal("repository-logical-capability-model/0.3.0", String(model, "modelVersion"));
            Assert.Equal("logical-capability/0.3.0", String(model, "candidateId"));
            Assert.Equal(
                "candidate-logical-capability/0.3.0+seed-rules/0.4.0",
                String(model, "estimatorVersion"));
            Assert.Equal("logical-capability-features/1.3.0", String(model, "featureContractVersion"));
            Assert.Equal(
                "normalized-evidence-intent-bounds/0.2.0",
                String(model.GetProperty("point"), "scorerVersion"));
            JsonElement pointFeatures = model.GetProperty("point").GetProperty("features");
            Assert.Contains(
                pointFeatures.EnumerateArray(),
                feature => feature.GetString() == "operation-only-data-evidence-count");
            Assert.Contains(
                pointFeatures.EnumerateArray(),
                feature => feature.GetString() == "semantic-boundary-minimums");
            JsonElement rangeFeatures = model.GetProperty("range").GetProperty("features");
            Assert.Contains(
                rangeFeatures.EnumerateArray(),
                feature => feature.GetString() == "bounded-kind-low-minimum");
            Assert.Contains(
                rangeFeatures.EnumerateArray(),
                feature => feature.GetString() == "minimum-high-factor");
            Assert.Equal(ImplementationCommit, String(model.GetProperty("training"), "implementationCommit"));
        }

        JsonDocument reportDocument = Read(NumericalPath, NumericalDigest);
        using (reportDocument)
        {
            JsonElement report = reportDocument.RootElement;
            Assert.Equal(
                "development-numerical-gates-passed-operational-preflight-pending",
                String(report, "status"));
            JsonElement candidate = report.GetProperty("candidate");
            Assert.Equal("logical-capability/0.3.0", String(candidate, "id"));
            Assert.Equal(
                ImplementationCommit,
                String(candidate.GetProperty("configuration"), "implementationCommit"));
            JsonElement metrics = candidate.GetProperty("developmentMetrics");
            Assert.Equal(0.1009m, Decimal(metrics, "repositoryExpectedWape"));
            Assert.Equal(0.5734m, Decimal(metrics, "relativeWapeImprovement"));
            Assert.Equal(0.0103m, Decimal(metrics, "absoluteAggregateBias"));
            Assert.Equal(0.7601m, Decimal(metrics, "matchedTargetExpectedCoverage"));
            Assert.Equal(0.7321m, Decimal(metrics, "matchedTargetMeanNormalizedWidth"));

            JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
            Assert.Equal(16, gates.Count(gate => String(gate, "status") == "passed"));
            Assert.Equal(12, gates.Count(gate => String(gate, "status") == "not-evaluated"));
            Assert.DoesNotContain(gates, gate => String(gate, "status") == "failed");
            AssertClosedBoundary(report);
        }
    }

    [Fact]
    public void V3OperationalAndMutationArtifactsFreezeEveryCompletedDevelopmentGate()
    {
        JsonDocument reportDocument = Read(OperationalPath, OperationalDigest);
        using (reportDocument)
        {
            JsonElement report = reportDocument.RootElement;
            Assert.Equal(
                "development-operational-gates-passed-measured-preflight-pending",
                String(report, "status"));
            JsonElement inputs = report.GetProperty("inputs");
            Assert.Equal(ModelDigest, String(inputs.GetProperty("candidateModel"), "digest"));
            Assert.Equal(NumericalDigest, String(inputs.GetProperty("numericalPreflight"), "digest"));
            JsonElement candidate = report.GetProperty("candidate");
            Assert.Equal("logical-capability/0.3.0", String(candidate, "id"));
            Assert.Equal(
                ImplementationCommit,
                String(candidate.GetProperty("configuration"), "implementationCommit"));
            JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
            Assert.Equal(5, gates.Count(gate => String(gate, "status") == "passed"));
            Assert.Equal(7, gates.Count(gate => String(gate, "status") == "not-evaluated"));
            Assert.DoesNotContain(gates, gate => String(gate, "status") == "failed");
            AssertClosedBoundary(report);
        }

        JsonDocument mutationDocument = Read(MutationPath, MutationDigest);
        using (mutationDocument)
        {
            JsonElement report = mutationDocument.RootElement;
            Assert.Equal("efforthours-public-synthetic-mutations", String(report, "suiteId"));
            Assert.Equal("0.8.0", String(report, "suiteVersion"));
            Assert.Equal(88, report.GetProperty("caseCount").GetInt32());
            Assert.Equal(339, report.GetProperty("assertionCount").GetInt32());
            Assert.Equal(339, report.GetProperty("passedCount").GetInt32());
            Assert.Equal(0, report.GetProperty("failedCount").GetInt32());
            Assert.True(report.GetProperty("allPassed").GetBoolean());
            Assert.Contains(
                report.GetProperty("candidateEstimatorVersions").EnumerateArray(),
                version => version.GetString() ==
                    "candidate-logical-capability/0.3.0+seed-rules/0.4.0");
        }
    }

    private static void AssertClosedBoundary(JsonElement report)
    {
        JsonElement holdouts = report.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        JsonElement decision = report.GetProperty("decision");
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
    }

    private static JsonDocument Read(string relativePath, string expectedDigest)
    {
        string json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        Assert.Equal(expectedDigest, Digest(json));
        return JsonDocument.Parse(json);
    }

    private static decimal Decimal(JsonElement element, string property) =>
        element.GetProperty(property).GetDecimal();

    private static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString()!;

    private static string Digest(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()}";
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
