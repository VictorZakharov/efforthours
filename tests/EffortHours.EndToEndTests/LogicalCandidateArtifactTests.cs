using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class LogicalCandidateArtifactTests
{
    private const string ModelPath =
        "calibration/corpora/public-readiness/0.5.0.logical-capability-model.json";
    private const string PreflightPath =
        "calibration/corpora/public-readiness/0.5.0.candidate-preflight.json";
    private const string ImplementationCommit =
        "e12765cb0312622a256de4d81798cd50884f9b49";
    private const string ModelDigest =
        "sha256:cde32aaea9d31baef2d0a588b488e7e0b2078a0b2fdb485ae5765f05e37de479";
    private const string PreflightDigest =
        "sha256:220eb04d95b7da022867afb8cb935d99731a12cbc268afeb4a39fc565aabc64f";

    [Fact]
    public void LogicalCapabilityModelFreezesDevelopmentOnlyLineageAndTables()
    {
        string root = FindRepositoryRoot();
        string json = File.ReadAllText(Path.Combine(root, ModelPath));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement model = document.RootElement;

        Assert.Equal(ModelDigest, Digest(json));
        Assert.Equal(
            "repository-logical-capability-model/0.1.0",
            String(model, "modelVersion"));
        Assert.Equal("logical-capability/0.1.0", String(model, "candidateId"));
        Assert.Equal(
            "candidate-logical-capability/0.1.0+seed-rules/0.4.0",
            String(model, "estimatorVersion"));
        Assert.Equal("logical-capability-features/1.0.0", String(model, "featureContractVersion"));
        Assert.Equal("MIT", String(model, "licenseExpression"));

        JsonElement training = model.GetProperty("training");
        Assert.Equal("efforthours-public-readiness-development", String(training, "corpusId"));
        Assert.Equal("0.3.0", String(training, "corpusVersion"));
        Assert.Equal(ImplementationCommit, String(training, "implementationCommit"));
        Assert.Equal(15, training.GetProperty("repositoryCount").GetInt32());
        Assert.Equal(2030, training.GetProperty("targetCount").GetInt32());
        JsonElement[] sources = [.. training.GetProperty("sources").EnumerateArray()];
        Assert.Equal(15, sources.Length);
        Assert.Equal(15, sources.Select(source => String(source, "recordId")).Distinct().Count());
        Assert.All(sources, source =>
        {
            Assert.Matches("^sha256:[0-9a-f]{64}$", String(source, "sourceDigest"));
            Assert.Matches("^sha256:[0-9a-f]{64}$", String(source, "estimateDigest"));
            Assert.Matches("^sha256:[0-9a-f]{64}$", String(source, "evidenceDigest"));
        });

        JsonElement point = model.GetProperty("point");
        Assert.Equal("evidence-logical-units/0.1.0", String(point, "scorerVersion"));
        Assert.Equal(0.25m, point.GetProperty("minimumFactor").GetDecimal());
        Assert.Equal(3m, point.GetProperty("maximumFactor").GetDecimal());
        JsonElement[] pointFactors = [.. point.GetProperty("factors").EnumerateArray()];
        Assert.Equal(144, pointFactors.Length);
        Assert.All(pointFactors, factor => Assert.InRange(
            factor.GetProperty("factor").GetDecimal(),
            0.25m,
            3m));

        JsonElement range = model.GetProperty("range");
        Assert.Equal(0.15m, range.GetProperty("lowerQuantile").GetDecimal());
        Assert.Equal(0.8m, range.GetProperty("upperQuantile").GetDecimal());
        Assert.Equal(3, range.GetProperty("minimumExactGroupSamples").GetInt32());
        JsonElement[] rangeFactors = [.. range.GetProperty("factors").EnumerateArray()];
        Assert.Equal(146, rangeFactors.Length);
        Assert.All(rangeFactors, factor =>
        {
            Assert.InRange(factor.GetProperty("lowFactor").GetDecimal(), 0m, 1m);
            Assert.True(factor.GetProperty("highFactor").GetDecimal() >= 1m);
            Assert.True(factor.GetProperty("sampleCount").GetInt32() >= 3);
        });
    }

    [Fact]
    public void LogicalCapabilityPreflightPassesNumericalGatesWithoutOpeningHoldouts()
    {
        string root = FindRepositoryRoot();
        string json = File.ReadAllText(Path.Combine(root, PreflightPath));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement report = document.RootElement;

        Assert.Equal(PreflightDigest, Digest(json));
        Assert.Equal("repository-candidate-preflight/0.2.0", String(report, "preflightVersion"));
        Assert.Equal(
            "development-numerical-gates-passed-operational-preflight-pending",
            String(report, "status"));
        Assert.Equal("development", String(report, "partition"));
        JsonElement modelReference = report.GetProperty("inputs").GetProperty("candidateModel");
        Assert.Equal("efforthours-public-readiness-logical-capability", String(modelReference, "id"));
        Assert.Equal("repository-logical-capability-model/0.1.0", String(modelReference, "version"));
        Assert.Equal(ModelDigest, String(modelReference, "digest"));

        JsonElement candidate = report.GetProperty("candidate");
        Assert.Equal("logical-capability/0.1.0", String(candidate, "id"));
        Assert.Equal(
            "numerically-eligible-operational-preflight-pending",
            String(candidate, "status"));
        Assert.Empty(candidate.GetProperty("rejectionReasons").EnumerateArray());
        Assert.Equal(
            ImplementationCommit,
            String(candidate.GetProperty("configuration"), "implementationCommit"));

        JsonElement metrics = candidate.GetProperty("developmentMetrics");
        Assert.Equal(0.1141m, Decimal(metrics, "repositoryExpectedWape"));
        Assert.Equal(0.5175m, Decimal(metrics, "relativeWapeImprovement"));
        Assert.Equal(0.0108m, Decimal(metrics, "absoluteAggregateBias"));
        Assert.Equal(0.9333m, Decimal(metrics, "familyOrdinaryErrorPassRate"));
        Assert.Equal(0.8667m, Decimal(metrics, "repositoryExpectedCoverage"));
        Assert.Equal(0.4403m, Decimal(metrics, "meanRepositoryNormalizedWidth"));
        Assert.Equal(0.8202m, Decimal(metrics, "matchedTargetExpectedCoverage"));
        Assert.Equal(0.7407m, Decimal(metrics, "matchedTargetMeanNormalizedWidth"));

        JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
        Assert.Equal(28, gates.Length);
        Assert.Equal(16, gates.Count(gate => String(gate, "status") == "passed"));
        Assert.Equal(12, gates.Count(gate => String(gate, "status") == "not-evaluated"));
        Assert.DoesNotContain(gates, gate => String(gate, "status") == "failed");
        Assert.All(
            gates.Where(gate => String(gate, "status") == "not-evaluated"),
            gate => Assert.False(gate.GetProperty("passed").GetBoolean()));

        JsonElement holdouts = report.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        Assert.Contains("not-authored", String(holdouts, "validation"), StringComparison.Ordinal);
        Assert.Contains("not-authored", String(holdouts, "test"), StringComparison.Ordinal);
        JsonElement decision = report.GetProperty("decision");
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
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
