using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class CandidateMeasurementArtifactTests
{
    private const string ModelRelativePath =
        "calibration/corpora/public-readiness/1.0.0.logical-capability-model.json";
    private const string OperationalRelativePath =
        "calibration/corpora/public-readiness/1.1.0.candidate-operational-preflight.json";
    private const string MutationSuiteRelativePath =
        "calibration/mutations/public-synthetic/0.8.0.suite.json";

    [Fact]
    public async Task SavedEvidenceShapesAndCandidateProjectionAreDeterministic()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"efforthours-candidate-measurement-{Guid.NewGuid():N}");
        try
        {
            string template = Path.Combine(
                root,
                "tests",
                "fixtures",
                "evidence",
                "minimal-dotnet.repository-evidence.json");
            IReadOnlyList<CandidateMeasurementInput> first =
                await CandidateMeasurementInputBuilder.BuildAsync(
                    template,
                    Path.Combine(temporary, "first"),
                    CancellationToken.None);
            IReadOnlyList<CandidateMeasurementInput> second =
                await CandidateMeasurementInputBuilder.BuildAsync(
                    template,
                    Path.Combine(temporary, "second"),
                    CancellationToken.None);

            Assert.Equal(["small", "medium", "large"], first.Select(item => item.Id));
            Assert.Equal([6, 96, 768], first.Select(item => item.FactCount));
            Assert.Equal(first.Select(item => item.Digest), second.Select(item => item.Digest));
            foreach (CandidateMeasurementInput input in first)
            {
                RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(
                    await File.ReadAllTextAsync(input.Path));
                Assert.Empty(ContractValidation.Validate(evidence));
                Assert.DoesNotContain(
                    evidence.Facts.SelectMany(fact => fact.Locations),
                    location => location.Path.Contains('\\'));
            }

            CandidateBenchmarkProjectionOptions options = new()
            {
                EvidencePath = first[0].Path,
                ApplyCandidate = true,
                ModelPath = Path.Combine(root, ModelRelativePath),
                ExpectedModelDigest = CandidateMeasurementRunner.ExpectedModelDigest,
            };
            using StringWriter firstOutput = new();
            using StringWriter secondOutput = new();
            await CandidateBenchmarkProjectionRunner.RunAsync(
                options,
                firstOutput,
                CancellationToken.None);
            await CandidateBenchmarkProjectionRunner.RunAsync(
                options,
                secondOutput,
                CancellationToken.None);

            Assert.Equal(firstOutput.ToString(), secondOutput.ToString());
            Assert.DoesNotContain('\r', firstOutput.ToString());
            Assert.EndsWith("\n", firstOutput.ToString(), StringComparison.Ordinal);
            Assert.False(firstOutput.ToString().EndsWith("\n\n", StringComparison.Ordinal));
            EstimateReport estimate = ContractJson.Deserialize<EstimateReport>(
                firstOutput.ToString());
            Assert.Equal(
                CandidateMeasurementRunner.ExpectedEstimatorVersion,
                estimate.EstimatorVersion);
            Assert.Empty(ContractValidation.Validate(estimate));
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    [Fact]
    public void FrozenMeasurementBoundaryPinsTheCompleteV3PredecessorChain()
    {
        string root = FindRepositoryRoot();
        string modelJson = File.ReadAllText(Path.Combine(root, ModelRelativePath));
        string operationalJson = File.ReadAllText(Path.Combine(root, OperationalRelativePath));
        string suiteJson = File.ReadAllText(Path.Combine(root, MutationSuiteRelativePath));
        LogicalCandidateModel model = ContractJson.Deserialize<LogicalCandidateModel>(modelJson);
        CandidatePreflightReport operational =
            ContractJson.Deserialize<CandidatePreflightReport>(operationalJson);

        Assert.Equal(
            CandidateMeasurementRunner.ExpectedModelDigest,
            JsonArtifactDigest.Compute(modelJson));
        Assert.Equal(
            CandidateMeasurementRunner.ExpectedOperationalDigest,
            JsonArtifactDigest.Compute(operationalJson));
        Assert.Equal(
            CandidateMeasurementRunner.ExpectedMutationSuiteDigest,
            JsonArtifactDigest.Compute(suiteJson));
        Assert.Equal(CandidateMeasurementRunner.ExpectedCandidateId, model.CandidateId);
        Assert.Equal(CandidateMeasurementRunner.ExpectedModelVersion, model.ModelVersion);
        Assert.Equal(CandidateMeasurementRunner.ExpectedEstimatorVersion, model.EstimatorVersion);
        Assert.Equal(
            CandidateMeasurementRunner.ExpectedFeatureContractVersion,
            model.FeatureContractVersion);
        Assert.Equal(
            CandidateMeasurementRunner.ExpectedCandidateImplementationCommit,
            model.Training.ImplementationCommit);
        Assert.Equal(model.CandidateId, operational.Candidate.Id);
        Assert.Equal(
            CandidateMeasurementRunner.ExpectedModelDigest,
            operational.Inputs.CandidateModel?.Digest);
        Assert.Equal(
            CandidateMeasurementRunner.ExpectedNumericalDigest,
            operational.Inputs.NumericalPreflight?.Digest);
        Assert.False(operational.Decision.CandidateManifestFrozen);
        Assert.False(operational.Decision.ValidationAuthorized);
    }

    [Fact]
    public void InstalledPackageMeasurementRequiresAnIsolatedCandidateOverlay()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"efforthours-candidate-package-{Guid.NewGuid():N}");
        string seed = Path.Combine(temporary, "seed");
        string candidate = Path.Combine(temporary, "candidate");
        try
        {
            Directory.CreateDirectory(seed);
            Directory.CreateDirectory(candidate);
            File.WriteAllBytes(Path.Combine(seed, "efforthours.dll"), new byte[100]);
            File.WriteAllBytes(Path.Combine(candidate, "efforthours.dll"), new byte[100]);
            File.WriteAllBytes(
                Path.Combine(candidate, "1.0.0.logical-capability-model.json"),
                new byte[25]);
            File.WriteAllBytes(
                Path.Combine(candidate, "EffortHours.RepositoryCalibration.dll"),
                new byte[50]);

            CandidatePackageMeasurement measurement =
                CandidatePackageMeasurementBuilder.Build(seed, candidate);

            Assert.Equal(75, measurement.IncreaseBytes);
            Assert.True(measurement.Passed);
            File.Delete(Path.Combine(candidate, "EffortHours.RepositoryCalibration.dll"));
            Assert.Throws<InvalidDataException>(
                () => CandidatePackageMeasurementBuilder.Build(seed, candidate));
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    [Fact]
    public void MeasurementOptionsRequireCompleteMutationAndPackageGroups()
    {
        string[] required =
        [
            "--model", "model.json",
            "--operational-preflight", "operational.json",
            "--evidence-template", "evidence.json",
            "--scanner-benchmark", "scanner.dll",
            "--workspace", "work",
            "--platform", OperatingSystem.IsWindows() ? "windows" : "linux",
            "--source-commit", "0123456789abcdef0123456789abcdef01234567",
            "--run-id", "42",
            "--run-attempt", "1",
            "--runs", "5",
            "--output", "report.json",
        ];

        Assert.True(CandidateMeasurementOptions.TryParse(required, out _, out _));
        Assert.False(CandidateMeasurementOptions.TryParse(
            [.. required, "--mutation-suite", "suite.json"],
            out _,
            out string? mutationError));
        Assert.Contains("Mutation options", mutationError, StringComparison.Ordinal);
        Assert.False(CandidateMeasurementOptions.TryParse(
            [.. required, "--seed-install", "seed"],
            out _,
            out string? packageError));
        Assert.Contains("installed-package", packageError, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalOutputDiagnosticNormalizesOperatingSystemLineEndingsOnly()
    {
        const string windows = "{\r\n  \"status\": \"stable\"\r\n}\r\n";

        string normalized = CandidateMeasurementProcess.NormalizeLineEndings(windows);

        Assert.Equal("{\n  \"status\": \"stable\"\n}\n", normalized);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "EffortHours.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
