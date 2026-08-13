using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class CandidatePreflightArtifactTests
{
    private const string RelativePath =
        "calibration/corpora/public-readiness/0.4.0.candidate-preflight.json";

    [Fact]
    public void PublicReadinessCandidatePreflightFailsClosedWithoutOpeningHoldouts()
    {
        string root = FindRepositoryRoot();
        string json = File.ReadAllText(Path.Combine(root, RelativePath));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement report = document.RootElement;

        Assert.Equal("repository-candidate-preflight/0.1.0", String(report, "preflightVersion"));
        Assert.Equal("repository-model-admission/1.0.0", String(report, "policyVersion"));
        Assert.Equal("rejected-before-candidate-freeze", String(report, "status"));
        Assert.Equal("development", String(report, "partition"));
        Assert.Equal(
            "sha256:03284022beaa170df5a2e8edbb78ab145a9f0da66934d5ff02debfb316cd083d",
            Digest(json));

        JsonElement inputs = report.GetProperty("inputs");
        AssertArtifactReference(
            root,
            inputs.GetProperty("samplingPlan"),
            "efforthours-public-readiness",
            "0.1.0",
            "calibration/corpora/public-readiness/0.1.0.sampling-plan.json");
        AssertArtifactReference(
            root,
            inputs.GetProperty("corpus"),
            "efforthours-public-readiness-development",
            "0.3.0",
            "calibration/corpora/public-readiness/0.3.0.development-corpus.json");
        AssertArtifactReference(
            root,
            inputs.GetProperty("seedEvaluation"),
            "efforthours-public-readiness-development",
            "0.3.0",
            "calibration/corpora/public-readiness/0.3.0.development-evaluation.json");
        JsonElement[] estimates =
        [.. inputs.GetProperty("developmentEstimates").EnumerateArray()];
        Assert.Equal(15, estimates.Length);
        Assert.Equal(15, estimates.Select(item => String(item, "recordId")).Distinct().Count());
        Assert.All(estimates, item =>
        {
            Assert.StartsWith("record:", String(item, "recordId"), StringComparison.Ordinal);
            Assert.StartsWith("repository:github.com/", String(item, "repositoryId"), StringComparison.Ordinal);
            Assert.Matches("^sha256:[0-9a-f]{64}$", String(item, "sourceDigest"));
            Assert.Matches("^sha256:[0-9a-f]{64}$", String(item, "estimateDigest"));
        });

        JsonElement candidate = report.GetProperty("candidate");
        Assert.Equal("scope-marginality/0.1.0", String(candidate, "id"));
        Assert.Equal("rejected-development-preflight", String(candidate, "status"));
        JsonElement configuration = candidate.GetProperty("configuration");
        Assert.Equal("transparent-rule", String(configuration, "kind"));
        Assert.Equal(
            "ca3636293e92f120ac4d4d0f88a9b633ff96431e",
            String(configuration, "implementationCommit"));
        Assert.Equal(0.25m, configuration.GetProperty("discountFactor").GetDecimal());
        Assert.Equal(0.77m, configuration.GetProperty("range").GetProperty("lowFactor").GetDecimal());
        Assert.Equal(1.26m, configuration.GetProperty("range").GetProperty("highFactor").GetDecimal());

        JsonElement metrics = candidate.GetProperty("developmentMetrics");
        Assert.Equal(0.1963m, Decimal(metrics, "repositoryExpectedWape"));
        Assert.Equal(0.1700m, Decimal(metrics, "relativeWapeImprovement"));
        Assert.Equal(0.0672m, Decimal(metrics, "absoluteAggregateBias"));
        Assert.Equal(0.7333m, Decimal(metrics, "familyOrdinaryErrorPassRate"));
        Assert.Equal(0.8000m, Decimal(metrics, "repositoryExpectedCoverage"));
        Assert.Equal(0.4808m, Decimal(metrics, "meanRepositoryNormalizedWidth"));
        Assert.Equal(0.2611m, Decimal(metrics, "matchedTargetExpectedCoverage"));
        Assert.Equal(0.6691m, Decimal(metrics, "matchedTargetMeanNormalizedWidth"));
        Assert.Equal(1m, Decimal(metrics, "targetMatchRate"));
        Assert.Equal(1m, Decimal(metrics, "sourceReferenceMatchRate"));
        Assert.Equal(1m, Decimal(metrics, "candidateItemMatchRate"));

        JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
        Assert.Equal(28, gates.Length);
        Assert.Equal(13, gates.Count(gate => gate.GetProperty("passed").GetBoolean()));
        AssertGate(gates, "aggregate-bias", "failed");
        AssertGate(gates, "per-family-ordinary-error", "failed");
        AssertGate(gates, "matched-target-expected-coverage", "failed");
        AssertGate(gates, "cross-platform-determinism", "not-evaluated");
        AssertGate(gates, "scanner-thresholds-and-target-fingerprints", "not-evaluated");

        JsonElement holdouts = report.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        Assert.Contains("not-authored", String(holdouts, "validation"), StringComparison.Ordinal);
        Assert.Contains("not-authored", String(holdouts, "test"), StringComparison.Ordinal);
        JsonElement decision = report.GetProperty("decision");
        Assert.Equal("no-candidate-frozen", String(decision, "status"));
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
    }

    private static void AssertGate(JsonElement[] gates, string id, string status)
    {
        JsonElement gate = Assert.Single(gates, item => String(item, "id") == id);
        Assert.Equal(status, String(gate, "status"));
        Assert.False(gate.GetProperty("passed").GetBoolean());
    }

    private static void AssertArtifactReference(
        string root,
        JsonElement reference,
        string id,
        string version,
        string relativePath)
    {
        Assert.Equal(id, String(reference, "id"));
        Assert.Equal(version, String(reference, "version"));
        Assert.Equal(
            Digest(File.ReadAllText(Path.Combine(root, relativePath))),
            String(reference, "digest"));
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
