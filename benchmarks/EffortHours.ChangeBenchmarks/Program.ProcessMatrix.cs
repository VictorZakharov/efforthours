using System.Globalization;
using System.Runtime.InteropServices;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

public static partial class Program
{
    private static async Task<int> RunAuthorPeriodWorkerAsync(string[] arguments)
    {
        try
        {
            return await AuthorPeriodBenchmarkWorker.RunAsync(
                arguments,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Author-period benchmark worker failed: {exception}");
            return 1;
        }
    }

    private static async Task<int> RunAuthorPeriodProcessMatrixAsync(
        ChangeBenchmarkOptions options,
        GitBenchmarkRepository repository,
        RepositoryStateSnapshot worktreeBefore,
        RepositoryStateSnapshot gitBefore,
        RepositoryStateSnapshot? sourceTreeBefore,
        CancellationToken cancellationToken)
    {
        AuthorPeriodProcessMatrixResult matrix = await AuthorPeriodProcessMatrix.RunAsync(
            repository.RootPath,
            repository.HeadObjectId,
            cancellationToken).ConfigureAwait(false);
        RepositoryStateSnapshot worktreeAfter = RepositoryStateSnapshot.Capture(
            repository.RootPath,
            ".git");
        RepositoryStateSnapshot gitAfter = RepositoryStateSnapshot.Capture(
            Path.Combine(repository.RootPath, ".git"));
        RepositoryStateSnapshot? sourceTreeAfter = options.SourceTreePath is null
            ? null
            : RepositoryStateSnapshot.Capture(options.SourceTreePath, ".git");
        bool worktreeUnchanged = worktreeBefore == worktreeAfter;
        bool gitUnchanged = gitBefore == gitAfter;
        bool sourceTreeUnchanged = sourceTreeBefore == sourceTreeAfter;
        if (!worktreeUnchanged || !gitUnchanged || !sourceTreeUnchanged)
        {
            throw new InvalidOperationException(
                "The source tree, worktree, or shared Git database changed during the process matrix.");
        }

        AuthorPeriodWorkerMeasurement single = matrix.Groups.Single(group =>
            group.ProcessCount == 1).Workers.Single();
        if (matrix.Groups.SelectMany(group => group.Workers).Any(worker =>
            worker.SelectedChanges != options.Commits || worker.ExpectedEffort != single.ExpectedEffort))
        {
            throw new InvalidOperationException(
                "Process-matrix workers did not preserve the selected changes and exact total EHE.");
        }

        Console.WriteLine("benchmark=change/1.4.0");
        Console.WriteLine($"estimator={ChangeEstimator.Version}");
        Console.WriteLine("mode=author-period-process-matrix");
        Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"os={RuntimeInformation.OSDescription}");
        Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
        Console.WriteLine(
            $"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"fixture-source={(options.SourceTreePath is null ? "synthetic" : "caller-supplied")}");
        Console.WriteLine("process-matrix=1,2,3");
        Console.WriteLine($"process-matrix-workers={matrix.WorkerCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"process-matrix-reports-equivalent={Lower(matrix.ReportsEquivalent)}");
        Console.WriteLine($"selected-changes={options.Commits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"expected-ehe={single.ExpectedEffort.ToString("F2", CultureInfo.InvariantCulture)}");
        foreach (AuthorPeriodProcessGroupMeasurement group in matrix.Groups)
        {
            decimal maximumWall = group.Workers.Max(worker => worker.WallSeconds);
            decimal totalCpu = group.Workers.Sum(worker => worker.CpuSeconds);
            decimal maximumPeak = group.Workers.Max(worker => worker.PeakWorkingSetMib);
            string prefix = $"process-{group.ProcessCount.ToString(CultureInfo.InvariantCulture)}";
            Console.WriteLine($"{prefix}-workers={group.Workers.Count.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"{prefix}-group-wall-seconds={group.GroupWallSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"{prefix}-max-worker-seconds={maximumWall.ToString("F3", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"{prefix}-total-cpu-seconds={totalCpu.ToString("F3", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"{prefix}-max-worker-peak-working-set-mib={maximumPeak.ToString("F2", CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"{prefix}-max-worker-memory-vs-single=" +
                $"{(maximumPeak / single.PeakWorkingSetMib).ToString("F2", CultureInfo.InvariantCulture)}");
        }

        Console.WriteLine($"head-files={repository.HeadFileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"head-directories={repository.HeadDirectoryCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"worktree-files={worktreeBefore.FileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"worktree-bytes={worktreeBefore.TotalBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"worktree-digest={worktreeBefore.Digest}");
        Console.WriteLine($"git-state-files={gitBefore.FileCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"git-state-bytes={gitBefore.TotalBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"git-state-digest={gitBefore.Digest}");
        WriteSourceTreeResults(repository, sourceTreeBefore, sourceTreeUnchanged);
        Console.WriteLine("worktree-unchanged=true");
        Console.WriteLine("git-state-unchanged=true");
        Console.WriteLine("shared-object-database=true");
        Console.WriteLine("target-execution=not-performed");
        Console.WriteLine("dependency-installation=not-performed");
        Console.WriteLine("network-access=not-performed");
        Console.WriteLine("seconds-threshold=not-set");
        Console.WriteLine("peak-mib-threshold=not-set");
        if (options.KeepRepository)
        {
            Console.WriteLine($"repository-path={repository.RootPath}");
        }

        return 0;
    }

    private static void WriteSourceTreeResults(
        GitBenchmarkRepository repository,
        RepositoryStateSnapshot? sourceTree,
        bool sourceTreeUnchanged)
    {
        Console.WriteLine(
            $"source-tree-files={sourceTree?.FileCount.ToString(CultureInfo.InvariantCulture) ?? "not-applicable"}");
        Console.WriteLine(
            $"source-tree-directories={repository.SourceTree?.DirectoryCount.ToString(CultureInfo.InvariantCulture) ?? "not-applicable"}");
        Console.WriteLine(
            $"source-tree-bytes={sourceTree?.TotalBytes.ToString(CultureInfo.InvariantCulture) ?? "not-applicable"}");
        Console.WriteLine($"source-tree-digest={sourceTree?.Digest ?? "not-applicable"}");
        Console.WriteLine(
            $"source-tree-skipped-links={repository.SourceTree?.SkippedLinkCount.ToString(CultureInfo.InvariantCulture) ?? "not-applicable"}");
        Console.WriteLine(
            $"source-tree-unchanged={(sourceTree is null ? "not-applicable" : Lower(sourceTreeUnchanged))}");
    }
}
