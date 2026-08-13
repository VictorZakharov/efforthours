using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class LogicalCandidateOperationalArtifactTests
{
    private const string RelativePath =
        "calibration/corpora/public-readiness/0.6.0.candidate-operational-preflight.json";
    private const string Digest =
        "sha256:50dc6d37ad707121d7137c5d031d60aa3eda7f9ac1ddcbeb2f58dbfc3ae3f47c";
    private const string ImplementationCommit =
        "c58f5eb45244aa3f8fd509d4c91c5059f1647440";

    [Fact]
    public void OperationalPreflightRejectsTheExactCandidateWithoutOpeningHoldouts()
    {
        string root = FindRepositoryRoot();
        string json = File.ReadAllText(Path.Combine(root, RelativePath));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement report = document.RootElement;

        Assert.Equal(Digest, ComputeDigest(json));
        Assert.Equal(
            "repository-candidate-operational-preflight/0.1.0",
            String(report, "preflightVersion"));
        Assert.Equal(
            "rejected-development-operational-preflight",
            String(report, "status"));
        JsonElement inputs = report.GetProperty("inputs");
        JsonElement numerical = inputs.GetProperty("numericalPreflight");
        Assert.Equal("logical-capability/0.1.0", String(numerical, "id"));
        Assert.Equal("repository-candidate-preflight/0.2.0", String(numerical, "version"));
        Assert.Equal(
            "sha256:220eb04d95b7da022867afb8cb935d99731a12cbc268afeb4a39fc565aabc64f",
            String(numerical, "digest"));

        JsonElement candidate = report.GetProperty("candidate");
        Assert.Equal("logical-capability/0.1.0", String(candidate, "id"));
        Assert.Equal(
            ImplementationCommit,
            String(candidate.GetProperty("configuration"), "implementationCommit"));
        JsonElement[] gates = [.. candidate.GetProperty("gates").EnumerateArray()];
        Assert.Equal(12, gates.Length);
        Assert.Equal(4, gates.Count(gate => String(gate, "status") == "passed"));
        Assert.Single(gates, gate => String(gate, "status") == "failed");
        Assert.Equal(7, gates.Count(gate => String(gate, "status") == "not-evaluated"));
        AssertGate(gates, "ecosystem-stratum-agreement", "passed");
        AssertGate(gates, "shape-and-size-slice-regression", "passed");
        AssertGate(gates, "schema-lineage-and-saved-explanation", "passed");
        AssertGate(gates, "offline-safety-ood-and-tamper", "passed");
        JsonElement categoryGate = AssertGate(
            gates,
            "material-category-agreement",
            "failed");
        string observed = String(categoryGate, "observed");
        Assert.Contains("pooled-wape=0.2082", observed, StringComparison.Ordinal);
        Assert.Contains("seed=0.4331", observed, StringComparison.Ordinal);
        Assert.Contains("SpecificationComprehensionAndDomainLearning", observed, StringComparison.Ordinal);
        Assert.Contains("bias=-0.2505", observed, StringComparison.Ordinal);
        AssertGate(gates, "public-mutation-suite", "not-evaluated");
        AssertGate(gates, "cross-platform-determinism", "not-evaluated");
        AssertGate(gates, "peak-working-set-overhead", "not-evaluated");

        JsonElement holdouts = report.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        JsonElement decision = report.GetProperty("decision");
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
        Assert.Contains("Retire logical-capability/0.1.0", String(decision, "nextBoundary"));
    }

    private static JsonElement AssertGate(
        JsonElement[] gates,
        string id,
        string status)
    {
        JsonElement gate = Assert.Single(gates, item => String(item, "id") == id);
        Assert.Equal(status, String(gate, "status"));
        Assert.Equal(status == "passed", gate.GetProperty("passed").GetBoolean());
        return gate;
    }

    private static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString()!;

    private static string ComputeDigest(string content)
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
