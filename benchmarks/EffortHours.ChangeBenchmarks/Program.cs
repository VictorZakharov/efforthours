using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

public static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        ChangeBenchmarkOptions options;
        try
        {
            options = ChangeBenchmarkOptions.Parse(arguments);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(ChangeBenchmarkOptions.Usage);
            return 2;
        }

        using CancellationTokenSource cancellation = new();
        void CancelHandler(object? _, ConsoleCancelEventArgs eventArguments)
        {
            eventArguments.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += CancelHandler;
        try
        {
            using GitBenchmarkRepository repository = await GitBenchmarkRepository.CreateAsync(
                options,
                cancellation.Token).ConfigureAwait(false);
            RepositoryStateSnapshot worktreeBefore = RepositoryStateSnapshot.Capture(
                repository.RootPath,
                ".git");
            RepositoryStateSnapshot gitBefore = RepositoryStateSnapshot.Capture(
                Path.Combine(repository.RootPath, ".git"));
            CountingEstimator repositoryEstimator = new();

            long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
            long workingSetBefore = Environment.WorkingSet;
            await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
            Stopwatch timer = Stopwatch.StartNew();
            ChangeBenchmarkExecution execution = await ChangeBenchmarkRunner.RunAsync(
                options,
                repository,
                repositoryEstimator,
                cancellation.Token).ConfigureAwait(false);
            timer.Stop();
            long peakWorkingSet = await workingSet.StopAsync().ConfigureAwait(false);
            long workingSetAfter = Environment.WorkingSet;
            long allocations = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;

            RepositoryStateSnapshot worktreeAfter = RepositoryStateSnapshot.Capture(
                repository.RootPath,
                ".git");
            RepositoryStateSnapshot gitAfter = RepositoryStateSnapshot.Capture(
                Path.Combine(repository.RootPath, ".git"));
            bool worktreeUnchanged = worktreeBefore == worktreeAfter;
            bool gitUnchanged = gitBefore == gitAfter;
            if (!worktreeUnchanged || !gitUnchanged)
            {
                throw new InvalidOperationException(
                    "The worktree or Git database changed during Change analysis; the benchmark refuses " +
                    "to report the run as read-only.");
            }

            if (repositoryEstimator.InvocationCount != execution.ExpectedSnapshotAnalyses)
            {
                throw new InvalidOperationException(
                    $"Expected {execution.ExpectedSnapshotAnalyses} unique snapshot analyses, but observed " +
                    $"{repositoryEstimator.InvocationCount}.");
            }

            if (execution.SelectedChanges !=
                (options.Mode == ChangeBenchmarkMode.AuthorPeriod ? options.Commits : 1))
            {
                throw new InvalidOperationException(
                    $"Expected the benchmark to select {options.Commits} author changes, but observed " +
                    $"{execution.SelectedChanges}.");
            }

            bool secondsPassed = options.MaximumSeconds is null ||
                (decimal)timer.Elapsed.TotalSeconds <= options.MaximumSeconds.Value;
            decimal peakMib = ToMebibytesValue(peakWorkingSet);
            bool memoryPassed = options.MaximumPeakMib is null || peakMib <= options.MaximumPeakMib.Value;
            WriteResults(
                options,
                repository,
                execution,
                repositoryEstimator.InvocationCount,
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
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Change benchmark cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Change benchmark failed: {exception}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
        }
    }

    private static void WriteResults(
        ChangeBenchmarkOptions options,
        GitBenchmarkRepository repository,
        ChangeBenchmarkExecution execution,
        int snapshotAnalyses,
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
        Console.WriteLine("benchmark=change/1.1.0");
        Console.WriteLine($"estimator={ChangeEstimator.Version}");
        Console.WriteLine($"mode={options.Name}");
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={RuntimeInformation.OSDescription}");
        Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-files={options.Files.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-lines-per-file={options.LinesPerFile.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"requested-commits={options.Commits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"maximum-range-components={options.MaximumRangeComponents.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"selected-changes={execution.SelectedChanges.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"planned-components={execution.PlannedComponents.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"range-audit-bounded={Lower(execution.AuditBounded)}");
        Console.WriteLine($"changed-scope-analysis={Lower(execution.ChangedScopeAnalysis)}");
        Console.WriteLine(
            $"full-snapshot-file-limit={ChangeEstimator.FullSnapshotAnalysisFileLimit.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"repository-estimator-invocations={snapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"head-files={repository.HeadFileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"head-directories={repository.HeadDirectoryCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"estimated-legacy-entry-comparisons-per-snapshot=" +
            $"{repository.EstimatedLegacyEntryComparisonsPerSnapshot.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"change-seconds={elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"change-allocated-mib={ToMebibytes(allocations)}");
        Console.WriteLine($"change-working-set-start-mib={ToMebibytes(workingSetBefore)}");
        Console.WriteLine($"change-peak-working-set-mib={ToMebibytes(peakWorkingSet)}");
        Console.WriteLine($"change-working-set-end-mib={ToMebibytes(workingSetAfter)}");
        Console.WriteLine($"expected-ehe={execution.ExpectedEffort.ToString("F2", CultureInfo.InvariantCulture)}");
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
        Console.WriteLine($"seconds-threshold={Threshold(options.MaximumSeconds)}");
        Console.WriteLine($"peak-mib-threshold={Threshold(options.MaximumPeakMib)}");
        Console.WriteLine($"seconds-threshold-passed={Lower(secondsPassed)}");
        Console.WriteLine($"peak-mib-threshold-passed={Lower(memoryPassed)}");
        if (options.KeepRepository)
        {
            Console.WriteLine($"repository-path={repository.RootPath}");
        }
    }

    private static string Threshold(decimal? value) => value?.ToString("F2", CultureInfo.InvariantCulture) ?? "not-set";

    private static string Lower(bool value) => value.ToString().ToLowerInvariant();

    private static decimal ToMebibytesValue(long bytes) => bytes / 1024m / 1024m;

    private static string ToMebibytes(long bytes) =>
        ToMebibytesValue(bytes).ToString("F2", CultureInfo.InvariantCulture);

}
