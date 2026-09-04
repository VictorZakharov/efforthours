using System.Diagnostics;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private static (ChangePortfolioComparisonReport Report, string Output)
        RenderComparisonWithOutputUsage(
            ChangePortfolioComparisonReport source,
            ChangePortfolioCommandOptions options,
            long workflowStarted)
    {
        ChangePortfolioComparisonReport report = source;
        report = report with
        {
            Execution = report.Execution with
            {
                EndToEndElapsedMilliseconds = decimal.Round(
                    (decimal)Stopwatch.GetElapsedTime(workflowStarted).TotalMilliseconds,
                    3,
                    MidpointRounding.AwayFromZero),
            },
        };
        long priorBytes = -1;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            ChangePortfolioComparisonResourceUsage resources = report.Execution.Resources ??
                throw new InvalidOperationException(
                    "Comparison execution resource usage was not measured.");
            report = report with
            {
                Execution = report.Execution with
                {
                    Resources = resources with
                    {
                        RenderedOutputBytes = Math.Max(0, priorBytes),
                    },
                },
            };
            string output = options.Format == "markdown"
                ? options.Today
                    ? ChangePortfolioTodayMarkdownRenderer.Render(report)
                    : options.IsNativePeriod
                        ? ChangePortfolioPeriodMarkdownRenderer.Render(report)
                    : ChangePortfolioComparisonMarkdownRenderer.Render(report)
                : new ChangePortfolioComparisonJsonRenderer(options.Compact).Render(report);
            long measured = RenderedOutputByteCount(output);
            if (measured == priorBytes)
            {
                _ = new ChangePortfolioComparisonJsonRenderer(compact: true).Render(report);
                return (report, output);
            }

            priorBytes = measured;
        }

        throw new InvalidOperationException(
            "Rendered comparison output byte accounting did not converge.");
    }
}
