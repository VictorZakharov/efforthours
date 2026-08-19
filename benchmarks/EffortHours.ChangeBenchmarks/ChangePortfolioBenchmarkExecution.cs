using EffortHours.Analysis;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

internal sealed record ChangePortfolioBenchmarkExecution
{
    public required int SelectedChanges { get; init; }

    public required int CombinedSnapshotAnalyses { get; init; }

    public required int IsolatedManifestSnapshotAnalyses { get; init; }

    public required int IsolatedManifestInvocations { get; init; }

    public required int IsolatedManifestSelectedRows { get; init; }

    public required int UniqueIsolatedManifestChanges { get; init; }

    public required int IsolatedManifestObjectReaders { get; init; }

    public required TimeSpan CombinedElapsed { get; init; }

    public required TimeSpan CombinedCpu { get; init; }

    public required TimeSpan CombinedAnalysisElapsed { get; init; }

    public required long CombinedCpuWorkAcquisitions { get; init; }

    public required TimeSpan CombinedCpuWorkOccupied { get; init; }

    public required TimeSpan CombinedCpuWorkWait { get; init; }

    public required RepositoryAnalysisWorkStatistics CombinedCommonFileInspection { get; init; }

    public required RepositoryAnalysisWorkStatistics CombinedSemanticFileAnalysis { get; init; }

    public required RepositoryAnalysisWorkStatistics CombinedRepositoryEstimation { get; init; }

    public required int ObservedMaximumActiveCpuWork { get; init; }

    public required TimeSpan InitialCombinedWarmupElapsed { get; init; }

    public required TimeSpan InitialCombinedWarmupCpu { get; init; }

    public required TimeSpan IsolatedManifestElapsed { get; init; }

    public required TimeSpan IsolatedManifestCpu { get; init; }

    public required bool IsolatedManifestReportsEquivalent { get; init; }

    public required bool ManualBaselineEquivalent { get; init; }

    public required bool ReorderedReportBytesEquivalent { get; init; }

    public required bool RepositoryScopedSharedObject { get; init; }

    public required bool FullyOverlappingHeadsPreserved { get; init; }

    public required bool EmptyContributorPreserved { get; init; }

    public required bool PrivacyBoundaryPreserved { get; init; }

    public required decimal ExpectedEffort { get; init; }

    public required string ReportSha256 { get; init; }

    public required string EstimateSemanticsSha256 { get; init; }

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }

    public required ChangePortfolioExecutionStatistics IsolatedManifestStatistics { get; init; }

    public IReadOnlyList<ChangePortfolioPhaseTiming> CombinedPhaseTimings { get; init; } = [];
}
