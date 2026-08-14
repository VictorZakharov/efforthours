using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class CandidateResourceMeasurementTests
{
    [Fact]
    public void ResourceAssessmentUsesTheFrozenAbsoluteAndRelativeLimits()
    {
        CandidateMeasurementRun[] seed =
        [
            Run(1, 100m, 100m),
            Run(2, 110m, 100m),
            Run(3, 120m, 100m),
            Run(4, 130m, 100m),
            Run(5, 140m, 100m),
        ];
        CandidateMeasurementRun[] passingCandidate =
        [
            Run(1, 360m, 164m),
            Run(2, 365m, 164m),
            Run(3, 370m, 164m),
            Run(4, 500m, 164m),
            Run(5, 640m, 164m),
        ];
        CandidateMeasurementRun[] failingCandidate =
        [
            Run(1, 360m, 165m),
            Run(2, 370m, 165m),
            Run(3, 371m, 165m),
            Run(4, 500m, 165m),
            Run(5, 641m, 165m),
        ];

        CandidateShapeSummary passing = CandidateResourceGateAssessment.Build(
            seed,
            passingCandidate);
        CandidateShapeSummary failing = CandidateResourceGateAssessment.Build(
            seed,
            failingCandidate);

        Assert.Equal(120m, passing.SeedMedianMilliseconds);
        Assert.Equal(370m, passing.MedianLimitMilliseconds);
        Assert.Equal(640m, passing.SlowestLimitMilliseconds);
        Assert.Equal(164m, passing.PeakWorkingSetLimitMib);
        Assert.True(passing.MedianPassed);
        Assert.True(passing.SlowestPassed);
        Assert.True(passing.PeakWorkingSetPassed);
        Assert.False(failing.MedianPassed);
        Assert.False(failing.SlowestPassed);
        Assert.False(failing.PeakWorkingSetPassed);
    }

    [Fact]
    public void ResourceAssessmentRequiresFivePairedFreshProcesses()
    {
        CandidateMeasurementRun[] four =
        [
            Run(1, 100m, 50m),
            Run(2, 100m, 50m),
            Run(3, 100m, 50m),
            Run(4, 100m, 50m),
        ];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => CandidateResourceGateAssessment.Build(four, four));

        Assert.Contains("at least five", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasuredGateAssessmentPreservesAMutationRejection()
    {
        CandidateMutationMeasurement mutation = new()
        {
            SuiteId = "efforthours-public-synthetic-mutations",
            SuiteVersion = "0.8.0",
            SuiteDigest = "sha256:suite",
            ReportDigest = "sha256:report",
            CaseCount = 88,
            CandidateAppliedCaseCount = 66,
            SeedFallbackCaseCount = 22,
            AssertionCount = 339,
            PassedCount = 314,
            FailedCount = 25,
            Passed = false,
        };
        CandidateCrossPlatformMeasurement crossPlatform = new()
        {
            PlatformCount = 3,
            ShapeCount = 3,
            EvidenceDigestsIdentical = true,
            SeedOutputsIdentical = true,
            CandidateOutputsIdentical = true,
            LfNormalizedSeedOutputsIdentical = true,
            LfNormalizedCandidateOutputsIdentical = true,
            RepeatedOutputsIdentical = true,
            Passed = true,
        };
        CandidatePackageMeasurement package = new()
        {
            SeedInstalledBytes = 1_000,
            CandidateInstalledBytes = 2_000,
            IncreaseBytes = 1_000,
            IncreaseMib = 0.001m,
            MaximumIncreaseMib = 25m,
            Passed = true,
        };

        IReadOnlyList<CandidatePreflightGate> gates = CandidateMeasuredGateAssessment.Build(
            [],
            crossPlatform,
            mutation,
            package);

        CandidatePreflightGate gate = Assert.Single(
            gates,
            item => item.Id == "public-mutation-suite");
        Assert.Equal("failed", gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("314/339", gate.Observed, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasuredGatesReplaceTheirNotEvaluatedPlaceholders()
    {
        CandidatePreflightGate placeholder = Gate("public-mutation-suite", "not-evaluated");
        CandidatePreflightGate measured = Gate("public-mutation-suite", "failed");
        string[] remainingIds =
        [
            "ecosystem-stratum-agreement", "material-category-agreement",
            "shape-and-size-slice-regression", "cross-platform-determinism",
            "schema-lineage-and-saved-explanation", "offline-safety-ood-and-tamper",
            "median-latency-overhead", "slowest-latency-overhead",
            "peak-working-set-overhead", "installed-package-increase",
            "scanner-thresholds-and-target-fingerprints",
        ];

        IReadOnlyList<CandidatePreflightGate> merged = CandidateMeasurementAggregator.MergeGates(
            [placeholder, .. remainingIds.Select(id => Gate(id, "passed"))],
            [measured]);

        Assert.Equal(12, merged.Count);
        Assert.Same(measured, merged.Single(gate => gate.Id == "public-mutation-suite"));
    }

    private static CandidateMeasurementRun Run(
        int sequence,
        decimal elapsedMilliseconds,
        decimal peakWorkingSetMib) => new()
        {
            Sequence = sequence,
            ElapsedMilliseconds = elapsedMilliseconds,
            PeakWorkingSetMib = peakWorkingSetMib,
            OutputDigest = "sha256:stable-output",
            LfNormalizedOutputDigest = "sha256:stable-output",
        };

    private static CandidatePreflightGate Gate(string id, string status) => new()
    {
        Id = id,
        Status = status,
        Passed = status == "passed",
        Requirement = "test requirement",
        Observed = "test observation",
        Rationale = "test rationale",
    };
}
