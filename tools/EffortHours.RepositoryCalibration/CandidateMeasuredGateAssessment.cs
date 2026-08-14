namespace EffortHours.RepositoryCalibration;

internal static class CandidateMeasuredGateAssessment
{
    public static IReadOnlyList<CandidatePreflightGate> Build(
        IReadOnlyList<CandidatePlatformMeasurement> platforms,
        CandidateCrossPlatformMeasurement crossPlatform,
        CandidateMutationMeasurement mutations,
        CandidatePackageMeasurement package)
    {
        PlatformShape[] shapes =
        [
            .. platforms.SelectMany(platform => platform.Shapes.Select(
                shape => new PlatformShape(platform.Environment.Platform, shape))),
        ];
        bool median = shapes.All(item => item.Shape.Summary.MedianPassed);
        bool slowest = shapes.All(item => item.Shape.Summary.SlowestPassed);
        bool peak = shapes.All(item => item.Shape.Summary.PeakWorkingSetPassed);
        bool scanner = platforms.All(platform => platform.Scanner.Passed);
        return
        [
            Gate(
                "public-mutation-suite",
                mutations.Passed,
                "Every applicable versioned public mutation assertion must pass without changing the frozen suite.",
                $"{mutations.PassedCount}/{mutations.AssertionCount} assertions passed across " +
                $"{mutations.CandidateAppliedCaseCount} candidate and {mutations.SeedFallbackCaseCount} fallback cases.",
                mutations.Passed
                    ? "The exact aggregate suite passed; out-of-policy SQL and Python cases retained the named seed fallback."
                    : $"{mutations.FailedCount} public mutation assertion(s) failed."),
            Gate(
                "cross-platform-determinism",
                crossPlatform.Passed,
                "Canonical candidate estimates must be byte-identical in fresh Windows, Linux, and macOS processes.",
                $"{crossPlatform.PlatformCount} platforms x {crossPlatform.ShapeCount} saved-evidence shapes; " +
                $"evidence identical={crossPlatform.EvidenceDigestsIdentical}; seed bytes identical=" +
                $"{crossPlatform.SeedOutputsIdentical}; candidate bytes identical=" +
                $"{crossPlatform.CandidateOutputsIdentical}; LF-normalized candidate bytes identical=" +
                $"{crossPlatform.LfNormalizedCandidateOutputsIdentical}; repeated outputs identical=" +
                $"{crossPlatform.RepeatedOutputsIdentical}.",
                crossPlatform.Passed
                    ? "All repeated and cross-platform canonical projection bytes matched."
                    : crossPlatform.LfNormalizedCandidateOutputsIdentical
                        ? "Raw canonical bytes differed by operating-system line endings; LF-normalized candidate bytes matched, but the frozen byte-identical gate still fails."
                        : "At least one saved input or canonical projection digest differed beyond line-ending normalization."),
            ResourceGate(
                "median-latency-overhead",
                median,
                "Each shape/platform candidate median must be at most max(seed x 1.15, seed + 250 ms).",
                shapes,
                summary => summary.SeedMedianMilliseconds,
                summary => summary.CandidateMedianMilliseconds,
                summary => summary.MedianLimitMilliseconds),
            ResourceGate(
                "slowest-latency-overhead",
                slowest,
                "Each shape/platform candidate slowest run must be at most max(seed x 1.25, seed + 500 ms).",
                shapes,
                summary => summary.SeedSlowestMilliseconds,
                summary => summary.CandidateSlowestMilliseconds,
                summary => summary.SlowestLimitMilliseconds),
            ResourceGate(
                "peak-working-set-overhead",
                peak,
                "Each shape/platform candidate peak must be at most max(seed x 1.15, seed + 64 MiB).",
                shapes,
                summary => summary.SeedPeakWorkingSetMib,
                summary => summary.CandidatePeakWorkingSetMib,
                summary => summary.PeakWorkingSetLimitMib),
            Gate(
                "installed-package-increase",
                package.Passed,
                "The staged installed candidate tool may add at most 25 MiB over the seed tool.",
                $"Seed {package.SeedInstalledBytes} bytes; candidate {package.CandidateInstalledBytes} bytes; " +
                $"increase {package.IncreaseMib} MiB; limit {package.MaximumIncreaseMib} MiB.",
                package.Passed
                    ? "The installed candidate model/runtime overlay remains within the frozen package limit."
                    : "The staged installed candidate layout exceeds the frozen package limit."),
            Gate(
                "scanner-thresholds-and-target-fingerprints",
                scanner,
                "Whole-command scanner checks must retain applicable thresholds and unchanged target fingerprints.",
                string.Join(
                    "; ",
                    platforms.Select(platform =>
                        $"{platform.Environment.Platform}={platform.Scanner.AnalyzedTextLines} lines/" +
                        $"{platform.Scanner.ScanSeconds}s/{platform.Scanner.PeakWorkingSetMib}MiB/" +
                        $"unchanged:{platform.Scanner.TargetMetadataUnchanged}")),
                scanner
                    ? "All three static mixed scans retained target fingerprints and offline/non-executing signals; no frozen cross-platform scanner threshold applies."
                    : "At least one scanner or read-only safety check failed."),
        ];
    }

    private static CandidatePreflightGate ResourceGate(
        string id,
        bool passed,
        string requirement,
        IReadOnlyList<PlatformShape> shapes,
        Func<CandidateShapeSummary, decimal> seed,
        Func<CandidateShapeSummary, decimal> candidate,
        Func<CandidateShapeSummary, decimal> limit)
    {
        string observed = string.Join(
            "; ",
            shapes.Select(item =>
                $"{item.Platform}/{item.Shape.Id}={seed(item.Shape.Summary)}/" +
                $"{candidate(item.Shape.Summary)}/{limit(item.Shape.Summary)}"));
        return Gate(
            id,
            passed,
            requirement,
            $"seed/candidate/limit by platform-shape: {observed}",
            passed
                ? "Every platform-shape pair remains within the frozen overhead boundary."
                : "At least one platform-shape pair exceeds the frozen overhead boundary.");
    }

    private static CandidatePreflightGate Gate(
        string id,
        bool passed,
        string requirement,
        string observed,
        string rationale) => new()
        {
            Id = id,
            Status = passed ? "passed" : "failed",
            Passed = passed,
            Requirement = requirement,
            Observed = observed,
            Rationale = rationale,
        };

    private sealed record PlatformShape(string Platform, CandidateShapeMeasurement Shape);
}
