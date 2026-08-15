using System.Diagnostics;
using System.Globalization;

namespace EffortHours.ChangeBenchmarks;

internal sealed record AuthorPeriodWorkerMeasurement(
    string ReportDigest,
    int SelectedChanges,
    decimal ExpectedEffort,
    decimal WallSeconds,
    decimal CpuSeconds,
    decimal PeakWorkingSetMib);

internal sealed record AuthorPeriodProcessGroupMeasurement(
    int ProcessCount,
    decimal GroupWallSeconds,
    IReadOnlyList<AuthorPeriodWorkerMeasurement> Workers);

internal sealed record AuthorPeriodProcessMatrixResult(
    IReadOnlyList<AuthorPeriodProcessGroupMeasurement> Groups)
{
    public int WorkerCount => Groups.Sum(group => group.Workers.Count);

    public bool ReportsEquivalent => Groups
        .SelectMany(group => group.Workers)
        .Select(worker => worker.ReportDigest)
        .Distinct(StringComparer.Ordinal)
        .Count() == 1;
}

internal static class AuthorPeriodProcessMatrix
{
    private static readonly int[] ProcessCounts = [1, 2, 3];

    public static async Task<AuthorPeriodProcessMatrixResult> RunAsync(
        string repositoryPath,
        string headObjectId,
        CancellationToken cancellationToken)
    {
        List<AuthorPeriodProcessGroupMeasurement> groups = [];
        foreach (int count in ProcessCounts)
        {
            groups.Add(await RunGroupAsync(
                repositoryPath,
                headObjectId,
                count,
                cancellationToken).ConfigureAwait(false));
        }

        AuthorPeriodProcessMatrixResult result = new(groups);
        if (!result.ReportsEquivalent)
        {
            throw new InvalidOperationException(
                "Concurrent author-period workers produced different deterministic reports.");
        }

        return result;
    }

    private static async Task<AuthorPeriodProcessGroupMeasurement> RunGroupAsync(
        string repositoryPath,
        string headObjectId,
        int processCount,
        CancellationToken cancellationToken)
    {
        string coordinationRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-author-process-matrix",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coordinationRoot);
        string gatePath = Path.Combine(coordinationRoot, "start.gate");
        List<RunningWorker> workers = [];
        try
        {
            for (int index = 0; index < processCount; index++)
            {
                string readyPath = Path.Combine(coordinationRoot, $"worker-{index:D2}.ready");
                workers.Add(StartWorker(repositoryPath, headObjectId, readyPath, gatePath));
            }

            await WaitForReadyAsync(workers, cancellationToken).ConfigureAwait(false);
            Stopwatch groupTimer = Stopwatch.StartNew();
            await File.WriteAllTextAsync(gatePath, "start", cancellationToken).ConfigureAwait(false);
            AuthorPeriodWorkerMeasurement[] measurements = await Task.WhenAll(
                workers.Select(worker => CompleteAsync(worker, cancellationToken)))
                .ConfigureAwait(false);
            groupTimer.Stop();
            return new AuthorPeriodProcessGroupMeasurement(
                processCount,
                (decimal)groupTimer.Elapsed.TotalSeconds,
                measurements);
        }
        finally
        {
            foreach (RunningWorker worker in workers)
            {
                try
                {
                    if (!worker.Process.HasExited)
                    {
                        worker.Process.Kill(entireProcessTree: true);
                        _ = worker.Process.WaitForExit(5_000);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The worker exited between the state check and termination request.
                }

                worker.Process.Dispose();
            }

            if (Directory.Exists(coordinationRoot))
            {
                Directory.Delete(coordinationRoot, recursive: true);
            }
        }
    }

    private static RunningWorker StartWorker(
        string repositoryPath,
        string headObjectId,
        string readyPath,
        string gatePath)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("--author-period-worker");
        startInfo.ArgumentList.Add(repositoryPath);
        startInfo.ArgumentList.Add(headObjectId);
        startInfo.ArgumentList.Add(readyPath);
        startInfo.ArgumentList.Add(gatePath);
        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start an author-period benchmark worker.");
        return new RunningWorker(
            process,
            readyPath,
            process.StandardOutput.ReadToEndAsync(),
            process.StandardError.ReadToEndAsync());
    }

    private static async Task WaitForReadyAsync(
        IReadOnlyList<RunningWorker> workers,
        CancellationToken cancellationToken)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (workers.Any(worker => !File.Exists(worker.ReadyPath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunningWorker? exited = workers.FirstOrDefault(worker => worker.Process.HasExited);
            if (exited is not null)
            {
                throw new InvalidOperationException(
                    $"An author-period worker exited before the start gate with code {exited.Process.ExitCode}.");
            }

            if (timer.Elapsed > TimeSpan.FromSeconds(30))
            {
                throw new TimeoutException("Author-period workers did not become ready within 30 seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<AuthorPeriodWorkerMeasurement> CompleteAsync(
        RunningWorker worker,
        CancellationToken cancellationToken)
    {
        await worker.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string standardOutput = await worker.StandardOutput.ConfigureAwait(false);
        string standardError = await worker.StandardError.ConfigureAwait(false);
        if (worker.Process.ExitCode != 0 || !string.IsNullOrWhiteSpace(standardError))
        {
            throw new InvalidOperationException(
                $"Author-period worker failed with exit {worker.Process.ExitCode}: {standardError.Trim()}");
        }

        Dictionary<string, string> values = standardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        return new AuthorPeriodWorkerMeasurement(
            values["report-digest"],
            int.Parse(values["selected-changes"], CultureInfo.InvariantCulture),
            decimal.Parse(values["expected-ehe"], CultureInfo.InvariantCulture),
            decimal.Parse(values["worker-seconds"], CultureInfo.InvariantCulture),
            decimal.Parse(values["worker-cpu-seconds"], CultureInfo.InvariantCulture),
            decimal.Parse(values["worker-peak-working-set-mib"], CultureInfo.InvariantCulture));
    }

    private sealed record RunningWorker(
        Process Process,
        string ReadyPath,
        Task<string> StandardOutput,
        Task<string> StandardError);
}
