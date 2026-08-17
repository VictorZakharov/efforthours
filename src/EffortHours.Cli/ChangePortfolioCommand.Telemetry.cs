using System.Globalization;
using EffortHours.Change;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private static ChangePortfolioExecutionTelemetry CreateExecutionTelemetry(
        TextWriter standardError)
    {
        Lock writerGate = new();
        return new ChangePortfolioExecutionTelemetry(
            phase =>
            {
                lock (writerGate)
                {
                    standardError.WriteLine($"eh: portfolio phase {phase} started");
                    standardError.Flush();
                }
            },
            progress =>
            {
                lock (writerGate)
                {
                    standardError.WriteLine(
                        $"eh: portfolio phase {progress.Phase} progress " +
                        $"{progress.ProcessedUnits}/{progress.TotalUnits} selected changes; " +
                        $"analysis-cache={progress.AnalysisCacheHits}/{progress.AnalysisCacheRequests}; " +
                        $"elapsed={progress.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms; " +
                        $"working-set={ToMebibytes(progress.WorkingSetBytes)} MiB; " +
                        $"observed-peak-working-set={ToMebibytes(progress.PeakWorkingSetBytes)} MiB");
                    standardError.Flush();
                }
            });
    }

    private static async Task WriteExecutionTimingsAsync(
        ChangePortfolioExecutionTelemetry executionTelemetry,
        TextWriter standardError)
    {
        foreach (ChangePortfolioPhaseTiming timing in executionTelemetry.GetTimings())
        {
            await standardError.WriteLineAsync(
                $"eh: portfolio phase {timing.Phase} " +
                $"{timing.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms")
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteInterruptedExecutionAsync(
        ChangePortfolioExecutionTelemetry executionTelemetry,
        TextWriter standardError)
    {
        ChangePortfolioProgress? progress = executionTelemetry.GetLastProgress();
        if (progress is null)
        {
            return;
        }

        string processed = progress.TotalUnits > 0
            ? $"{progress.ProcessedUnits}/{progress.TotalUnits}"
            : "unknown";
        string remaining = progress.TotalUnits > 0
            ? (progress.TotalUnits - progress.ProcessedUnits).ToString(CultureInfo.InvariantCulture)
            : "unknown";
        await standardError.WriteLineAsync(
            $"eh: portfolio interrupted phase={progress.Phase}; " +
            $"elapsed={progress.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)} ms; " +
            $"processed={processed}; remaining={remaining}; " +
            $"analysis-cache={progress.AnalysisCacheHits}/{progress.AnalysisCacheRequests}; " +
            $"working-set={ToMebibytes(progress.WorkingSetBytes)} MiB; " +
            $"observed-peak-working-set={ToMebibytes(progress.PeakWorkingSetBytes)} MiB")
            .ConfigureAwait(false);
    }

    private static string ToMebibytes(long bytes) =>
        (bytes / 1024m / 1024m).ToString("F2", CultureInfo.InvariantCulture);
}
