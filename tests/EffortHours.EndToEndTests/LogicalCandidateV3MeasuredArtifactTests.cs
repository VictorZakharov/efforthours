using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class LogicalCandidateV3MeasuredArtifactTests
{
    private const string RootPath = "calibration/corpora/public-readiness/1.2.0";
    private const string MeasurementDigest =
        "sha256:f3d25627394e8efb3faa3c51399e4da16312c31aa9aa3eeaa2f90d436957cabc";
    private const string PreflightDigest =
        "sha256:01db86e772e6cc2582441d82404845fdc4b73d1e6b5a2e7d77169b3315448b17";
    private const string MutationDigest =
        "sha256:59fb04a4e677795198a2b4c81d5aacf871eef0fdbd67c0bbc577f7992b5f79c0";
    private const string ManifestDigest =
        "sha256:206b3955d53af9902996b588e9255ab9396e7b7624731a6d6e09896ce5026f23";
    private const string MeasurementCommit =
        "40ad39c6dda9029c90aed60a37a2e93c0ecb1ce9";
    private const string RunId = "31892555353";

    private static readonly (string Platform, string Digest)[] PlatformArtifacts =
    [
        ("linux", "sha256:e99d99666b9a3eb6915078b56d3f5451518de7e6a646bc612f8bf459b614b3e3"),
        ("macos", "sha256:46fa9fc697b9d977d4fceeb9e8e31b8c6b85ff1054ff5450b2899f3c026586a3"),
        ("windows", "sha256:3bd4b0d9fd1bd58aa82cfd50a4b6f141da4533f06e5db3db534f870f94ac673b")
    ];

    [Fact]
    public void V3MeasuredCheckpointPassesEveryGateAndFreezesValidationBoundary()
    {
        string measurementJson = Read("1.2.0.measured-operational-report.json", MeasurementDigest);
        string preflightJson = Read("1.2.0.candidate-operational-preflight.json", PreflightDigest);
        string mutationJson = Read("0.8.0.candidate-mutation-report.json", MutationDigest);
        string manifestJson = Read("1.2.0.candidate-manifest.json", ManifestDigest);

        AssertMeasurement(measurementJson);
        AssertAggregatePreflight(preflightJson);
        AssertManifest(manifestJson);

        CalibrationMutationReport mutation =
            ContractJson.Deserialize<CalibrationMutationReport>(mutationJson);
        Assert.Empty(ContractValidation.Validate(mutation));
        Assert.Equal(88, mutation.CaseCount);
        Assert.Equal(339, mutation.AssertionCount);
        Assert.Equal(339, mutation.PassedCount);
        Assert.Equal(0, mutation.FailedCount);
        Assert.True(mutation.AllPassed);
    }

    private static void AssertMeasurement(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement measurement = document.RootElement;
        Assert.Equal(
            "repository-candidate-measured-operational-preflight/1.0.0",
            String(measurement, "measurementVersion"));
        Assert.Equal("repository-model-admission/1.0.0", String(measurement, "policyVersion"));
        Assert.Equal("all-measured-gates-passed", String(measurement, "status"));

        JsonElement[] platforms = [.. measurement.GetProperty("platforms").EnumerateArray()];
        Assert.Equal(3, platforms.Length);
        Assert.Equal(
            ["linux", "macos", "windows"],
            platforms.Select(platform => String(platform.GetProperty("environment"), "platform")));
        Assert.All(platforms, platform =>
        {
            Assert.Equal(MeasurementCommit, String(platform, "measurementImplementationCommit"));
            Assert.Equal(RunId, String(platform, "runId"));
            Assert.Equal(1, platform.GetProperty("runAttempt").GetInt32());
            Assert.Equal(5, platform.GetProperty("freshProcessRunsPerShape").GetInt32());
            JsonElement[] shapes = [.. platform.GetProperty("shapes").EnumerateArray()];
            Assert.Equal(3, shapes.Length);
            Assert.All(shapes, shape =>
            {
                JsonElement summary = shape.GetProperty("summary");
                Assert.True(summary.GetProperty("medianPassed").GetBoolean());
                Assert.True(summary.GetProperty("slowestPassed").GetBoolean());
                Assert.True(summary.GetProperty("peakWorkingSetPassed").GetBoolean());
            });
            Assert.True(platform.GetProperty("scanner").GetProperty("passed").GetBoolean());
        });

        JsonElement crossPlatform = measurement.GetProperty("crossPlatform");
        Assert.True(crossPlatform.GetProperty("evidenceDigestsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("seedOutputsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("candidateOutputsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("repeatedOutputsIdentical").GetBoolean());
        Assert.True(crossPlatform.GetProperty("passed").GetBoolean());

        JsonElement mutations = measurement.GetProperty("mutations");
        Assert.Equal(66, mutations.GetProperty("candidateAppliedCaseCount").GetInt32());
        Assert.Equal(22, mutations.GetProperty("seedFallbackCaseCount").GetInt32());
        Assert.Equal(339, mutations.GetProperty("passedCount").GetInt32());
        Assert.Equal(0, mutations.GetProperty("failedCount").GetInt32());
        Assert.True(mutations.GetProperty("passed").GetBoolean());

        JsonElement package = measurement.GetProperty("package");
        Assert.Equal(0.9871m, package.GetProperty("increaseMib").GetDecimal());
        Assert.True(package.GetProperty("passed").GetBoolean());
        JsonElement[] gates = [.. measurement.GetProperty("gates").EnumerateArray()];
        Assert.Equal(7, gates.Length);
        Assert.All(gates, gate => Assert.Equal("passed", String(gate, "status")));
    }

    private static void AssertAggregatePreflight(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement preflight = document.RootElement;
        Assert.Equal(
            "development-all-operational-gates-passed-candidate-freeze-pending",
            String(preflight, "status"));
        JsonElement[] gates = [.. preflight.GetProperty("candidate").GetProperty("gates")
            .EnumerateArray()];
        Assert.Equal(12, gates.Length);
        Assert.All(gates, gate => Assert.Equal("passed", String(gate, "status")));
        Assert.False(preflight.GetProperty("holdouts")
            .GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(preflight.GetProperty("holdouts").GetProperty("labelsAuthored").GetBoolean());

        JsonElement decision = preflight.GetProperty("decision");
        Assert.False(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.False(decision.GetProperty("validationAuthorized").GetBoolean());
    }

    private static void AssertManifest(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement manifest = document.RootElement;
        Assert.Equal("repository-candidate-manifest/1.0.0", String(manifest, "manifestVersion"));
        Assert.Equal("frozen-before-validation", String(manifest, "status"));
        Assert.Equal(1, manifest.GetProperty("attempt").GetProperty("challengerCount").GetInt32());

        JsonElement challenger = Assert.Single(manifest.GetProperty("challengers").EnumerateArray());
        Assert.Equal("logical-capability/0.3.0", String(challenger, "id"));
        Assert.Equal("transparent-rule", String(challenger, "candidateKind"));
        Assert.Equal("transparent-fitted-table", String(challenger, "implementationKind"));
        Assert.Null(challenger.GetProperty("hyperparameters").GetProperty("trainingSeed").GetString());
        Assert.Equal("seed-rules/0.4.0", String(challenger, "fallbackEstimatorVersion"));

        JsonElement boundary = manifest.GetProperty("developmentBoundary");
        Assert.Equal(15, boundary.GetProperty("recordIds").GetArrayLength());
        Assert.Equal(9, boundary.GetProperty("excludedFamilies")
            .GetProperty("validation").GetArrayLength());
        Assert.Equal(9, boundary.GetProperty("excludedFamilies")
            .GetProperty("test").GetArrayLength());
        Assert.Empty(boundary.GetProperty("contaminatedRecords").EnumerateArray());

        JsonElement measurement = manifest.GetProperty("resourceBudget").GetProperty("measurement");
        Assert.Equal(MeasurementCommit, String(measurement, "measurementImplementationCommit"));
        Assert.Equal(RunId, String(measurement, "runId"));
        Assert.Equal(7, measurement.GetProperty("observed").GetProperty("measuredGatesPassed")
            .GetInt32());
        Assert.Equal(12, measurement.GetProperty("observed")
            .GetProperty("totalOperationalGatesPassed").GetInt32());
        JsonElement[] artifacts = [.. measurement.GetProperty("platformArtifacts").EnumerateArray()];
        Assert.Equal(
            PlatformArtifacts.Select(item => item.Digest),
            artifacts.Select(artifact => String(artifact, "digest")));

        JsonElement rule = manifest.GetProperty("validationSelectionRule");
        Assert.Equal("repository-total expected WAPE", String(rule, "primaryMetric"));
        Assert.Equal(0.01m, rule.GetProperty("absoluteWapeTieTolerance").GetDecimal());
        Assert.Equal(4, rule.GetProperty("tieBreakers").GetArrayLength());

        JsonElement holdouts = manifest.GetProperty("holdouts");
        Assert.False(holdouts.GetProperty("candidateOutputsGenerated").GetBoolean());
        Assert.False(holdouts.GetProperty("labelsAuthored").GetBoolean());
        JsonElement decision = manifest.GetProperty("decision");
        Assert.True(decision.GetProperty("candidateManifestFrozen").GetBoolean());
        Assert.True(decision.GetProperty("validationAuthorized").GetBoolean());
        Assert.False(decision.GetProperty("testAuthorized").GetBoolean());
        Assert.False(decision.GetProperty("candidateAdmitted").GetBoolean());

        foreach ((string platform, string digest) in PlatformArtifacts)
        {
            string platformJson = Read($"platforms/{platform}.json", digest);
            using JsonDocument platformDocument = JsonDocument.Parse(platformJson);
            JsonElement platformRecord = platformDocument.RootElement;
            Assert.Equal(platform, String(platformRecord.GetProperty("environment"), "platform"));
            Assert.Equal(MeasurementCommit, String(platformRecord, "measurementImplementationCommit"));
            Assert.Equal(RunId, String(platformRecord, "runId"));
        }
    }

    private static string Read(string relativePath, string expectedDigest)
    {
        string json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), RootPath, relativePath));
        Assert.Equal(expectedDigest, Digest(json));
        return json;
    }

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
