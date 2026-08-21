using System.Runtime.InteropServices;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioRepositoryOutcome
{
    public required string RepositoryId { get; init; }

    public required string InputDigest { get; init; }

    public required ChangePortfolioRepositoryExecutionStatus Status { get; init; }

    public required ChangePortfolioCheckpointDisposition CheckpointDisposition { get; init; }

    public IReadOnlyList<ChangePortfolioCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public required ChangePortfolioExecutionTelemetry Telemetry { get; init; }

    public ChangePortfolioExecutionStatistics? Statistics { get; init; }

    public GitAuthorPeriodManifestRepositoryScope? Scope { get; init; }

    public long CheckpointReadBytes { get; init; }

    public long CheckpointWrittenBytes { get; init; }

    public TimeSpan Elapsed { get; init; }

    public ChangePortfolioComparisonFailure? Failure { get; init; }
}

internal static class ChangePortfolioComparisonExecutionFactory
{
    public static ChangePortfolioComparisonExecution Create(
        IReadOnlyList<ChangePortfolioRepositoryOutcome> outcomes,
        bool checkpointEnabled,
        ChangePortfolioExecutionTelemetry? portfolioTelemetry = null)
    {
        ChangePortfolioProgress? last = outcomes
            .Select(outcome => outcome.Telemetry.GetLastProgress())
            .Append(portfolioTelemetry?.GetLastProgress())
            .Where(progress => progress is not null)
            .OrderByDescending(progress => progress!.ObservedAt)
            .FirstOrDefault();
        ChangePortfolioExecutionStatistics[] statistics =
            [.. outcomes.Where(outcome => outcome.Statistics is not null)
                .Select(outcome => outcome.Statistics!)];
        long peakWorkingSetBytes = outcomes
            .Select(outcome => outcome.Telemetry.GetLastProgress()?.PeakWorkingSetBytes ?? 0)
            .Append(portfolioTelemetry?.GetLastProgress()?.PeakWorkingSetBytes ?? 0)
            .DefaultIfEmpty()
            .Max();
        ChangePortfolioPhaseTiming[] timings =
        [
            .. outcomes.SelectMany(outcome => outcome.Telemetry.GetTimings())
                .Concat(portfolioTelemetry?.GetTimings() ?? [])
                .GroupBy(timing => timing.Phase, StringComparer.Ordinal)
                .OrderBy(group => PhaseOrder(group.Key))
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new ChangePortfolioPhaseTiming
                {
                    Phase = group.Key,
                    Elapsed = TimeSpan.FromTicks(group.Sum(value => value.Elapsed.Ticks)),
                }),
        ];
        return new ChangePortfolioComparisonExecution
        {
            RuntimeFramework = RuntimeInformation.FrameworkDescription,
            OperatingSystemFamily = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsLinux()
                    ? "linux"
                    : OperatingSystem.IsMacOS() ? "macos" : "other",
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            LogicalProcessorCount = Environment.ProcessorCount,
            ShardPolicy = ChangePortfolioComparisonPolicies.RepositoryEvidenceShardsV1,
            Checkpoint = new ChangePortfolioComparisonCheckpoint
            {
                Enabled = checkpointEnabled,
                HitCount = outcomes.Count(outcome =>
                    outcome.CheckpointDisposition == ChangePortfolioCheckpointDisposition.Hit),
                MissCount = outcomes.Count(outcome => outcome.CheckpointDisposition is
                    ChangePortfolioCheckpointDisposition.MissWritten or
                    ChangePortfolioCheckpointDisposition.MissFailed),
                WriteCount = outcomes.Count(outcome =>
                    outcome.CheckpointDisposition == ChangePortfolioCheckpointDisposition.MissWritten),
                FailureCount = outcomes.Count(outcome =>
                    outcome.CheckpointDisposition == ChangePortfolioCheckpointDisposition.MissFailed),
                ReadBytes = outcomes.Sum(outcome => outcome.CheckpointReadBytes),
                WrittenBytes = outcomes.Sum(outcome => outcome.CheckpointWrittenBytes),
            },
            RepositoryShardCount = outcomes.Count,
            Repositories = [.. outcomes
                .OrderBy(outcome => outcome.RepositoryId, StringComparer.Ordinal)
                .Select(RepositoryExecution)],
            PhaseTimings = [.. timings.Select(timing => new ChangePortfolioComparisonPhaseTiming
            {
                Phase = timing.Phase,
                ElapsedMilliseconds = Round((decimal)timing.Elapsed.TotalMilliseconds),
            })],
            LastProgress = Progress(last),
            Reuse = new ChangePortfolioComparisonReuse
            {
                SnapshotAnalysisRequests = statistics.Sum(value => value.SnapshotAnalysisRequests),
                SnapshotAnalysisHits = statistics.Sum(value => value.SnapshotAnalysisHits),
                UniqueSnapshotAnalysisKeys = statistics.Sum(value => value.UniqueSnapshotAnalysisKeys),
                AnalysisArtifactRequests = statistics.Sum(value => value.AnalysisArtifactRequests),
                AnalysisArtifactHits = statistics.Sum(value => value.AnalysisArtifactHits),
                UniqueAnalysisArtifactKeys = statistics.Sum(value => value.UniqueAnalysisArtifactKeys),
                SnapshotInventoryRequests = statistics.Sum(value => value.SnapshotInventoryRequests),
                SnapshotInventoryHits = statistics.Sum(value => value.SnapshotInventoryHits),
                UniqueSnapshotInventoryObjects = statistics.Sum(value => value.UniqueSnapshotInventoryObjects),
                BlobRequests = statistics.Sum(value => value.BlobRequests),
                BlobCacheHits = statistics.Sum(value => value.BlobCacheHits),
                UniqueBlobObjects = statistics.Sum(value => value.UniqueBlobObjects),
                BlobReadBytes = statistics.Sum(value => value.BlobReadBytes),
                PeakWorkingSetBytes = peakWorkingSetBytes,
            },
            Resources = new ChangePortfolioComparisonResourceUsage
            {
                CandidateLedgerChargePolicy =
                    ChangePortfolioPreflightPolicies.CandidateLedgerChargeV1,
                SelectionScopeComplete = outcomes.All(outcome => outcome.Scope is not null),
                CandidateCount = outcomes.Sum(outcome => (long)(outcome.Scope?.CandidateCount ?? 0)),
                ChargedCandidateLedgerBytes = outcomes.Sum(outcome =>
                    outcome.Scope?.ChargedCandidateLedgerBytes ?? 0),
                SelectionChunkCount = outcomes.Sum(outcome =>
                    outcome.Scope?.SelectionChunkCount ?? 0),
                SelectionChunkSize = ChangeAuthorPeriodManifestLimits.SelectionChunkSize,
                SelectedChangeCount = outcomes.Sum(outcome =>
                    (long)(outcome.Scope?.SelectedChangeCount ?? outcome.Candidates.Count)),
                ProjectedSnapshotRequests = outcomes.Sum(outcome =>
                    outcome.Scope?.ProjectedSnapshotRequests ?? 0),
                AnalysisChunkCount = outcomes.Sum(outcome => outcome.Scope?.AnalysisChunkCount ?? 0),
                AnalysisChunkSize = ChangeEstimator.PortfolioDeltaPrimeChunkSize,
                SnapshotAnalysisRequests = statistics.Sum(value => value.SnapshotAnalysisRequests),
                PeakWorkingSetBytes = peakWorkingSetBytes,
                MaximumCandidateLedgerBytesPerRepository =
                    ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository,
                MaximumCheckpointBytesPerRepository =
                    ChangePortfolioLimits.MaximumCheckpointBytesPerRepository,
                MaximumConcurrentRepositories =
                    ChangeEstimator.MaximumConcurrentPortfolioRepositories,
                MaximumBufferedChangesPerRepository =
                    ChangeEstimator.MaximumConcurrentPortfolioChangesPerRepository,
                MaximumConcurrentCpuWorkItems =
                    RepositoryAnalysisConcurrency.MaximumCpuWorkItems,
                MaximumConcurrentGitTreeReads =
                    RepositoryAnalysisConcurrency.MaximumGitTreeReads,
                MaximumPendingFileInspections =
                    RepositoryAnalysisConcurrency.MaximumPendingFileInspections,
                MaximumBufferedFileBytes =
                    RepositoryAnalysisConcurrency.MaximumBufferedFileBytes,
                MaximumRenderedOutputBytes = ChangePortfolioLimits.MaximumRenderedOutputBytes,
            },
            Failures = [.. outcomes.Where(outcome => outcome.Failure is not null)
                .Select(outcome => outcome.Failure!)],
        };
    }

    private static ChangePortfolioComparisonRepositoryExecution RepositoryExecution(
        ChangePortfolioRepositoryOutcome outcome) => new()
        {
            RepositoryId = outcome.RepositoryId,
            Status = outcome.Status,
            CheckpointDisposition = outcome.CheckpointDisposition,
            SelectedChangeCount = outcome.Scope?.SelectedChangeCount ?? outcome.Candidates.Count,
            CandidateCount = outcome.Scope?.CandidateCount ?? 0,
            ChargedCandidateLedgerBytes = outcome.Scope?.ChargedCandidateLedgerBytes ?? 0,
            SelectionChunkCount = outcome.Scope?.SelectionChunkCount ?? 0,
            AnalysisChunkCount = outcome.Scope?.AnalysisChunkCount ?? 0,
            ProjectedSnapshotRequests = outcome.Scope?.ProjectedSnapshotRequests ?? 0,
            CheckpointReadBytes = outcome.CheckpointReadBytes,
            CheckpointWrittenBytes = outcome.CheckpointWrittenBytes,
            ElapsedMilliseconds = Round((decimal)outcome.Elapsed.TotalMilliseconds),
            InputDigest = outcome.InputDigest,
            PhaseTimings = [.. outcome.Telemetry.GetTimings().Select(timing =>
                new ChangePortfolioComparisonPhaseTiming
                {
                    Phase = timing.Phase,
                    ElapsedMilliseconds = Round((decimal)timing.Elapsed.TotalMilliseconds),
                })],
            LastProgress = Progress(outcome.Telemetry.GetLastProgress()),
        };

    private static ChangePortfolioComparisonProgress? Progress(
        ChangePortfolioProgress? progress) => progress is null ? null : new ChangePortfolioComparisonProgress
        {
            ObservedAt = progress.ObservedAt.ToUniversalTime(),
            Phase = progress.Phase,
            ProcessedUnits = progress.ProcessedUnits,
            TotalUnits = progress.TotalUnits,
            AnalysisCacheRequests = progress.AnalysisCacheRequests,
            AnalysisCacheHits = progress.AnalysisCacheHits,
            ElapsedMilliseconds = Round((decimal)progress.Elapsed.TotalMilliseconds),
            WorkingSetBytes = progress.WorkingSetBytes,
            PeakWorkingSetBytes = progress.PeakWorkingSetBytes,
        };

    private static int PhaseOrder(string phase) => phase switch
    {
        ChangePortfolioExecutionPhases.ManifestValidation => 0,
        ChangePortfolioExecutionPhases.HeadValidation => 1,
        ChangePortfolioExecutionPhases.HistoryUnion => 2,
        ChangePortfolioExecutionPhases.Selection => 3,
        ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction => 4,
        ChangePortfolioExecutionPhases.StaticAnalysis => 5,
        ChangePortfolioExecutionPhases.Reconciliation => 6,
        ChangePortfolioExecutionPhases.Allocation => 7,
        ChangePortfolioExecutionPhases.Rendering => 8,
        _ => int.MaxValue,
    };

    private static decimal Round(decimal value) =>
        decimal.Round(value, 3, MidpointRounding.AwayFromZero);
}
