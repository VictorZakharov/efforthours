using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class CandidateMeasuredOperationalArtifactTests
{
    private const string MeasurementRelativePath =
        "calibration/corpora/public-readiness/0.9.0.measured-operational-report.json";
    private const string PreflightRelativePath =
        "calibration/corpora/public-readiness/0.9.0.candidate-operational-preflight.json";
    private const string MutationRelativePath =
        "calibration/corpora/public-readiness/0.9.0/0.8.0.candidate-mutation-report.json";
    private const string MeasurementDigest =
        "sha256:47c9d916ac8a728a549086f8118d40ed785a797fb975f7570d39e46ae1163275";
    private const string PreflightDigest =
        "sha256:a958aed00f2a0836d813590ec7eb09fd6d88ba6d7159219131c4c977a285e841";
    private const string MutationDigest =
        "sha256:9943f4826ddcdb7536d2759db61e1b1d8d5e55dbb64425020b4e5f3aafe3e383";
    private const string MeasurementImplementationCommit =
        "f783fa2df25054e7969d53805e77e0e5d66b92c5";

    [Fact]
    public void MeasuredCheckpointRetiresTheCandidateWithoutOpeningHoldouts()
    {
        string root = FindRepositoryRoot();
        string measurementJson = Read(root, MeasurementRelativePath);
        string preflightJson = Read(root, PreflightRelativePath);
        string mutationJson = Read(root, MutationRelativePath);
        Assert.Equal(MeasurementDigest, Digest(measurementJson));
        Assert.Equal(PreflightDigest, Digest(preflightJson));
        Assert.Equal(MutationDigest, Digest(mutationJson));

        using JsonDocument measurementDocument = JsonDocument.Parse(measurementJson);
        JsonElement measurement = measurementDocument.RootElement;
        Assert.Equal(
            "repository-candidate-measured-operational-preflight/1.0.0",
            String(measurement, "measurementVersion"));
        Assert.Equal("repository-model-admission/1.0.0", String(measurement, "policyVersion"));
        Assert.Equal("measured-gate-failure", String(measurement, "status"));
        Assert.Equal(
            "sha256:f53b5af09b5adf0d3efed5339e9309156f026b3378c6b69e979467b92524ae93",
            String(measurement.GetProperty("candidateModel"), "digest"));
        Assert.Equal(MutationDigest, String(measurement.GetProperty("mutationReport"), "digest"));

        JsonElement[] platforms = [.. measurement.GetProperty("platforms").EnumerateArray()];
        Assert.Equal(3, platforms.Length);
        Assert.Equal(
            ["linux", "macos", "windows"],
            platforms.Select(item => String(item.GetProperty("environment"), "platform")));
        Assert.All(platforms, platform =>
        {
            Assert.Equal(
                MeasurementImplementationCommit,
                String(platform, "measurementImplementationCommit"));
            Assert.Equal("31758839188", String(platform, "runId"));
            Assert.Equal(1, platform.GetProperty("runAttempt").GetInt32());
            Assert.Equal(5, platform.GetProperty("freshProcessRunsPerShape").GetInt32());
            JsonElement[] shapes = [.. platform.GetProperty("shapes").EnumerateArray()];
            Assert.Equal(3, shapes.Length);
            Assert.All(shapes, shape =>
            {
                Assert.Equal(5, shape.GetProperty("seedRuns").GetArrayLength());
                Assert.Equal(5, shape.GetProperty("candidateRuns").GetArrayLength());
                Assert.True(shape.GetProperty("summary").GetProperty("medianPassed").GetBoolean());
                Assert.True(shape.GetProperty("summary").GetProperty("slowestPassed").GetBoolean());
                Assert.True(shape.GetProperty("summary").GetProperty("peakWorkingSetPassed").GetBoolean());
            });
            Assert.True(platform.GetProperty("scanner").GetProperty("passed").GetBoolean());
        });

        JsonElement crossPlatform = measurement.GetProperty("crossPlatform");
        Assert.True(crossPlatform.GetProperty("evidenceDigestsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("repeatedOutputsIdentical").GetBoolean());
        Assert.False(crossPlatform.GetProperty("seedOutputsIdentical").GetBoolean());
        Assert.False(crossPlatform.GetProperty("candidateOutputsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("lfNormalizedSeedOutputsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("lfNormalizedCandidateOutputsIdentical").GetBoolean());
        Assert.False(crossPlatform.GetProperty("passed").GetBoolean());

        JsonElement mutation = measurement.GetProperty("mutations");
        Assert.Equal(88, mutation.GetProperty("caseCount").GetInt32());
        Assert.Equal(66, mutation.GetProperty("candidateAppliedCaseCount").GetInt32());
        Assert.Equal(22, mutation.GetProperty("seedFallbackCaseCount").GetInt32());
        Assert.Equal(314, mutation.GetProperty("passedCount").GetInt32());
        Assert.Equal(25, mutation.GetProperty("failedCount").GetInt32());
        Assert.False(mutation.GetProperty("passed").GetBoolean());
        JsonElement package = measurement.GetProperty("package");
        Assert.Equal(0.9106m, package.GetProperty("increaseMib").GetDecimal());
        Assert.True(package.GetProperty("passed").GetBoolean());

        JsonElement[] measuredGates = [.. measurement.GetProperty("gates").EnumerateArray()];
        Assert.Equal(7, measuredGates.Length);
        Assert.Equal(5, measuredGates.Count(gate => String(gate, "status") == "passed"));
        AssertGate(measuredGates, "public-mutation-suite", "failed");
        JsonElement determinism = AssertGate(
            measuredGates,
            "cross-platform-determinism",
            "failed");
        Assert.Contains(
            "operating-system line endings",
            String(determinism, "rationale"),
            StringComparison.Ordinal);

        using JsonDocument preflightDocument = JsonDocument.Parse(preflightJson);
        JsonElement preflight = preflightDocument.RootElement;
        Assert.Equal(
            "repository-candidate-operational-preflight/0.2.0",
            String(preflight, "preflightVersion"));
        Assert.Equal("rejected-measured-operational-preflight", String(preflight, "status"));
        Assert.Equal(
            MeasurementDigest,
            String(preflight.GetProperty("inputs").GetProperty("measuredOperationalPreflight"), "digest"));
        JsonElement[] allGates =
        [.. preflight.GetProperty("candidate").GetProperty("gates").EnumerateArray()];
        Assert.Equal(12, allGates.Length);
        Assert.Equal(10, allGates.Count(gate => String(gate, "status") == "passed"));
        Assert.Equal(2, allGates.Count(gate => String(gate, "status") == "failed"));
        Assert.DoesNotContain(allGates, gate => String(gate, "status") == "not-evaluated");
        JsonElement holdouts = preflight.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        JsonElement decision = preflight.GetProperty("decision");
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
        Assert.Contains(
            "Retire logical-capability/0.2.0",
            String(decision, "nextBoundary"),
            StringComparison.Ordinal);

        CalibrationMutationReport mutationReport =
            ContractJson.Deserialize<CalibrationMutationReport>(mutationJson);
        Assert.Empty(ContractValidation.Validate(mutationReport));
        Assert.Equal(339, mutationReport.AssertionCount);
        Assert.Equal(314, mutationReport.PassedCount);
        Assert.Equal(25, mutationReport.FailedCount);
        Assert.False(mutationReport.AllPassed);
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

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath));

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
