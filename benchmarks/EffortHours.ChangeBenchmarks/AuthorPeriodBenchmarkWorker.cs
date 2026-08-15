using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal static class AuthorPeriodBenchmarkWorker
{
    private const string WorkerOption = "--author-period-worker";

    public static bool IsWorker(string[] arguments) =>
        arguments.Length > 0 && string.Equals(arguments[0], WorkerOption, StringComparison.Ordinal);

    public static async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length != 5)
        {
            Console.Error.WriteLine(
                "The internal author-period worker requires repository, head, ready, and gate paths.");
            return 2;
        }

        string repositoryPath = Path.GetFullPath(arguments[1]);
        string headObjectId = arguments[2];
        string readyPath = Path.GetFullPath(arguments[3]);
        string gatePath = Path.GetFullPath(arguments[4]);
        await File.WriteAllTextAsync(readyPath, "ready", cancellationToken).ConfigureAwait(false);
        Stopwatch gateWait = Stopwatch.StartNew();
        while (!File.Exists(gatePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (gateWait.Elapsed > TimeSpan.FromSeconds(60))
            {
                throw new TimeoutException("The process-matrix worker did not receive its start gate.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        }

        using Process process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        await using WorkingSetSampler workingSet = WorkingSetSampler.Start();
        Stopwatch timer = Stopwatch.StartNew();
        ChangePortfolioReport report = await RunScenarioAsync(
            repositoryPath,
            headObjectId,
            cancellationToken).ConfigureAwait(false);
        timer.Stop();
        long peakWorkingSet = await workingSet.StopAsync().ConfigureAwait(false);
        process.Refresh();
        TimeSpan cpu = process.TotalProcessorTime - cpuBefore;
        string reportJson = ContractJson.Serialize(report);
        string reportDigest = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(reportJson))).ToLowerInvariant();

        Console.WriteLine($"report-digest={reportDigest}");
        Console.WriteLine($"selected-changes={report.Items.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"expected-ehe={report.TotalEffort.Expected.ToString("F2", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"worker-seconds={timer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"worker-cpu-seconds={cpu.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"worker-peak-working-set-mib={(peakWorkingSet / 1024m / 1024m).ToString("F2", CultureInfo.InvariantCulture)}");
        return 0;
    }

    private static async Task<ChangePortfolioReport> RunScenarioAsync(
        string repositoryPath,
        string headObjectId,
        CancellationToken cancellationToken)
    {
        GitAuthorPeriodPortfolioPlan plan = await new GitPortfolioPlanner().PlanAuthorPeriodAsync(
            repositoryPath,
            new GitAuthorPeriodPortfolioOptions
            {
                Aliases = [GitBenchmarkRepository.SelectedAuthorEmail],
                SinceInclusive = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UntilExclusive = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                HeadRevision = headObjectId,
            },
            cancellationToken).ConfigureAwait(false);
        ChangePortfolioEstimateBatch estimate =
            await new ChangeEstimator().EstimatePortfolioCandidatesWithStatisticsAsync(
                [.. plan.Items.Select(item => item.Plan)],
                EstimationProfile.Implementation,
                cancellationToken).ConfigureAwait(false);
        ChangePortfolioCandidate[] candidates = [.. plan.Items.Select((item, index) =>
            new ChangePortfolioCandidate
            {
                RepositoryId = plan.RepositoryId,
                SelectorId = item.SelectorId,
                Report = estimate.Reports[index],
                Attribution = item.Attribution,
            })];
        return ChangePortfolioReconciler.Reconcile(
            plan.Selection,
            candidates,
            EstimationProfile.Implementation,
            planningDiagnostics: plan.Diagnostics);
    }
}
