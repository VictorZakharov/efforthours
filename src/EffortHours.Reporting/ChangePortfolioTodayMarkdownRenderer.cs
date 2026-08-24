using System.Globalization;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static class ChangePortfolioTodayMarkdownRenderer
{
    public static string Render(ChangePortfolioComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The today-to-date comparison report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        DateTimeOffset asOf = report.AsOf ??
            throw new ArgumentException("A today-to-date report requires an as-of instant.", nameof(report));
        ChangePortfolioHostDiscovery discovery = report.Discovery ??
            throw new ArgumentException("A today-to-date report requires discovery metadata.", nameof(report));
        ChangePortfolioScopeProfile profile = report.ScopeProfile ??
            throw new ArgumentException("A today-to-date report requires scope metadata.", nameof(report));
        if (report.BucketPolicy.Policy != ChangePortfolioComparisonPolicies.TodayToDateV1 ||
            report.Buckets.Count != 1 || !report.Buckets[0].PartialEnd)
        {
            throw new ArgumentException(
                "A today-to-date report requires one partial daily bucket.",
                nameof(report));
        }

        StringBuilder markdown = new();
        markdown.Append("# ").AppendLine(Escape(report.Title));
        markdown.AppendLine();
        markdown.Append("Status: **").Append(report.Status == ChangePortfolioComparisonStatus.Complete
            ? "complete"
            : "incomplete").AppendLine("**");
        if (report.Status == ChangePortfolioComparisonStatus.Complete)
        {
            AppendCompleteSummary(markdown, report, discovery, asOf);
            AppendRepositories(markdown, report);
        }
        else
        {
            markdown.AppendLine();
            markdown.AppendLine(
                "No EHE aggregate or X factor is published because required work is incomplete.");
        }

        AppendScope(markdown, report, discovery, profile, asOf);
        AppendExecution(markdown, report);
        AppendFailures(markdown, report);
        AppendLimits(markdown);
        return markdown.ToString().ReplaceLineEndings("\n").TrimEnd() + "\n";
    }

    private static void AppendCompleteSummary(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report,
        ChangePortfolioHostDiscovery discovery,
        DateTimeOffset asOf)
    {
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(series =>
            series.Kind == ChangePortfolioSeriesKind.Portfolio);
        ChangePortfolioRatioRange ratio = portfolio.TotalCapacityRatio ??
            throw new ArgumentException("Today-to-date capacity ratios are missing.", nameof(report));
        ChangePortfolioScopeSummary scope = report.ScopeSummary!;
        int headCount = discovery.DefaultHeadCount + discovery.OpenPullRequestHeadCount;
        markdown.AppendLine();
        markdown.Append("Snapshot through: `")
            .Append(asOf.ToString("O", CultureInfo.InvariantCulture)).Append("` (`")
            .Append(Escape(report.Selection.AuthorPeriodManifest!.TimeZone)).AppendLine("`)");
        markdown.Append("EHE low / expected / high: **")
            .Append(Hours(portfolio.TotalEffort.Low)).Append(" / ")
            .Append(Hours(portfolio.TotalEffort.Expected)).Append(" / ")
            .Append(Hours(portfolio.TotalEffort.High)).AppendLine(" h**");
        markdown.Append("X low / expected / high: **")
            .Append(Ratio(ratio.Low)).Append(" / ")
            .Append(Ratio(ratio.Expected)).Append(" / ")
            .Append(Ratio(ratio.High)).AppendLine("**");
        markdown.Append("Reference-capacity policy: **")
            .Append(Hours(portfolio.TotalCapacityHours!.Value))
            .AppendLine(" caller-supplied hours for the partial daily bucket**");
        markdown.AppendLine(
            "Actual-hours formula: `actual X = expected EHE / actual hours` (actual hours are not inferred by EffortHours).");
        markdown.Append("Coverage: **").Append(discovery.ActiveRepositoryCount)
            .Append(" repositories / ").Append(headCount).Append(" heads / ")
            .Append(scope.IdentitySelectedCommitCount).Append(" identity-selected commits / ")
            .Append(scope.AdmittedCommitCount).Append(" engineering commits / ")
            .Append(scope.ScopeEmptyCommitCount).AppendLine(" scope-empty commits**");
    }

    private static void AppendRepositories(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioAggregation aggregation = report.SourcePortfolio!.Aggregation!;
        if (aggregation.Repositories.Count == 0)
        {
            markdown.AppendLine();
            markdown.AppendLine("Repository attribution: no selected work.");
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Repository attribution");
        markdown.AppendLine();
        markdown.AppendLine("| Repository | Heads | Selected | Expected EHE |\n|---|---:|---:|---:|");
        foreach (ChangePortfolioRepositorySummary repository in aggregation.Repositories
            .OrderBy(value => value.RepositoryId, StringComparer.Ordinal))
        {
            int heads = repository.Heads.Count(head => head.ReachableSelectedCommitCount > 0);
            markdown.Append("| `").Append(Escape(repository.RepositoryId)).Append("` | ")
                .Append(heads).Append(" | ").Append(repository.SelectedCommitCount).Append(" | ")
                .Append(Hours(repository.NormalizedEffort.Expected)).AppendLine(" h |");
        }
    }

    private static void AppendScope(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report,
        ChangePortfolioHostDiscovery discovery,
        ChangePortfolioScopeProfile profile,
        DateTimeOffset asOf)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Scope and discovery");
        markdown.AppendLine();
        markdown.Append("- Profile: `").Append(Escape(profile.Id)).Append("` / `")
            .Append(Escape(profile.Version)).Append("` / `").Append(Escape(profile.Digest))
            .Append("` (`").Append(Escape(profile.Source)).AppendLine("`).");
        markdown.Append("- Important exclusions: ")
            .AppendLine(string.Join(", ", profile.ImportantExclusions.Select(value =>
                "`" + Escape(value) + "`")) + ".");
        markdown.Append("- GitHub discovery was ").Append(discovery.Complete ? "complete" : "incomplete")
            .Append(": ").Append(discovery.ProviderRepositoryCount).Append(" provider repositories, ")
            .Append(discovery.ConsideredRepositoryCount).Append(" considered, ")
            .Append(discovery.ActiveRepositoryCount).Append(" active, ")
            .Append(discovery.OpenPullRequestCount).Append(" user-authored open PRs, ")
            .Append(discovery.ProviderPageCount).Append(" pages, and ")
            .Append(discovery.ProviderQueryCount).Append(" queries across ")
            .Append(discovery.ProviderProcessCount).Append(" provider processes (")
            .Append(Milliseconds(discovery.ProviderProcessStartupMilliseconds))
            .Append(" startup); metadata cache ")
            .Append(discovery.ProviderMetadataCacheHit ? "hit" : "miss")
            .AppendLine(".");
        markdown.Append("- Managed cache used ").Append(discovery.LocalObjectCount)
            .Append(" existing selected objects and acquired ")
            .Append(discovery.AcquiredObjectCount).Append(" objects / ")
            .Append(Bytes(discovery.AcquiredBytes)).AppendLine(".");
        markdown.Append("- The bucket begins at `")
            .Append(report.Buckets[0].SinceInclusive.ToString("O", CultureInfo.InvariantCulture))
            .Append("` and is partial through `")
            .Append(asOf.ToString("O", CultureInfo.InvariantCulture)).AppendLine("`.");
    }

    private static void AppendExecution(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonExecution execution = report.Execution;
        markdown.AppendLine();
        markdown.AppendLine("## Execution");
        markdown.AppendLine();
        markdown.Append("- Estimators: portfolio `").Append(Escape(report.EstimatorVersion))
            .Append("`; change `").Append(Escape(report.SourceChangeEstimatorVersion))
            .Append("`; CLI `").Append(Escape(report.CliVersion)).AppendLine("`.");
        markdown.Append("- Checkpoints: ").Append(execution.Checkpoint.Enabled ? "enabled" : "disabled")
            .Append("; hits ").Append(execution.Checkpoint.HitCount)
            .Append(", misses ").Append(execution.Checkpoint.MissCount)
            .Append(", writes ").Append(execution.Checkpoint.WriteCount)
            .Append(", failures ").Append(execution.Checkpoint.FailureCount).AppendLine(".");
        markdown.Append("- In-run analysis reuse: snapshots ")
            .Append(execution.Reuse.SnapshotAnalysisHits).Append('/')
            .Append(execution.Reuse.SnapshotAnalysisRequests).Append(", artifacts ")
            .Append(execution.Reuse.AnalysisArtifactHits).Append('/')
            .Append(execution.Reuse.AnalysisArtifactRequests).Append(", inventories ")
            .Append(execution.Reuse.SnapshotInventoryHits).Append('/')
            .Append(execution.Reuse.SnapshotInventoryRequests).AppendLine(".");
        markdown.Append("- End to end: ")
            .Append(Milliseconds(execution.EndToEndElapsedMilliseconds)).AppendLine(".");
        if (execution.PhaseTimings.Count > 0)
        {
            markdown.Append("- Phase timings: ")
                .AppendLine(string.Join(", ", execution.PhaseTimings.Select(timing =>
                    $"`{Escape(timing.Phase)}` {Milliseconds(timing.ElapsedMilliseconds)}")) + ".");
        }
    }

    private static void AppendFailures(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        if (report.Execution.Failures.Count == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("## Failures");
        markdown.AppendLine();
        foreach (ChangePortfolioComparisonFailure failure in report.Execution.Failures
            .OrderBy(value => value.RepositoryId, StringComparer.Ordinal)
            .ThenBy(value => value.Phase, StringComparer.Ordinal))
        {
            markdown.Append("- `").Append(Escape(failure.RepositoryId)).Append("` / `")
                .Append(Escape(failure.Phase)).Append("` / `")
                .Append(Escape(failure.Category)).Append("`: ")
                .Append(Escape(failure.Message)).Append(" (`")
                .Append(Escape(failure.MessageDigest)).AppendLine("`)");
            if (failure.AgentAction is not null)
            {
                markdown.Append("  - Agent action: `")
                    .Append(Escape(ContractJson.SerializeCompact(failure.AgentAction)))
                    .AppendLine("`");
            }
        }
    }

    private static void AppendLimits(StringBuilder markdown)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Interpretation limits");
        markdown.AppendLine();
        markdown.AppendLine(
            "> EHE is experimental, uncalibrated replacement effort, not actual labor, a timesheet, productivity, authorship, compensation, or an invoice. Low/high are planning uncertainty bounds. Capacity is only a caller-supplied denominator.");
    }

    private static string Escape(string value) => ReportFormatting.Escape(value);

    private static string Hours(decimal value) =>
        value.ToString("#,0.00", CultureInfo.InvariantCulture);

    private static string Ratio(decimal value) =>
        value.ToString("0.#####", CultureInfo.InvariantCulture) + "x";

    private static string Milliseconds(decimal value) =>
        value.ToString("#,0.###", CultureInfo.InvariantCulture) + " ms";

    private static string Bytes(long value) =>
        value.ToString("#,0", CultureInfo.InvariantCulture) + " bytes";
}
