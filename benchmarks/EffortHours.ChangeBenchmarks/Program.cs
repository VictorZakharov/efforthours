using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using EffortHours.Change;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

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
            GitChangePlanner planner = new(
                new GitClient(),
                new UnsupportedPullRequestResolver(),
                new GitChangePlannerOptions
                {
                    MaximumRangeComponents = options.MaximumRangeComponents,
                });

            long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
            long workingSetBefore = Environment.WorkingSet;
            await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
            Stopwatch timer = Stopwatch.StartNew();
            GitChangePlan plan = options.Mode == ChangeBenchmarkMode.LargeTree
                ? await planner.PlanBaseHeadAsync(
                    repository.RootPath,
                    repository.BaseObjectId,
                    repository.HeadObjectId,
                    cancellation.Token).ConfigureAwait(false)
                : await planner.PlanRangeAsync(
                    repository.RootPath,
                    $"{repository.BaseObjectId}..{repository.HeadObjectId}",
                    cancellation.Token).ConfigureAwait(false);
            ChangeEstimateReport report = await new ChangeEstimator(repositoryEstimator).EstimateAsync(
                plan,
                EstimationProfile.Implementation,
                cancellationToken: cancellation.Token).ConfigureAwait(false);
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

            bool auditBounded = plan.Diagnostics.Any(diagnostic => diagnostic.Code == "FB5105");
            int expectedSnapshotAnalyses = auditBounded ? 2 : options.Commits + 1;
            if (repositoryEstimator.InvocationCount != expectedSnapshotAnalyses)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedSnapshotAnalyses} unique snapshot analyses, but observed " +
                    $"{repositoryEstimator.InvocationCount}.");
            }

            bool secondsPassed = options.MaximumSeconds is null ||
                (decimal)timer.Elapsed.TotalSeconds <= options.MaximumSeconds.Value;
            decimal peakMib = ToMebibytesValue(peakWorkingSet);
            bool memoryPassed = options.MaximumPeakMib is null || peakMib <= options.MaximumPeakMib.Value;
            WriteResults(
                options,
                repository,
                plan,
                report,
                repositoryEstimator.InvocationCount,
                timer.Elapsed,
                allocations,
                workingSetBefore,
                peakWorkingSet,
                workingSetAfter,
                worktreeBefore,
                gitBefore,
                auditBounded,
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
        GitChangePlan plan,
        ChangeEstimateReport report,
        int snapshotAnalyses,
        TimeSpan elapsed,
        long allocations,
        long workingSetBefore,
        long peakWorkingSet,
        long workingSetAfter,
        RepositoryStateSnapshot worktree,
        RepositoryStateSnapshot git,
        bool auditBounded,
        bool secondsPassed,
        bool memoryPassed)
    {
        Console.WriteLine("benchmark=change/1.0.0");
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
        Console.WriteLine($"planned-components={plan.Components.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"range-audit-bounded={Lower(auditBounded)}");
        Console.WriteLine($"repository-estimator-invocations={snapshotAnalyses.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"change-seconds={elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"change-allocated-mib={ToMebibytes(allocations)}");
        Console.WriteLine($"change-working-set-start-mib={ToMebibytes(workingSetBefore)}");
        Console.WriteLine($"change-peak-working-set-mib={ToMebibytes(peakWorkingSet)}");
        Console.WriteLine($"change-working-set-end-mib={ToMebibytes(workingSetAfter)}");
        Console.WriteLine($"expected-ehe={report.TotalEffort.Expected.ToString("F2", CultureInfo.InvariantCulture)}");
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

    private sealed class CountingEstimator : IEstimator
    {
        private readonly SeedEstimator _inner = new();

        public int InvocationCount { get; private set; }

        public EstimateReport Estimate(
            RepositoryEvidence evidence,
            EstimationProfile profile,
            RateCard? rateCard = null)
        {
            InvocationCount++;
            return _inner.Estimate(evidence, profile, rateCard);
        }
    }

    private sealed class UnsupportedPullRequestResolver : IPullRequestResolver
    {
        public Task<ResolvedPullRequest> ResolveAsync(
            string repositoryPath,
            string input,
            string? repository,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The Change benchmark does not resolve pull requests.");
    }
}
