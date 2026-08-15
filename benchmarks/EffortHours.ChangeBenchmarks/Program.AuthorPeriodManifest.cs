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
            execution.IndependentReportsEquivalent,
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
            execution.UniqueIndependentChanges != expectedChanges)
        {
            throw new InvalidOperationException(
                $"Expected {expectedChanges} repository-scoped changes, but observed " +
                $"{execution.SelectedChanges} combined and {execution.UniqueIndependentChanges} independent.");
        }

        if (execution.Statistics.RepositoryCount != fixture.Repositories.Count ||
            execution.Statistics.ObjectDatabaseReaders != fixture.Repositories.Count ||
            execution.CombinedSnapshotAnalyses >= execution.IndependentSnapshotAnalyses)
        {
            throw new InvalidOperationException(
                "The combined manifest did not preserve the required repository-session or analysis reuse.");
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
        Console.WriteLine("benchmark=change/1.4.0");
        Console.WriteLine($"estimator={ChangeEstimator.Version}");
        Console.WriteLine($"mode={options.Name}");
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={RuntimeInformation.OSDescription}");
        Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-files-per-repository={options.Files.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-lines-per-file={options.LinesPerFile.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-qualifying-commits-per-repository={options.Commits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-repositories={fixture.Repositories.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-heads={fixture.HeadCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"portfolio-contributors={fixture.ContributorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"selected-changes={execution.SelectedChanges.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-snapshot-analyses={execution.CombinedSnapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"independent-snapshot-analyses={execution.IndependentSnapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"independent-invocations={execution.IndependentInvocations.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"independent-empty-invocations={execution.EmptyIndependentInvocations.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"independent-selected-rows={execution.IndependentSelectedRows.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"independent-unique-changes={execution.UniqueIndependentChanges.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"combined-git-object-readers={reuse.ObjectDatabaseReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"independent-git-object-readers={execution.IndependentObjectReaders.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-requests={reuse.SnapshotAnalysisRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-analysis-hits={reuse.SnapshotAnalysisHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-requests={reuse.SnapshotInventoryRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"snapshot-inventory-hits={reuse.SnapshotInventoryHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"full-snapshot-inventory-loads={reuse.FullSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"incremental-snapshot-inventory-loads={reuse.IncrementalSnapshotInventoryLoads.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-requests={reuse.BlobRequests.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"blob-cache-hits={reuse.BlobCacheHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"peak-cached-blob-mib={ToMebibytes(reuse.PeakCachedBlobBytesPerRepository)}");
        Console.WriteLine($"combined-estimate-seconds={Duration(execution.CombinedElapsed)}");
        Console.WriteLine($"combined-estimate-cpu-seconds={Duration(execution.CombinedCpu)}");
        Console.WriteLine($"independent-estimate-seconds={Duration(execution.IndependentElapsed)}");
        Console.WriteLine($"independent-estimate-cpu-seconds={Duration(execution.IndependentCpu)}");
        Console.WriteLine($"combined-faster-than-independent={Lower(execution.CombinedElapsed < execution.IndependentElapsed)}");
        Console.WriteLine($"less-repeated-analysis={Lower(execution.CombinedSnapshotAnalyses < execution.IndependentSnapshotAnalyses)}");
        Console.WriteLine($"independent-reports-equivalent={Lower(execution.IndependentReportsEquivalent)}");
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
