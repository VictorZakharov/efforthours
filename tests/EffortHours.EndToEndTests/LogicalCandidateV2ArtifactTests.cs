using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class LogicalCandidateV2ArtifactTests
{
    private const string ModelPath =
        "calibration/corpora/public-readiness/0.7.0.logical-capability-model.json";
    private const string NumericalPath =
        "calibration/corpora/public-readiness/0.7.0.candidate-preflight.json";
    private const string OperationalPath =
        "calibration/corpora/public-readiness/0.8.0.candidate-operational-preflight.json";
    private const string ImplementationCommit =
        "6962a3a49911ae230f2df13b5b05f8aded5c7e12";
    private const string ModelDigest =
        "sha256:f53b5af09b5adf0d3efed5339e9309156f026b3378c6b69e979467b92524ae93";
    private const string NumericalDigest =
        "sha256:4a12b35e7088d54e8fee6240c5a478f007d6a714bdc1b089398591de1c52c333";
    private const string OperationalDigest =
        "sha256:609307d5a366b52c18118db2ef9f79e46d86565b5419aa767e0cbbf7f1fe8ec8";

    [Fact]
    public void V2ModelFreezesTheSeparateSpecificationCeilingAndDevelopmentLineage()
    {
        JsonElement model = Read(ModelPath, ModelDigest);

        Assert.Equal("repository-logical-capability-model/0.2.0", String(model, "modelVersion"));
        Assert.Equal("logical-capability/0.2.0", String(model, "candidateId"));
        Assert.Equal(
            "candidate-logical-capability/0.2.0+seed-rules/0.4.0",
            String(model, "estimatorVersion"));
        Assert.Equal("logical-capability-features/1.1.0", String(model, "featureContractVersion"));
        Assert.Equal("MIT", String(model, "licenseExpression"));

        JsonElement training = model.GetProperty("training");
        Assert.Equal(ImplementationCommit, String(training, "implementationCommit"));
        Assert.Equal(15, training.GetProperty("repositoryCount").GetInt32());
        Assert.Equal(2030, training.GetProperty("targetCount").GetInt32());
        Assert.Equal(15, training.GetProperty("sources").GetArrayLength());

        JsonElement point = model.GetProperty("point");
        Assert.Equal(0.25m, point.GetProperty("minimumFactor").GetDecimal());
        Assert.Equal(3m, point.GetProperty("maximumFactor").GetDecimal());
        JsonElement ceiling = Assert.Single(
            point.GetProperty("maximumFactorOverrides").EnumerateArray());
        Assert.Equal("specification-comprehension", String(ceiling, "workItemKind"));
        Assert.Equal(4m, ceiling.GetProperty("maximumFactor").GetDecimal());
        JsonElement[] factors = [.. point.GetProperty("factors").EnumerateArray()];
        Assert.Equal(144, factors.Length);
        Assert.All(factors, factor => Assert.InRange(
            factor.GetProperty("factor").GetDecimal(),
            0.25m,
            String(factor, "workItemKind") == "specification-comprehension" ? 4m : 3m));
        Assert.Equal(2, factors.Count(factor =>
            String(factor, "workItemKind") == "specification-comprehension" &&
            factor.GetProperty("factor").GetDecimal() == 4m));

        JsonElement range = model.GetProperty("range");
        Assert.Equal(0.15m, range.GetProperty("lowerQuantile").GetDecimal());
        Assert.Equal(0.8m, range.GetProperty("upperQuantile").GetDecimal());
        Assert.Equal(3, range.GetProperty("minimumExactGroupSamples").GetInt32());
        Assert.Equal(145, range.GetProperty("factors").GetArrayLength());
    }

    [Fact]
    public void V2NumericalPreflightPassesAllSixteenGatesWithoutOpeningHoldouts()
    {
        JsonElement report = Read(NumericalPath, NumericalDigest);

        Assert.Equal(
            "development-numerical-gates-passed-operational-preflight-pending",
            String(report, "status"));
        Assert.Equal(ModelDigest, String(
            report.GetProperty("inputs").GetProperty("candidateModel"),
            "digest"));
        JsonElement candidate = report.GetProperty("candidate");
        Assert.Equal("logical-capability/0.2.0", String(candidate, "id"));
        JsonElement metrics = candidate.GetProperty("developmentMetrics");
        Assert.Equal(0.1137m, Decimal(metrics, "repositoryExpectedWape"));
        Assert.Equal(0.5192m, Decimal(metrics, "relativeWapeImprovement"));
        Assert.Equal(0.0094m, Decimal(metrics, "absoluteAggregateBias"));
        Assert.Equal(0.8667m, Decimal(metrics, "repositoryExpectedCoverage"));
        Assert.Equal(0.4405m, Decimal(metrics, "meanRepositoryNormalizedWidth"));
        Assert.Equal(0.8217m, Decimal(metrics, "matchedTargetExpectedCoverage"));
        Assert.Equal(0.7406m, Decimal(metrics, "matchedTargetMeanNormalizedWidth"));
        JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
        Assert.Equal(16, gates.Count(gate => String(gate, "status") == "passed"));
        Assert.Equal(12, gates.Count(gate => String(gate, "status") == "not-evaluated"));
        Assert.DoesNotContain(gates, gate => String(gate, "status") == "failed");
        AssertClosedHoldouts(report);
    }

    [Fact]
    public void V2OperationalPreflightPassesFiveGatesAndLeavesMeasurementsPending()
    {
        JsonElement report = Read(OperationalPath, OperationalDigest);

        Assert.Equal(
            "development-operational-gates-passed-measured-preflight-pending",
            String(report, "status"));
        JsonElement inputs = report.GetProperty("inputs");
        Assert.Equal(ModelDigest, String(inputs.GetProperty("candidateModel"), "digest"));
        Assert.Equal(NumericalDigest, String(inputs.GetProperty("numericalPreflight"), "digest"));
        JsonElement candidate = report.GetProperty("candidate");
        Assert.Equal("logical-capability/0.2.0", String(candidate, "id"));
        Assert.Equal("operationally-eligible-measurement-pending", String(candidate, "status"));
        JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
        Assert.Equal(5, gates.Count(gate => String(gate, "status") == "passed"));
        Assert.Equal(7, gates.Count(gate => String(gate, "status") == "not-evaluated"));
        Assert.DoesNotContain(gates, gate => String(gate, "status") == "failed");
        JsonElement category = Assert.Single(gates, gate =>
            String(gate, "id") == "material-category-agreement");
        Assert.Contains("pooled-wape=0.2075", String(category, "observed"), StringComparison.Ordinal);
        Assert.Contains("seed=0.4331", String(category, "observed"), StringComparison.Ordinal);
        Assert.Contains("violations=0[]", String(category, "observed"), StringComparison.Ordinal);
        AssertClosedHoldouts(report);
        JsonElement decision = report.GetProperty("decision");
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
        Assert.Contains("remaining measured", String(decision, "nextBoundary"), StringComparison.Ordinal);
    }

    private static void AssertClosedHoldouts(JsonElement report)
    {
        JsonElement holdouts = report.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        Assert.Contains("not-authored", String(holdouts, "validation"), StringComparison.Ordinal);
        Assert.Contains("not-authored", String(holdouts, "test"), StringComparison.Ordinal);
    }

    private static JsonElement Read(string relativePath, string expectedDigest)
    {
        string json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        Assert.Equal(expectedDigest, Digest(json));
        return JsonDocument.Parse(json).RootElement.Clone();
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
