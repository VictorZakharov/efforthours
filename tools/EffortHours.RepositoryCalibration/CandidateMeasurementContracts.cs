using System.Text.Json.Serialization;

namespace EffortHours.RepositoryCalibration;

internal sealed record CandidateMeasurementEnvironment
{
    public required string Platform { get; init; }

    public required string OperatingSystem { get; init; }

    public required string Architecture { get; init; }

    public required string Framework { get; init; }

    public required int LogicalProcessors { get; init; }
}

internal sealed record CandidateMeasurementRun
{
    public required int Sequence { get; init; }

    public required decimal ElapsedMilliseconds { get; init; }

    public required decimal PeakWorkingSetMib { get; init; }

    public required string OutputDigest { get; init; }
}

internal sealed record CandidateShapeSummary
{
    public required decimal SeedMedianMilliseconds { get; init; }

    public required decimal CandidateMedianMilliseconds { get; init; }

    public required decimal MedianLimitMilliseconds { get; init; }

    public required bool MedianPassed { get; init; }

    public required decimal SeedSlowestMilliseconds { get; init; }

    public required decimal CandidateSlowestMilliseconds { get; init; }

    public required decimal SlowestLimitMilliseconds { get; init; }

    public required bool SlowestPassed { get; init; }

    public required decimal SeedPeakWorkingSetMib { get; init; }

    public required decimal CandidatePeakWorkingSetMib { get; init; }

    public required decimal PeakWorkingSetLimitMib { get; init; }

    public required bool PeakWorkingSetPassed { get; init; }
}

internal sealed record CandidateShapeMeasurement
{
    public required string Id { get; init; }

    public required int ModuleCopies { get; init; }

    public required int EvidenceFactCount { get; init; }

    public required string EvidenceDigest { get; init; }

    public required int SeedWorkItemCount { get; init; }

    public required int CandidateWorkItemCount { get; init; }

    public IReadOnlyList<CandidateMeasurementRun> SeedRuns { get; init; } = [];

    public IReadOnlyList<CandidateMeasurementRun> CandidateRuns { get; init; } = [];

    public required CandidateShapeSummary Summary { get; init; }
}

internal sealed record CandidateMutationMeasurement
{
    public required string SuiteId { get; init; }

    public required string SuiteVersion { get; init; }

    public required string SuiteDigest { get; init; }

    public required string ReportDigest { get; init; }

    public required int CaseCount { get; init; }

    public required int CandidateAppliedCaseCount { get; init; }

    public required int SeedFallbackCaseCount { get; init; }

    public required int AssertionCount { get; init; }

    public required int PassedCount { get; init; }

    public required int FailedCount { get; init; }

    public required bool Passed { get; init; }
}

internal sealed record CandidateScannerMeasurement
{
    public required string Mode { get; init; }

    public required int RequestedLines { get; init; }

    public required long AnalyzedTextLines { get; init; }

    public required decimal ScanSeconds { get; init; }

    public required decimal PeakWorkingSetMib { get; init; }

    public required string SourceDigest { get; init; }

    public required string TargetMetadataDigest { get; init; }

    public required bool TargetMetadataUnchanged { get; init; }

    public required bool TargetExecutionNotPerformed { get; init; }

    public required bool DependencyInstallationNotPerformed { get; init; }

    public required bool NetworkAccessNotPerformed { get; init; }

    public required string ThresholdStatus { get; init; }

    public required bool Passed { get; init; }
}

internal sealed record CandidatePackageMeasurement
{
    public required long SeedInstalledBytes { get; init; }

    public required long CandidateInstalledBytes { get; init; }

    public required long IncreaseBytes { get; init; }

    public required decimal IncreaseMib { get; init; }

    public required decimal MaximumIncreaseMib { get; init; }

    public required bool Passed { get; init; }
}

internal sealed record CandidatePlatformMeasurement
{
    public required string MeasurementVersion { get; init; }

    public required string PolicyVersion { get; init; }

    public required string CandidateId { get; init; }

    public required string ModelVersion { get; init; }

    public required string ModelDigest { get; init; }

    public required string CandidateImplementationCommit { get; init; }

    public required string MeasurementImplementationCommit { get; init; }

    public required string OperationalPreflightDigest { get; init; }

    public required string RunId { get; init; }

    public required int RunAttempt { get; init; }

    public required CandidateMeasurementEnvironment Environment { get; init; }

    public required int FreshProcessRunsPerShape { get; init; }

    public IReadOnlyList<CandidateShapeMeasurement> Shapes { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CandidateMutationMeasurement? Mutations { get; init; }

    public required CandidateScannerMeasurement Scanner { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CandidatePackageMeasurement? Package { get; init; }
}

internal sealed record CandidateCrossPlatformMeasurement
{
    public required int PlatformCount { get; init; }

    public required int ShapeCount { get; init; }

    public required bool EvidenceDigestsIdentical { get; init; }

    public required bool CandidateOutputsIdentical { get; init; }

    public required bool RepeatedOutputsIdentical { get; init; }

    public required bool Passed { get; init; }
}

internal sealed record CandidateMeasuredOperationalReport
{
    public required string MeasurementVersion { get; init; }

    public required string PolicyVersion { get; init; }

    public required string Status { get; init; }

    public required CandidatePreflightArtifactReference OperationalPreflight { get; init; }

    public required CandidatePreflightArtifactReference CandidateModel { get; init; }

    public required CandidatePreflightArtifactReference MutationSuite { get; init; }

    public required CandidatePreflightArtifactReference MutationReport { get; init; }

    public IReadOnlyList<CandidatePlatformMeasurement> Platforms { get; init; } = [];

    public required CandidateCrossPlatformMeasurement CrossPlatform { get; init; }

    public required CandidateMutationMeasurement Mutations { get; init; }

    public required CandidatePackageMeasurement Package { get; init; }

    public IReadOnlyList<CandidatePreflightGate> Gates { get; init; } = [];

    public IReadOnlyList<string> Limitations { get; init; } = [];
}
