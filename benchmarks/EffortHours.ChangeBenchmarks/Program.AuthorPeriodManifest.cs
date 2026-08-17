using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

public static partial class Program
{
    private static async Task<int> RunAuthorPeriodManifestBenchmarkAsync(
        ChangeBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        using GitPortfolioBenchmarkFixture fixture = await GitPortfolioBenchmarkFixture.CreateAsync(
            options,
            cancellationToken).ConfigureAwait(false);
        RepositoryStateSnapshot worktreeBefore = fixture.CaptureWorktrees();
        RepositoryStateSnapshot gitBefore = fixture.CaptureGitState();

        long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
        long workingSetBefore = Environment.WorkingSet;
        await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
        Stopwatch timer = Stopwatch.StartNew();
        ChangePortfolioBenchmarkExecution execution = await ChangePortfolioBenchmarkRunner.RunAsync(
            fixture,
            cancellationToken).ConfigureAwait(false);
        timer.Stop();
        long peakWorkingSet = await workingSet.StopAsync().ConfigureAwait(false);
        long workingSetAfter = Environment.WorkingSet;
        long allocations = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;

        RepositoryStateSnapshot worktreeAfter = fixture.CaptureWorktrees();
        RepositoryStateSnapshot gitAfter = fixture.CaptureGitState();
        bool worktreeUnchanged = worktreeBefore == worktreeAfter;
        bool gitUnchanged = gitBefore == gitAfter;
        if (!worktreeUnchanged || !gitUnchanged)
        {
            throw new InvalidOperationException(
                "A worktree or Git database changed during manifest analysis; the benchmark " +
                "refuses to report the run as read-only.");
        }

        ValidateAuthorPeriodManifestExecution(options, fixture, execution);
        bool secondsPassed = options.MaximumSeconds is null ||
            (decimal)timer.Elapsed.TotalSeconds <= options.MaximumSeconds.Value;
        decimal peakMib = ToMebibytesValue(peakWorkingSet);
        bool memoryPassed = options.MaximumPeakMib is null ||
            peakMib <= options.MaximumPeakMib.Value;
        WriteAuthorPeriodManifestResults(
            options,
            fixture,
            execution,
            timer.Elapsed,
            allocations,
            workingSetBefore,
            peakWorkingSet,
            workingSetAfter,
            worktreeBefore,
            gitBefore,
            secondsPassed,
            memoryPassed);
        return secondsPassed && memoryPassed ? 0 : 3;
    }

    private static void ValidateAuthorPeriodManifestExecution(
        ChangeBenchmarkOptions options,
        GitPortfolioBenchmarkFixture fixture,
        ChangePortfolioBenchmarkExecution execution)
    {
        bool[] invariants =
        [
            execution.IsolatedManifestReportsEquivalent,
            execution.ManualBaselineEquivalent,
            execution.ReorderedReportBytesEquivalent,
            execution.RepositoryScopedSharedObject,
            execution.FullyOverlappingHeadsPreserved,
            execution.EmptyContributorPreserved,
            execution.PrivacyBoundaryPreserved,
        ];
        if (invariants.Any(value => !value))
        {
            throw new InvalidOperationException(
                "The author-period manifest benchmark violated a deterministic regression invariant.");
        }

        int expectedChanges = options.Commits * fixture.Repositories.Count;
        if (execution.SelectedChanges != expectedChanges ||
            execution.UniqueIsolatedManifestChanges != expectedChanges)
        {
            throw new InvalidOperationException(
                $"Expected {expectedChanges} repository-scoped changes, but observed " +
                $"{execution.SelectedChanges} combined and " +
                $"{execution.UniqueIsolatedManifestChanges} across isolated manifests.");
        }

        if (execution.Statistics.RepositoryCount != fixture.Repositories.Count ||
            execution.Statistics.ObjectDatabaseReaders != fixture.Repositories.Count ||
            execution.IsolatedManifestObjectReaders !=
                fixture.Repositories.Count * execution.IsolatedManifestInvocations ||
            execution.Statistics.UniqueAnalysisArtifactKeys >=
                execution.IsolatedManifestStatistics.UniqueAnalysisArtifactKeys)
        {
            throw new InvalidOperationException(
                "The combined and isolated manifests did not preserve the required repository " +
                "sessions or immutable file-analysis reuse.");
        }
    }

    private static void WriteAuthorPeriodManifestResults(
        ChangeBenchmarkOptions options,
        GitPortfolioBenchmarkFixture fixture,
        ChangePortfolioBenchmarkExecution execution,
        TimeSpan elapsed,
        long allocations,
        long workingSetBefore,
        long peakWorkingSet,
        long workingSetAfter,
        RepositoryStateSnapshot worktree,
        RepositoryStateSnapshot git,
        bool secondsPassed,
        bool memoryPassed)
    {
        ChangePortfolioExecutionStatistics reuse = execution.Statistics;
        ChangePortfolioExecutionStatistics isolatedReuse = execution.IsolatedManifestStatistics;
        Console.WriteLine("benchmark=change/1.7.0");
        Console.WriteLine($"estimator={ChangeEstimator.Version}");
        Console.WriteLine($"mode={options.Name}");
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={RuntimeInformation.OSDescription}");
        Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-files-per-repository={options.Files.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-context-projects-per-repository={options.ContextProjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"active-context-projects-per-repository=" +
            $"{Math.Min(options.ContextProjects, GitPortfolioBenchmarkFixture.MaximumActiveContextProjects).ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-lines-per-file={options.LinesPerFile.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-qualifying-commits-per-repository={options.Commits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-repositories={fixture.Repositories.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"maximum-active-repositories=" +
            $"{reuse.MaximumActiveRepositories.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"maximum-file-analyses-per-repository=" +
            $"{reuse.MaximumConcurrentFileAnalysesPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-heads={fixture.HeadCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-contributors={fixture.ContributorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"selected-changes={execution.SelectedChanges.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-snapshot-analyses={execution.CombinedSnapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-analyses={execution.IsolatedManifestSnapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-invocations={execution.IsolatedManifestInvocations.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-selected-rows={execution.IsolatedManifestSelectedRows.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-unique-changes={execution.UniqueIsolatedManifestChanges.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-git-object-readers={reuse.ObjectDatabaseReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-git-metadata-readers={reuse.ObjectMetadataReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-requests={reuse.ObjectMetadataRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-cache-hits={reuse.ObjectMetadataCacheHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-object-metadata-objects={reuse.UniqueObjectMetadataObjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-cache-evictions={reuse.ObjectMetadataCacheEvictions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-cached-object-metadata-lengths={reuse.PeakCachedObjectMetadataLengthsPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-cache-entry-limit={reuse.ObjectMetadataCacheEntryLimitPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-git-object-readers={execution.IsolatedManifestObjectReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-requests={reuse.SnapshotAnalysisRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-hits={reuse.SnapshotAnalysisHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-snapshot-analysis-keys={reuse.UniqueSnapshotAnalysisKeys.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-revisit-misses={reuse.SnapshotAnalysisRevisitMisses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analysis-artifact-requests={reuse.AnalysisArtifactRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analysis-artifact-hits={reuse.AnalysisArtifactHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-analysis-artifact-keys={reuse.UniqueAnalysisArtifactKeys.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analysis-artifact-revisit-misses={reuse.AnalysisArtifactRevisitMisses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-analysis-artifact-requests={isolatedReuse.AnalysisArtifactRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-analysis-artifact-hits={isolatedReuse.AnalysisArtifactHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-unique-analysis-artifact-keys={isolatedReuse.UniqueAnalysisArtifactKeys.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-analysis-requests={isolatedReuse.SnapshotAnalysisRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-analysis-hits={isolatedReuse.SnapshotAnalysisHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-requests={reuse.SnapshotInventoryRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-hits={reuse.SnapshotInventoryHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-snapshot-inventory-objects={reuse.UniqueSnapshotInventoryObjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-revisit-misses={reuse.SnapshotInventoryRevisitMisses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-inventory-requests={isolatedReuse.SnapshotInventoryRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-inventory-hits={isolatedReuse.SnapshotInventoryHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-evictions={reuse.SnapshotInventoryEvictions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-retained-snapshot-inventories={reuse.PeakRetainedSnapshotInventories.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-retained-snapshot-inventory-roots={reuse.PeakRetainedSnapshotInventoryRoots.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-retention-limit={reuse.SnapshotInventoryRetentionLimit.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-root-retention-limit={reuse.SnapshotInventoryRootRetentionLimit.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"full-snapshot-inventory-loads={reuse.FullSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"incremental-snapshot-inventory-loads={reuse.IncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"batched-incremental-snapshot-inventory-loads={reuse.BatchedIncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-full-snapshot-inventory-loads={isolatedReuse.FullSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-incremental-snapshot-inventory-loads={isolatedReuse.IncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-batched-incremental-snapshot-inventory-loads={isolatedReuse.BatchedIncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-delta-batch-output-limit-mib={ToMebibytes(reuse.SnapshotDeltaBatchOutputByteLimit)}");
        Console.WriteLine($"blob-requests={reuse.BlobRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-cache-hits={reuse.BlobCacheHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-blob-objects={reuse.UniqueBlobObjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-requested-bytes={reuse.BlobRequestedBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-cache-hit-bytes={reuse.BlobCacheHitBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-read-bytes={reuse.BlobReadBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-blob-bytes={reuse.UniqueBlobBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-blob-requests={isolatedReuse.BlobRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-blob-cache-hits={isolatedReuse.BlobCacheHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-cached-blob-mib={ToMebibytes(reuse.PeakCachedBlobBytesPerRepository)}");
        Console.WriteLine(
            "timing-order=initial-combined-warmup,isolated-manifests," +
            "measured-reordered-combined");
        Console.WriteLine(
            $"initial-combined-warmup-seconds=" +
            $"{Duration(execution.InitialCombinedWarmupElapsed)}");
        Console.WriteLine(
            $"initial-combined-warmup-cpu-seconds=" +
            $"{Duration(execution.InitialCombinedWarmupCpu)}");
        Console.WriteLine($"combined-estimate-seconds={Duration(execution.CombinedElapsed)}");
        Console.WriteLine($"combined-estimate-cpu-seconds={Duration(execution.CombinedCpu)}");
        Console.WriteLine($"isolated-manifest-estimate-seconds={Duration(execution.IsolatedManifestElapsed)}");
        Console.WriteLine($"isolated-manifest-estimate-cpu-seconds={Duration(execution.IsolatedManifestCpu)}");
        Console.WriteLine(
            $"combined-to-isolated-manifest-wall-ratio=" +
            $"{(execution.CombinedElapsed.TotalSeconds / execution.IsolatedManifestElapsed.TotalSeconds).ToString("F3", CultureInfo.InvariantCulture)}");
        foreach (ChangePortfolioPhaseTiming timing in execution.CombinedPhaseTimings)
        {
            Console.WriteLine($"combined-phase-{timing.Phase}-seconds={Duration(timing.Elapsed)}");
        }

        Console.WriteLine($"combined-faster-than-isolated-manifests={Lower(execution.CombinedElapsed < execution.IsolatedManifestElapsed)}");
        Console.WriteLine($"fewer-snapshot-analyses-than-isolated-manifests={Lower(execution.CombinedSnapshotAnalyses < execution.IsolatedManifestSnapshotAnalyses)}");
        Console.WriteLine($"fewer-blob-cache-misses-than-isolated-manifests={Lower((reuse.BlobRequests - reuse.BlobCacheHits) < (isolatedReuse.BlobRequests - isolatedReuse.BlobCacheHits))}");
        Console.WriteLine($"fewer-unique-analysis-artifacts-than-isolated-manifests={Lower(reuse.UniqueAnalysisArtifactKeys < isolatedReuse.UniqueAnalysisArtifactKeys)}");
        Console.WriteLine($"isolated-manifest-reports-equivalent={Lower(execution.IsolatedManifestReportsEquivalent)}");
        Console.WriteLine($"manual-baseline-equivalent={Lower(execution.ManualBaselineEquivalent)}");
        Console.WriteLine($"reordered-report-bytes-equivalent={Lower(execution.ReorderedReportBytesEquivalent)}");
        Console.WriteLine($"repository-scoped-shared-object={Lower(execution.RepositoryScopedSharedObject)}");
        Console.WriteLine($"fully-overlapping-heads-preserved={Lower(execution.FullyOverlappingHeadsPreserved)}");
        Console.WriteLine($"empty-contributor-preserved={Lower(execution.EmptyContributorPreserved)}");
        Console.WriteLine($"privacy-boundary-preserved={Lower(execution.PrivacyBoundaryPreserved)}");
        Console.WriteLine($"head-files={fixture.HeadFileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"head-directories={fixture.HeadDirectoryCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"change-seconds={elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"change-allocated-mib={ToMebibytes(allocations)}");
        Console.WriteLine($"change-working-set-start-mib={ToMebibytes(workingSetBefore)}");
        Console.WriteLine($"change-peak-working-set-mib={ToMebibytes(peakWorkingSet)}");
        Console.WriteLine($"change-working-set-end-mib={ToMebibytes(workingSetAfter)}");
        Console.WriteLine($"expected-ehe={execution.ExpectedEffort.ToString("F2", CultureInfo.InvariantCulture)}");
        WriteRepositoryState(worktree, git);
        Console.WriteLine($"seconds-threshold={Threshold(options.MaximumSeconds)}");
        Console.WriteLine($"peak-mib-threshold={Threshold(options.MaximumPeakMib)}");
        Console.WriteLine($"seconds-threshold-passed={Lower(secondsPassed)}");
        Console.WriteLine($"peak-mib-threshold-passed={Lower(memoryPassed)}");
        if (options.KeepRepository)
        {
            Console.WriteLine($"repository-container-path={fixture.RootPath}");
        }
    }

    private static void WriteRepositoryState(
        RepositoryStateSnapshot worktree,
        RepositoryStateSnapshot git)
    {
        Console.WriteLine($"worktree-files={worktree.FileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"worktree-bytes={worktree.TotalBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"worktree-digest={worktree.Digest}");
        Console.WriteLine($"git-state-files={git.FileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"git-state-bytes={git.TotalBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"git-state-digest={git.Digest}");
        Console.WriteLine("worktree-unchanged=true");
        Console.WriteLine("git-state-unchanged=true");
        Console.WriteLine("target-execution=not-performed");
        Console.WriteLine("dependency-installation=not-performed");
        Console.WriteLine("network-access=not-performed");
    }
}
