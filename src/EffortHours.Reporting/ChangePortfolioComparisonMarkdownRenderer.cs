using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static partial class ChangePortfolioComparisonMarkdownRenderer
{
    public static string Render(ChangePortfolioComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change portfolio comparison report is invalid: " +
                string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        if (report.View == ChangePortfolioComparisonView.Findings)
        {
            AppendFindings(markdown, report);
        }
        else
        {
            AppendTrendReport(markdown, report);
        }

        AppendResourceUsage(markdown, report.Execution.Resources);

        return markdown.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    private static void AppendHeader(StringBuilder markdown, ChangePortfolioComparisonReport report)
    {
        markdown.Append("# ").AppendLine(Escape(report.Title));
        markdown.AppendLine();
        markdown.Append("Generated: `")
            .Append(report.GeneratedAt.ToString("O", CultureInfo.InvariantCulture))
            .AppendLine("`  ");
        markdown.Append("Status: `").Append(Kebab(report.Status)).AppendLine("`  ");
        markdown.Append("CLI: `").Append(Escape(report.CliVersion)).Append("`; estimator: `")
            .Append(Escape(report.EstimatorVersion)).AppendLine("`  ");
        markdown.Append("Immutable author-period input: `")
            .Append(Escape(report.Selection.AuthorPeriodManifest!.ManifestDigest))
            .AppendLine("`  ");
        markdown.Append("Immutable source portfolio: `")
            .Append(Escape(report.Verification.SourcePortfolioDigest ?? "unavailable-incomplete"))
            .AppendLine("`  ");
        markdown.Append("Semantic comparison: `")
            .Append(Escape(report.Verification.SemanticDigest)).AppendLine("`");
        markdown.AppendLine();
        markdown.AppendLine(
            "> EHE is replacement effort for one competent senior contractor unfamiliar with the domain and not using AI. It is not actual labor, a timesheet, productivity, authorship, compensation, or an invoice.");
    }

    private static void AppendEffortHeader(StringBuilder markdown)
    {
        markdown.AppendLine("| Bucket | Covered dates | Capacity | EHE low | EHE expected | EHE high | Ratio low | Ratio expected | Ratio high | Changes |");
        markdown.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
    }

    private static void AppendResourceUsage(
        StringBuilder markdown,
        ChangePortfolioComparisonResourceUsage? resources)
    {
        if (resources is null)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Bounded execution scope");
        markdown.AppendLine();
        markdown.AppendLine("| Candidates | Selected changes | Projected / analyzed snapshots | Logical selection chunks | Analysis chunks | Ledger charge | Peak working set | Output bytes / bound |");
        markdown.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");
        markdown.Append("| ").Append(resources.CandidateCount)
            .Append(" | ").Append(resources.SelectedChangeCount)
            .Append(" | ").Append(resources.ProjectedSnapshotRequests)
            .Append(" / ").Append(resources.SnapshotAnalysisRequests)
            .Append(" | ").Append(resources.SelectionChunkCount)
            .Append(" | ").Append(resources.AnalysisChunkCount)
            .Append(" | ").Append(resources.ChargedCandidateLedgerBytes)
            .Append(" | ").Append(Mebibytes(resources.PeakWorkingSetBytes))
            .Append(" | ").Append(resources.RenderedOutputBytes)
            .Append(" / ").Append(resources.MaximumRenderedOutputBytes)
            .AppendLine(" |");
        markdown.AppendLine();
        markdown.Append("Declared pipeline bounds: ")
            .Append(resources.MaximumConcurrentRepositories).Append(" repositories, ")
            .Append(resources.MaximumBufferedChangesPerRepository)
            .Append(" buffered changes per repository, ")
            .Append(resources.MaximumConcurrentCpuWorkItems).Append(" CPU work items, ")
            .Append(resources.MaximumConcurrentGitTreeReads).Append(" Git tree reads, and ")
            .Append(resources.MaximumPendingFileInspections)
            .AppendLine(" pending file inspections.");
        markdown.AppendLine();
        markdown.AppendLine("This Markdown is summary-only and omits per-change detail rows. Every selected change still participates in the globally reconciled calculation. The candidate ledger charge is deterministic retained-state accounting, while peak working set is an observed process sample.");
    }

    private static void AppendPointRow(
        StringBuilder markdown,
        ChangePortfolioComparisonBucket bucket,
        ChangePortfolioComparisonPoint point)
    {
        markdown.Append("| ").Append(Escape(bucket.Label)).Append(" | ")
            .Append(bucket.SinceInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append('–')
            .Append(bucket.UntilExclusive.AddTicks(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Append(" | ").Append(Number(point.CapacityHours)).Append(" | ")
            .Append(Hours(point.Effort.Low)).Append(" | ")
            .Append(Hours(point.Effort.Expected)).Append(" | ")
            .Append(Hours(point.Effort.High)).Append(" | ")
            .Append(Ratio(point.CapacityRatio?.Low)).Append(" | ")
            .Append(Ratio(point.CapacityRatio?.Expected)).Append(" | ")
            .Append(Ratio(point.CapacityRatio?.High)).Append(" | ")
            .Append(point.SelectedChangeCount).AppendLine(" |");
    }

    private static void AppendTotalRow(
        StringBuilder markdown,
        ChangePortfolioComparisonSeries series)
    {
        markdown.Append("| **Full period** | — | **")
            .Append(Number(series.TotalCapacityHours)).Append("** | **")
            .Append(Hours(series.TotalEffort.Low)).Append("** | **")
            .Append(Hours(series.TotalEffort.Expected)).Append("** | **")
            .Append(Hours(series.TotalEffort.High)).Append("** | **")
            .Append(Ratio(series.TotalCapacityRatio?.Low)).Append("** | **")
            .Append(Ratio(series.TotalCapacityRatio?.Expected)).Append("** | **")
            .Append(Ratio(series.TotalCapacityRatio?.High)).Append("** | **")
            .Append(series.Points.Sum(point => point.SelectedChangeCount)).AppendLine("** |");
    }

    private static string Kebab<T>(T value) where T : struct, Enum =>
        ReportFormatting.Kebab(value);

    private static string Escape(string value) => ReportFormatting.Escape(value);

    private static string Hours(decimal value) => value.ToString("#,0.00", CultureInfo.InvariantCulture);

    private static string Number(decimal? value) =>
        value?.ToString("#,0.##", CultureInfo.InvariantCulture) ?? "—";

    private static string Ratio(decimal? value) =>
        value is null ? "—" : value.Value.ToString("0.00×", CultureInfo.InvariantCulture);

    private static string Percentage(decimal? value) =>
        value is null ? "unavailable" : value.Value.ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static string Mebibytes(long bytes) =>
        (bytes / 1024m / 1024m).ToString("#,0.00", CultureInfo.InvariantCulture) + " MiB";
}
