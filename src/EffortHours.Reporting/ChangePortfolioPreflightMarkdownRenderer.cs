using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangePortfolioPreflightMarkdownRenderer
{
    public static string Render(ChangePortfolioPreflightReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change portfolio preflight report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.AppendLine("# Author-period portfolio preflight");
        markdown.AppendLine();
        markdown.Append("- Status: **").Append(report.Status.ToString()).AppendLine("**");
        markdown.Append("- Recommended action: **")
            .Append(report.Recommendation.Action.ToString()).AppendLine("**");
        markdown.Append("- Manifest digest: `")
            .Append(report.Verification.ManifestDigest).AppendLine("`");
        markdown.Append("- Exact interval: ")
            .Append(report.Selection.AuthorPeriodManifest!.SinceInclusive.ToString(
                "O",
                CultureInfo.InvariantCulture))
            .Append(" to ")
            .Append(report.Selection.AuthorPeriodManifest.UntilExclusive.ToString(
                "O",
                CultureInfo.InvariantCulture))
            .AppendLine(" (since-inclusive, until-exclusive)");
        markdown.AppendLine();
        markdown.AppendLine("## Measured scope");
        markdown.AppendLine();
        markdown.AppendLine("| Repository | Heads | Candidates | Selected | Snapshots | Logical selection chunks | Analysis chunks | Ledger charge / bound | Blocking resource |");
        markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (ChangePortfolioPreflightRepository repository in report.Repositories)
        {
            markdown.Append("| ").Append(repository.RepositoryId)
                .Append(" | ").Append(repository.HeadCount)
                .Append(" | ").Append(repository.CandidateCount)
                .Append(repository.CandidateCountIsLowerBound ? "+" : string.Empty)
                .Append(" | ").Append(Optional(repository.SelectedChangeCount))
                .Append(" | ").Append(Optional(repository.ProjectedSnapshotRequests))
                .Append(" | ").Append(repository.SelectionChunkCount)
                .Append(" | ").Append(Optional(repository.AnalysisChunkCount))
                .Append(" | ").Append(repository.ChargedCandidateLedgerBytes)
                .Append(" / ").Append(repository.MaximumCandidateLedgerBytes)
                .Append(" | ").Append(repository.BlockingResource ?? "none")
                .AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Recommendation");
        markdown.AppendLine();
        markdown.AppendLine(report.Recommendation.Reason);
        markdown.AppendLine();
        foreach (string step in report.Recommendation.Steps)
        {
            markdown.Append("- ").AppendLine(step);
        }

        markdown.AppendLine();
        markdown.AppendLine("## Boundaries");
        markdown.AppendLine();
        markdown.Append("- Candidate ledger: ")
            .Append(report.Resources.MaximumCandidateLedgerBytesPerRepository)
            .AppendLine(" charged bytes per repository.");
        markdown.Append("- Work chunks: ").Append(report.Resources.SelectionChunkSize)
            .Append(" selection candidates and ").Append(report.Resources.AnalysisChunkSize)
            .AppendLine(" selected changes.");
        markdown.Append("- Concurrency: ").Append(report.Resources.MaximumConcurrentRepositories)
            .Append(" repositories and ")
            .Append(report.Resources.MaximumConcurrentChangesPerRepository)
            .Append(" buffered changes per repository; ")
            .Append(report.Resources.MaximumConcurrentCpuWorkItems)
            .Append(" managed CPU work items, ")
            .Append(report.Resources.MaximumConcurrentGitTreeReads)
            .Append(" Git tree reads, and ")
            .Append(report.Resources.MaximumPendingFileInspections)
            .AppendLine(" pending file inspections process-wide.");
        markdown.Append("- Buffered source reads: at most ")
            .Append(report.Resources.MaximumBufferedFileBytes)
            .AppendLine(" bytes per admitted file read.");
        markdown.Append("- Output/checkpoint bounds: ")
            .Append(report.Resources.MaximumRenderedOutputBytes).Append(" rendered bytes and ")
            .Append(report.Resources.MaximumCheckpointBytesPerRepository)
            .AppendLine(" checkpoint bytes per repository.");
        markdown.AppendLine("- This preflight did not construct snapshots, analyze source, estimate EHE, or expose paths/aliases.");
        return markdown.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    private static string Optional<T>(T? value) where T : struct =>
        value?.ToString() ?? "unknown";
}
