using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using EffortHours.Analysis;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

public static partial class Program
{
    private static async Task<int> RunAuthorPeriodManifestBenchmarkAsync(
        ChangeBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        Stopwatch preparationTimer = Stopwatch.StartNew();
        using GitPortfolioBenchmarkFixture fixture = options.PreparedFixturePath is null
            ? await GitPortfolioBenchmarkFixture.CreateAsync(options, cancellationToken)
                .ConfigureAwait(false)
            : await GitPortfolioBenchmarkFixture.LoadAsync(
                options.PreparedFixturePath,
                cancellationToken).ConfigureAwait(false);
        preparationTimer.Stop();
        if (options.PrepareOnly)
        {
            WritePreparedFixtureResults(fixture, preparationTimer.Elapsed);
            return 0;
        }

        (RepositoryStateSnapshot worktreeBefore, RepositoryStateSnapshot gitBefore) =
            options.CombinedOnly
                ? await fixture.CaptureLightweightStateAsync(cancellationToken)
                    .ConfigureAwait(false)
                : (fixture.CaptureWorktrees(), fixture.CaptureGitState());

        long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
        TimeSpan pauseBefore = GC.GetTotalPauseDuration();
        int generationZeroBefore = GC.CollectionCount(0);
        int generationOneBefore = GC.CollectionCount(1);
        int generationTwoBefore = GC.CollectionCount(2);
        long workingSetBefore = Environment.WorkingSet;
        await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
        Stopwatch timer = Stopwatch.StartNew();
        ChangePortfolioBenchmarkExecution execution = options.CombinedOnly
            ? await ChangePortfolioBenchmarkRunner.RunCombinedOnlyAsync(
                fixture,
                cancellationToken).ConfigureAwait(false)
            : await ChangePortfolioBenchmarkRunner.RunAsync(
                fixture,
                cancellationToken).ConfigureAwait(false);
        timer.Stop();
        long peakWorkingSet = await workingSet.StopAsync().ConfigureAwait(false);
        long workingSetAfter = Environment.WorkingSet;
        long allocations = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;
        TimeSpan pauseDuration = GC.GetTotalPauseDuration() - pauseBefore;
        int generationZeroCollections = GC.CollectionCount(0) - generationZeroBefore;
        int generationOneCollections = GC.CollectionCount(1) - generationOneBefore;
        int generationTwoCollections = GC.CollectionCount(2) - generationTwoBefore;

        (RepositoryStateSnapshot worktreeAfter, RepositoryStateSnapshot gitAfter) =
            options.CombinedOnly
                ? await fixture.CaptureLightweightStateAsync(cancellationToken)
                    .ConfigureAwait(false)
                : (fixture.CaptureWorktrees(), fixture.CaptureGitState());
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
            pauseDuration,
            generationZeroCollections,
            generationOneCollections,
            generationTwoCollections,
            workingSetBefore,
            peakWorkingSet,
            workingSetAfter,
            worktreeBefore,
            gitBefore,
            preparationTimer.Elapsed,
            secondsPassed,
            memoryPassed);
        return secondsPassed && memoryPassed ? 0 : 3;
    }

    private static void ValidateAuthorPeriodManifestExecution(
        ChangeBenchmarkOptions options,
        GitPortfolioBenchmarkFixture fixture,
        ChangePortfolioBenchmarkExecution execution)
    {
        bool[] invariants = options.CombinedOnly
            ?
            [
                execution.RepositoryScopedSharedObject,
                execution.FullyOverlappingHeadsPreserved,
                execution.EmptyContributorPreserved,
                execution.PrivacyBoundaryPreserved,
            ]
            :
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

        int expectedChanges = fixture.QualifyingCommitsPerRepository * fixture.Repositories.Count;
        if (execution.SelectedChanges != expectedChanges ||
            (!options.CombinedOnly &&
                execution.UniqueIsolatedManifestChanges != expectedChanges))
        {
            throw new InvalidOperationException(
                $"Expected {expectedChanges} repository-scoped changes, but observed " +
                $"{execution.SelectedChanges} combined and " +
                $"{execution.UniqueIsolatedManifestChanges} across isolated manifests.");
        }

        if (execution.Statistics.RepositoryCount != fixture.Repositories.Count ||
            execution.Statistics.ObjectDatabaseReaders != fixture.Repositories.Count ||
            (!options.CombinedOnly &&
                (execution.IsolatedManifestObjectReaders !=
                    fixture.Repositories.Count * execution.IsolatedManifestInvocations ||
                execution.Statistics.UniqueAnalysisArtifactKeys >=
                    execution.IsolatedManifestStatistics.UniqueAnalysisArtifactKeys)))
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
        TimeSpan pauseDuration,
        int generationZeroCollections,
        int generationOneCollections,
        int generationTwoCollections,
        long workingSetBefore,
        long peakWorkingSet,
        long workingSetAfter,
        RepositoryStateSnapshot worktree,
        RepositoryStateSnapshot git,
        TimeSpan preparationElapsed,
        bool secondsPassed,
        bool memoryPassed)
    {
        ChangePortfolioExecutionStatistics reuse = execution.Statistics;
        ChangePortfolioExecutionStatistics isolatedReuse = execution.IsolatedManifestStatistics;
        Console.WriteLine("benchmark=change/1.9.0");
        Console.WriteLine($"estimator={ChangeEstimator.Version}");
        Console.WriteLine($"mode={options.Name}");
        Console.WriteLine(
            $"benchmark-run={(options.CombinedOnly ? "combined-only" : "comparison-suite")}");
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={RuntimeInformation.OSDescription}");
        Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        ThreadPool.GetMinThreads(out int minimumWorkerThreads, out _);
        Console.WriteLine($"minimum-worker-threads={minimumWorkerThreads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"server-gc={System.Runtime.GCSettings.IsServerGC.ToString().ToLowerInvariant()}");
        Console.WriteLine($"gc-pause-seconds={Duration(pauseDuration)}");
        Console.WriteLine($"gc-generation-0-collections={generationZeroCollections.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"gc-generation-1-collections={generationOneCollections.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"gc-generation-2-collections={generationTwoCollections.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"fixture-source={(fixture.IsPreparedFixture ? "prepared" : "generated")}");
        Console.WriteLine($"fixture-preparation-seconds={Duration(preparationElapsed)}");
        Console.WriteLine($"requested-files-per-repository={fixture.FilesPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-context-projects-per-repository={fixture.ContextProjectsPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"active-context-projects-per-repository=" +
            $"{Math.Min(fixture.ContextProjectsPerRepository, GitPortfolioBenchmarkFixture.MaximumActiveContextProjects).ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-lines-per-file={fixture.LinesPerFile.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-qualifying-commits-per-repository={fixture.QualifyingCommitsPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-repositories={fixture.Repositories.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"maximum-active-repositories=" +
            $"{reuse.MaximumActiveRepositories.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"maximum-process-cpu-work-items=" +
            $"{reuse.MaximumConcurrentCpuWorkItems.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-heads={fixture.HeadCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-contributors={fixture.ContributorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"selected-changes={execution.SelectedChanges.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-snapshot-analyses={execution.CombinedSnapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-analyses={Compared(options, execution.IsolatedManifestSnapshotAnalyses)}");
        Console.WriteLine($"isolated-manifest-invocations={Compared(options, execution.IsolatedManifestInvocations)}");
        Console.WriteLine($"isolated-manifest-selected-rows={Compared(options, execution.IsolatedManifestSelectedRows)}");
        Console.WriteLine($"isolated-manifest-unique-changes={Compared(options, execution.UniqueIsolatedManifestChanges)}");
        Console.WriteLine($"combined-git-object-readers={reuse.ObjectDatabaseReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-git-metadata-readers={reuse.ObjectMetadataReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-requests={reuse.ObjectMetadataRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-cache-hits={reuse.ObjectMetadataCacheHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-object-metadata-objects={reuse.UniqueObjectMetadataObjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-cache-evictions={reuse.ObjectMetadataCacheEvictions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-cached-object-metadata-lengths={reuse.PeakCachedObjectMetadataLengthsPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"object-metadata-cache-entry-limit={reuse.ObjectMetadataCacheEntryLimitPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-git-object-readers={Compared(options, execution.IsolatedManifestObjectReaders)}");
        Console.WriteLine($"snapshot-analysis-requests={reuse.SnapshotAnalysisRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-hits={reuse.SnapshotAnalysisHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-snapshot-analysis-keys={reuse.UniqueSnapshotAnalysisKeys.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-revisit-misses={reuse.SnapshotAnalysisRevisitMisses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analysis-artifact-requests={reuse.AnalysisArtifactRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analysis-artifact-hits={reuse.AnalysisArtifactHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-analysis-artifact-keys={reuse.UniqueAnalysisArtifactKeys.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"analysis-artifact-revisit-misses={reuse.AnalysisArtifactRevisitMisses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-analysis-artifact-requests={Compared(options, isolatedReuse.AnalysisArtifactRequests)}");
        Console.WriteLine($"isolated-manifest-analysis-artifact-hits={Compared(options, isolatedReuse.AnalysisArtifactHits)}");
        Console.WriteLine($"isolated-manifest-unique-analysis-artifact-keys={Compared(options, isolatedReuse.UniqueAnalysisArtifactKeys)}");
        Console.WriteLine($"isolated-manifest-snapshot-analysis-requests={Compared(options, isolatedReuse.SnapshotAnalysisRequests)}");
        Console.WriteLine($"isolated-manifest-snapshot-analysis-hits={Compared(options, isolatedReuse.SnapshotAnalysisHits)}");
        Console.WriteLine($"snapshot-inventory-requests={reuse.SnapshotInventoryRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-hits={reuse.SnapshotInventoryHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-snapshot-inventory-objects={reuse.UniqueSnapshotInventoryObjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-revisit-misses={reuse.SnapshotInventoryRevisitMisses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-snapshot-inventory-requests={Compared(options, isolatedReuse.SnapshotInventoryRequests)}");
        Console.WriteLine($"isolated-manifest-snapshot-inventory-hits={Compared(options, isolatedReuse.SnapshotInventoryHits)}");
        Console.WriteLine($"snapshot-inventory-evictions={reuse.SnapshotInventoryEvictions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-retained-snapshot-inventories={reuse.PeakRetainedSnapshotInventories.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-retained-snapshot-inventory-roots={reuse.PeakRetainedSnapshotInventoryRoots.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-retention-limit={reuse.SnapshotInventoryRetentionLimit.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-root-retention-limit={reuse.SnapshotInventoryRootRetentionLimit.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"full-snapshot-inventory-loads={reuse.FullSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"incremental-snapshot-inventory-loads={reuse.IncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"batched-incremental-snapshot-inventory-loads={reuse.BatchedIncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-full-snapshot-inventory-loads={Compared(options, isolatedReuse.FullSnapshotInventoryLoads)}");
        Console.WriteLine($"isolated-manifest-incremental-snapshot-inventory-loads={Compared(options, isolatedReuse.IncrementalSnapshotInventoryLoads)}");
        Console.WriteLine($"isolated-manifest-batched-incremental-snapshot-inventory-loads={Compared(options, isolatedReuse.BatchedIncrementalSnapshotInventoryLoads)}");
        Console.WriteLine($"snapshot-delta-batch-output-limit-mib={ToMebibytes(reuse.SnapshotDeltaBatchOutputByteLimit)}");
        Console.WriteLine(
            $"full-snapshot-inventory-read-seconds=" +
            $"{Duration(reuse.FullSnapshotInventoryReadTime)}");
        Console.WriteLine(
            $"full-snapshot-inventory-projection-seconds=" +
            $"{Duration(reuse.FullSnapshotInventoryProjectionTime)}");
        Console.WriteLine(
            $"incremental-snapshot-inventory-projection-seconds=" +
            $"{Duration(reuse.IncrementalSnapshotInventoryProjectionTime)}");
        Console.WriteLine($"blob-requests={reuse.BlobRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-cache-hits={reuse.BlobCacheHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-blob-objects={reuse.UniqueBlobObjects.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-requested-bytes={reuse.BlobRequestedBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-cache-hit-bytes={reuse.BlobCacheHitBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-read-bytes={reuse.BlobReadBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"unique-blob-bytes={reuse.UniqueBlobBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-blob-requests={Compared(options, isolatedReuse.BlobRequests)}");
        Console.WriteLine($"isolated-manifest-blob-cache-hits={Compared(options, isolatedReuse.BlobCacheHits)}");
        Console.WriteLine($"peak-cached-blob-mib={ToMebibytes(reuse.PeakCachedBlobBytesPerRepository)}");
        Console.WriteLine($"git-object-reader-cpu-seconds={Duration(reuse.ObjectReaderCpuTime)}");
        Console.WriteLine(
            $"git-object-reader-occupied-seconds=" +
            $"{Duration(reuse.ObjectReaderOccupiedTime)}");
        Console.WriteLine(
            $"git-object-reader-wait-seconds={Duration(reuse.ObjectReaderWaitTime)}");
        Console.WriteLine(
            $"timing-order={(options.CombinedOnly ? "single-combined" : "initial-combined-warmup,isolated-manifests,measured-reordered-combined")}");
        Console.WriteLine(
            $"initial-combined-warmup-seconds=" +
            $"{Compared(options, Duration(execution.InitialCombinedWarmupElapsed))}");
        Console.WriteLine(
            $"initial-combined-warmup-cpu-seconds=" +
            $"{Compared(options, Duration(execution.InitialCombinedWarmupCpu))}");
        Console.WriteLine($"combined-estimate-seconds={Duration(execution.CombinedElapsed)}");
        Console.WriteLine(
            $"combined-analysis-seconds={Duration(execution.CombinedAnalysisElapsed)}");
        Console.WriteLine($"combined-estimate-cpu-seconds={Duration(execution.CombinedCpu)}");
        Console.WriteLine(
            $"combined-cpu-work-acquisitions=" +
            $"{execution.CombinedCpuWorkAcquisitions.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"combined-cpu-work-occupied-seconds=" +
            $"{Duration(execution.CombinedCpuWorkOccupied)}");
        Console.WriteLine(
            $"combined-cpu-work-wait-seconds=" +
            $"{Duration(execution.CombinedCpuWorkWait)}");
        WriteCpuWork("common-file-inspection", execution.CombinedCommonFileInspection);
        WriteCpuWork("semantic-file-analysis", execution.CombinedSemanticFileAnalysis);
        WriteCpuWork("repository-estimation", execution.CombinedRepositoryEstimation);
        Console.WriteLine(
            $"observed-maximum-active-cpu-work=" +
            $"{execution.ObservedMaximumActiveCpuWork.ToString(CultureInfo.InvariantCulture)}");
        double averageActiveCpuWorkers = execution.CombinedAnalysisElapsed <= TimeSpan.Zero
            ? 0
            : execution.CombinedCpuWorkOccupied.TotalSeconds /
                execution.CombinedAnalysisElapsed.TotalSeconds;
        Console.WriteLine(
            $"combined-average-active-cpu-work=" +
            $"{averageActiveCpuWorkers.ToString("F3", CultureInfo.InvariantCulture)}");
        double managedProcessorEquivalents = execution.CombinedElapsed <= TimeSpan.Zero
            ? 0
            : execution.CombinedCpu.TotalSeconds / execution.CombinedElapsed.TotalSeconds;
        double managedLogicalProcessorUtilization = Environment.ProcessorCount == 0
            ? 0
            : managedProcessorEquivalents / Environment.ProcessorCount * 100;
        Console.WriteLine(
            $"combined-managed-average-processor-equivalents=" +
            $"{managedProcessorEquivalents.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"combined-managed-logical-processor-utilization-percent=" +
            $"{managedLogicalProcessorUtilization.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"isolated-manifest-estimate-seconds={Compared(options, Duration(execution.IsolatedManifestElapsed))}");
        Console.WriteLine($"isolated-manifest-estimate-cpu-seconds={Compared(options, Duration(execution.IsolatedManifestCpu))}");
        Console.WriteLine(
            $"combined-to-isolated-manifest-wall-ratio=" +
            $"{Compared(options, options.CombinedOnly ? string.Empty : (execution.CombinedElapsed.TotalSeconds / execution.IsolatedManifestElapsed.TotalSeconds).ToString("F3", CultureInfo.InvariantCulture))}");
        foreach (ChangePortfolioPhaseTiming timing in execution.CombinedPhaseTimings)
        {
            Console.WriteLine($"combined-phase-{timing.Phase}-seconds={Duration(timing.Elapsed)}");
        }

        Console.WriteLine($"combined-faster-than-isolated-manifests={Compared(options, Lower(execution.CombinedElapsed < execution.IsolatedManifestElapsed))}");
        Console.WriteLine($"fewer-snapshot-analyses-than-isolated-manifests={Compared(options, Lower(execution.CombinedSnapshotAnalyses < execution.IsolatedManifestSnapshotAnalyses))}");
        Console.WriteLine($"fewer-blob-cache-misses-than-isolated-manifests={Compared(options, Lower((reuse.BlobRequests - reuse.BlobCacheHits) < (isolatedReuse.BlobRequests - isolatedReuse.BlobCacheHits)))}");
        Console.WriteLine($"fewer-unique-analysis-artifacts-than-isolated-manifests={Compared(options, Lower(reuse.UniqueAnalysisArtifactKeys < isolatedReuse.UniqueAnalysisArtifactKeys))}");
        Console.WriteLine($"isolated-manifest-reports-equivalent={Compared(options, Lower(execution.IsolatedManifestReportsEquivalent))}");
        Console.WriteLine($"manual-baseline-equivalent={Compared(options, Lower(execution.ManualBaselineEquivalent))}");
        Console.WriteLine($"reordered-report-bytes-equivalent={Compared(options, Lower(execution.ReorderedReportBytesEquivalent))}");
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
        Console.WriteLine($"report-sha256={execution.ReportSha256}");
        Console.WriteLine($"estimate-semantics-sha256={execution.EstimateSemanticsSha256}");
        WriteRepositoryState(worktree, git, options.CombinedOnly);
        Console.WriteLine($"seconds-threshold={Threshold(options.MaximumSeconds)}");
        Console.WriteLine($"peak-mib-threshold={Threshold(options.MaximumPeakMib)}");
        Console.WriteLine($"seconds-threshold-passed={Lower(secondsPassed)}");
        Console.WriteLine($"peak-mib-threshold-passed={Lower(memoryPassed)}");
        if (options.KeepRepository)
        {
            Console.WriteLine($"repository-container-path={fixture.RootPath}");
            Console.WriteLine($"fixture-descriptor-path={fixture.DescriptorPath}");
        }
    }

    private static void WriteCpuWork(
        string name,
        RepositoryAnalysisWorkStatistics statistics)
    {
        Console.WriteLine(
            $"combined-{name}-acquisitions=" +
            statistics.Acquisitions.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine(
            $"combined-{name}-occupied-seconds={Duration(statistics.OccupiedTime)}");
        Console.WriteLine($"combined-{name}-wait-seconds={Duration(statistics.WaitTime)}");
    }

    private static void WritePreparedFixtureResults(
        GitPortfolioBenchmarkFixture fixture,
        TimeSpan preparationElapsed)
    {
        Console.WriteLine("benchmark=change-fixture/1.0.0");
        Console.WriteLine("mode=author-period-manifest-fixture-preparation");
        Console.WriteLine($"fixture-preparation-seconds={Duration(preparationElapsed)}");
        Console.WriteLine($"files-per-repository={fixture.FilesPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"context-projects-per-repository={fixture.ContextProjectsPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"lines-per-file={fixture.LinesPerFile.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"qualifying-commits-per-repository={fixture.QualifyingCommitsPerRepository.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-repositories={fixture.Repositories.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-heads={fixture.HeadCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"repository-container-path={fixture.RootPath}");
        Console.WriteLine($"fixture-descriptor-path={fixture.DescriptorPath}");
        Console.WriteLine("target-execution=not-performed");
        Console.WriteLine("dependency-installation=not-performed");
        Console.WriteLine("network-access=not-performed");
    }

    private static string Compared(ChangeBenchmarkOptions options, long value) =>
        options.CombinedOnly
            ? "not-measured"
            : value.ToString(CultureInfo.InvariantCulture);

    private static string Compared(ChangeBenchmarkOptions options, string value) =>
        options.CombinedOnly ? "not-measured" : value;

    private static void WriteRepositoryState(
        RepositoryStateSnapshot worktree,
        RepositoryStateSnapshot git,
        bool lightweight)
    {
        Console.WriteLine($"worktree-files={worktree.FileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"worktree-bytes={(lightweight ? "not-measured" : worktree.TotalBytes.ToString(CultureInfo.InvariantCulture))}");
        Console.WriteLine($"worktree-digest={worktree.Digest}");
        Console.WriteLine(
            $"git-state-files={(lightweight ? "not-measured" : git.FileCount.ToString(CultureInfo.InvariantCulture))}");
        Console.WriteLine(
            $"git-state-bytes={(lightweight ? "not-measured" : git.TotalBytes.ToString(CultureInfo.InvariantCulture))}");
        Console.WriteLine($"git-state-digest={git.Digest}");
        Console.WriteLine(
            $"repository-state-check={(lightweight ? "git-status-head-refs-and-object-count" : "full-content-digest")}");
        Console.WriteLine("worktree-unchanged=true");
        Console.WriteLine("git-state-unchanged=true");
        Console.WriteLine("target-execution=not-performed");
        Console.WriteLine("dependency-installation=not-performed");
        Console.WriteLine("network-access=not-performed");
    }
}
