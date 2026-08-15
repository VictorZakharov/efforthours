using System.Globalization;
using EffortHours.Change;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private static ChangePortfolioExecutionTelemetry CreateExecutionTelemetry(
        TextWriter standardError) => new(phase =>
        {
            standardError.WriteLine($"eh: portfolio phase {phase} started");
            standardError.Flush();
        });

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
}
