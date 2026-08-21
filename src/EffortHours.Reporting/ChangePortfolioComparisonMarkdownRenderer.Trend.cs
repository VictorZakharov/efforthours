using System.Globalization;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static partial class ChangePortfolioComparisonMarkdownRenderer
{
    private static void AppendTrendReport(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        AppendHeader(markdown, report);
        if (report.Status == ChangePortfolioComparisonStatus.Incomplete)
        {
            AppendCoverage(markdown, report);
            AppendFindingsStatus(markdown, report);
            AppendRepositoryOutcomes(markdown, report);
            AppendFailures(markdown, report);
            AppendPhaseAndProgress(markdown, report);
            AppendReuseAndVolume(markdown, report);
            AppendInterpretationLimits(markdown, report);
            return;
        }

        ChangePortfolioComparisonSeries portfolio = report.Series.Single(
            series => series.Kind == ChangePortfolioSeriesKind.Portfolio);
        AppendCoverage(markdown, report);
        AppendResultSummary(markdown, portfolio);
        ChangePortfolioComparisonSeries[] contributors =
            [.. report.Series.Where(series =>
                series.Kind is ChangePortfolioSeriesKind.ContributorExclusive or
                    ChangePortfolioSeriesKind.ContributorIsolated)];
        AppendChart(markdown, report.Buckets, portfolio, contributors);
        AppendSeriesSection(markdown, "Overall portfolio", report.Buckets, portfolio);
        foreach (ChangePortfolioComparisonSeries contributor in contributors)
        {
            string basis = contributor.Kind == ChangePortfolioSeriesKind.ContributorIsolated
                ? "isolated canonical estimate"
                : "jointly normalized exclusive match set";
            AppendSeriesSection(
                markdown,
                "Contributor " + contributor.ContributorIds[0] + " — " + basis,
                report.Buckets,
                contributor);
        }

        AppendContributorMatrix(markdown, report.Buckets, contributors, portfolio);
        AppendSharedGroups(markdown, report);
        AppendAnalyzed(markdown, report);
        AppendCalculation(markdown, report);
        AppendInterpretationLimits(markdown, report);
    }

    private static void AppendCoverage(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonBucket first = report.Buckets[0];
        ChangePortfolioComparisonBucket last = report.Buckets[^1];
        markdown.AppendLine();
        markdown.AppendLine("## Coverage");
        markdown.AppendLine();
        markdown.Append("Interval: `").Append(first.SinceInclusive.ToString("O", CultureInfo.InvariantCulture))
            .Append("` through `").Append(last.UntilExclusive.ToString("O", CultureInfo.InvariantCulture))
            .AppendLine("` (start inclusive, end exclusive)  ");
        markdown.Append("Buckets: ").Append(report.Buckets.Count).Append(" using `")
            .Append(Escape(report.BucketPolicy.Policy)).AppendLine("`  ");
        markdown.Append("Contributor normalization: `")
            .Append(Kebab(report.BucketPolicy.ContributorNormalization)).AppendLine("`  ");
        if (first.PartialStart || last.PartialEnd)
        {
            markdown.Append("Partial-period note: ")
                .Append(first.PartialStart ? "the first bucket starts mid-period" : "the first bucket is complete")
                .Append("; ")
                .Append(last.PartialEnd ? "the final bucket ends mid-period" : "the final bucket is complete")
                .AppendLine(". Capacity values must follow the caller's stated partial-period calendar policy.");
        }

        if (report.BucketPolicy.CapacityCalendarPolicy is not null)
        {
            markdown.Append("Capacity policy: ")
                .AppendLine(Escape(report.BucketPolicy.CapacityCalendarPolicy));
        }
    }

    private static void AppendResultSummary(
        StringBuilder markdown,
        ChangePortfolioComparisonSeries portfolio)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Result summary");
        markdown.AppendLine();
        if (portfolio.Trend is null)
        {
            markdown.AppendLine(
                "No reference capacity was supplied, so the report does not calculate capacity ratios, rolling ratios, slope, or R-squared.");
            return;
        }

        ChangePortfolioTrendStatistics trend = portfolio.Trend;
        markdown.Append("- First expected EHE/capacity ratio (`")
            .Append(Escape(trend.FirstBucketId)).Append("`): **")
            .Append(Ratio(trend.FirstExpectedRatio)).AppendLine("**");
        markdown.Append("- Latest expected EHE/capacity ratio (`")
            .Append(Escape(trend.LatestBucketId)).Append("`): **")
            .Append(Ratio(trend.LatestExpectedRatio)).AppendLine("**");
        markdown.Append("- First-to-latest change: **")
            .Append(Percentage(trend.PercentageChange)).AppendLine("**");
        markdown.Append("- OLS slope per bucket: **")
            .Append(trend.OrdinaryLeastSquaresSlope?.ToString("0.########", CultureInfo.InvariantCulture) ?? "unavailable")
            .AppendLine("**");
        markdown.Append("- OLS R²: **")
            .Append(trend.RSquared?.ToString("0.########", CultureInfo.InvariantCulture) ?? "unavailable")
            .AppendLine("**");
        if (trend.CapacityWeightedRollingWindow.Count > 0)
        {
            markdown.Append("- Three-bucket capacity-weighted expected ratios: ")
                .AppendLine(string.Join(", ", trend.CapacityWeightedRollingWindow.Select(point =>
                    $"`{Escape(point.BucketId)}` {Ratio(point.ExpectedRatio)}")));
        }

        markdown.AppendLine();
        markdown.AppendLine(
            "These statistics describe association across the selected Git/EHE buckets only. They do not establish that AI, contributor skill, staffing, or any other factor caused the observed change.");
    }

    private static void AppendChart(
        StringBuilder markdown,
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets,
        ChangePortfolioComparisonSeries portfolio,
        IReadOnlyList<ChangePortfolioComparisonSeries> contributors)
    {
        bool ratios = portfolio.Points.All(point => point.CapacityRatio is not null);
        ChangePortfolioComparisonSeries[] chartSeries = [portfolio, .. contributors];
        decimal[][] values =
        [
            .. chartSeries.Select(series => ratios
                ? series.Points.Select(point => point.CapacityRatio!.Expected).ToArray()
                : series.Points.Select(point => point.Effort.Expected).ToArray()),
        ];
        decimal maximum = values.Length == 0
            ? 1m
            : Math.Max(1m, values.SelectMany(value => value).DefaultIfEmpty().Max() * 1.1m);
        markdown.AppendLine();
        markdown.AppendLine("## Trend chart");
        markdown.AppendLine();
        markdown.AppendLine("```mermaid");
        markdown.AppendLine("xychart-beta");
        markdown.Append("    title \"")
            .Append(ratios ? "Expected EHE to capacity ratio" : "Expected EHE")
            .AppendLine("\"");
        markdown.Append("    x-axis [")
            .Append(string.Join(", ", buckets.Select(bucket => $"\"{Escape(bucket.Id)}\"")))
            .AppendLine("]");
        markdown.Append("    y-axis \"")
            .Append(ratios ? "EHE / capacity" : "EHE hours")
            .Append("\" 0 --> ").AppendLine(maximum.ToString("0.######", CultureInfo.InvariantCulture));
        foreach (decimal[] seriesValues in values)
        {
            markdown.Append("    line [")
                .Append(string.Join(", ", seriesValues.Select(value =>
                    value.ToString("0.######", CultureInfo.InvariantCulture))))
                .AppendLine("]");
        }

        markdown.AppendLine("```");
        markdown.AppendLine();
        markdown.Append("Series order: ").AppendLine(string.Join(", ", chartSeries.Select(series =>
            $"`{Escape(series.Id)}`")));
        markdown.AppendLine();
        markdown.AppendLine("Numeric fallback:");
        foreach ((ChangePortfolioComparisonSeries series, int index) in chartSeries.Select(
            (series, index) => (series, index)))
        {
            markdown.Append("- `").Append(Escape(series.Id)).Append("`: ")
                .AppendLine(string.Join(", ", buckets.Select((bucket, bucketIndex) =>
                    $"`{Escape(bucket.Id)}`={values[index][bucketIndex].ToString("0.######", CultureInfo.InvariantCulture)}")));
        }
    }

    private static void AppendSeriesSection(
        StringBuilder markdown,
        string heading,
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets,
        ChangePortfolioComparisonSeries series)
    {
        markdown.AppendLine();
        markdown.Append("## ").AppendLine(Escape(heading));
        markdown.AppendLine();
        AppendEffortHeader(markdown);
        for (int index = 0; index < buckets.Count; index++)
        {
            AppendPointRow(markdown, buckets[index], series.Points[index]);
        }

        AppendTotalRow(markdown, series);
        if (series.Trend is not null)
        {
            markdown.AppendLine();
            markdown.Append("First/latest ratio: ").Append(Ratio(series.Trend.FirstExpectedRatio))
                .Append(" → ").Append(Ratio(series.Trend.LatestExpectedRatio))
                .Append("; change: ").Append(Percentage(series.Trend.PercentageChange))
                .Append("; slope: ")
                .Append(series.Trend.OrdinaryLeastSquaresSlope?.ToString("0.########", CultureInfo.InvariantCulture) ?? "unavailable")
                .Append("; R²: ")
                .AppendLine(series.Trend.RSquared?.ToString("0.########", CultureInfo.InvariantCulture) ?? "unavailable");
        }
    }

    private static void AppendContributorMatrix(
        StringBuilder markdown,
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets,
        IReadOnlyList<ChangePortfolioComparisonSeries> contributors,
        ChangePortfolioComparisonSeries portfolio)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Contributor comparison matrix");
        markdown.AppendLine();
        bool isolated = contributors.All(series =>
            series.Kind == ChangePortfolioSeriesKind.ContributorIsolated);
        markdown.AppendLine(isolated
            ? "Values below are membership-stable isolated expected EHE. Shared commits can appear in more than one contributor column, so contributor columns are non-additive and must not be summed into the portfolio."
            : "Values below are jointly normalized expected EHE for each contributor's exclusive exact-match set. Shared-contributor groups are shown separately and are not copied into individual columns.");
        markdown.AppendLine();
        markdown.Append("| Bucket |");
        foreach (ChangePortfolioComparisonSeries contributor in contributors)
        {
            markdown.Append(' ').Append(Escape(contributor.ContributorIds[0])).Append(" |");
        }

        markdown.AppendLine(" Portfolio |");
        markdown.Append("| --- |");
        foreach (ChangePortfolioComparisonSeries _ in contributors)
        {
            markdown.Append(" ---: |");
        }

        markdown.AppendLine(" ---: |");
        for (int index = 0; index < buckets.Count; index++)
        {
            markdown.Append("| ").Append(Escape(buckets[index].Label)).Append(" |");
            foreach (ChangePortfolioComparisonSeries contributor in contributors)
            {
                markdown.Append(' ').Append(Hours(contributor.Points[index].Effort.Expected)).Append(" |");
            }

            markdown.Append(' ').Append(Hours(portfolio.Points[index].Effort.Expected)).AppendLine(" |");
        }
    }

    private static void AppendSharedGroups(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonSeries[] shared =
            [.. report.Series.Where(series =>
                series.Kind == ChangePortfolioSeriesKind.SharedContributors)];
        markdown.AppendLine();
        markdown.AppendLine("## Shared-credit and joint reconciliation");
        markdown.AppendLine();
        markdown.AppendLine(report.BucketPolicy.ContributorNormalization ==
            ChangePortfolioContributorNormalization.Isolated
            ? "The canonical source portfolio still keeps every multi-contributor commit in one exact shared group and counts it once. Isolated contributor series intentionally show the full canonical item for every matching contributor, so those personal comparison series can overlap and are never additive."
            : "A commit matching more than one requested contributor belongs to one exact shared-contributor group. Its EHE is counted once in the portfolio and is not divided into invented personal percentages. Contributor-exclusive values and jointly normalized allocations can therefore be context-dependent when report membership changes.");
        if (report.BucketPolicy.ContributorNormalization ==
            ChangePortfolioContributorNormalization.Isolated)
        {
            ChangePortfolioContributorGroup[] canonicalShared =
            [.. report.SourcePortfolio!.Aggregation!.ContributorGroups.Where(group =>
                group.Kind == ChangePortfolioContributorGroupKind.SharedContributors)];
            if (canonicalShared.Length == 0)
            {
                markdown.AppendLine();
                markdown.AppendLine("No shared-contributor exact-match group was selected.");
                return;
            }

            markdown.AppendLine();
            markdown.AppendLine("| Contributors | Changes | Joint low | Joint expected | Joint high |");
            markdown.AppendLine("| --- | ---: | ---: | ---: | ---: |");
            foreach (ChangePortfolioContributorGroup group in canonicalShared)
            {
                markdown.Append("| ").Append(Escape(string.Join(", ", group.ContributorIds)))
                    .Append(" | ").Append(group.ItemIds.Count)
                    .Append(" | ").Append(Hours(group.NormalizedEffort.Low))
                    .Append(" | ").Append(Hours(group.NormalizedEffort.Expected))
                    .Append(" | ").Append(Hours(group.NormalizedEffort.High)).AppendLine(" |");
            }

            return;
        }

        if (shared.Length == 0)
        {
            markdown.AppendLine();
            markdown.AppendLine("No shared-contributor exact-match group was selected.");
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("| Contributors | Changes | Low | Expected | High |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (ChangePortfolioComparisonSeries group in shared)
        {
            markdown.Append("| ").Append(Escape(string.Join(", ", group.ContributorIds)))
                .Append(" | ").Append(group.Points.Sum(point => point.SelectedChangeCount))
                .Append(" | ").Append(Hours(group.TotalEffort.Low))
                .Append(" | ").Append(Hours(group.TotalEffort.Expected))
                .Append(" | ").Append(Hours(group.TotalEffort.High)).AppendLine(" |");
        }
    }

    private static void AppendAnalyzed(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioAuthorPeriodManifestSelection selection =
            report.Selection.AuthorPeriodManifest!;
        markdown.AppendLine();
        markdown.AppendLine("## What was analyzed");
        markdown.AppendLine();
        markdown.Append("- Repositories: ").AppendLine(selection.Repositories.Count.ToString(CultureInfo.InvariantCulture));
        markdown.Append("- Pinned heads: ").AppendLine(selection.Repositories.Sum(repository => repository.Heads.Count).ToString(CultureInfo.InvariantCulture));
        markdown.Append("- Requested contributors: ").AppendLine(selection.ContributorIds.Count.ToString(CultureInfo.InvariantCulture));
        markdown.Append("- Selected immutable changes after repository-scoped deduplication: ")
            .AppendLine(report.SourcePortfolio!.Items.Count.ToString(CultureInfo.InvariantCulture));
        markdown.Append("- Date field: `").Append(Kebab(selection.DateField)).Append("`; merge policy: `")
            .Append(Kebab(selection.MergePolicy)).Append("`; coauthor policy: `")
            .Append(Kebab(selection.CoauthorPolicy)).AppendLine("`");
        markdown.AppendLine(
            "- Each repository's pinned-head reachability was unioned before identity/time selection; each selected immutable commit is assigned to exactly one bucket.");
    }

    private static void AppendCalculation(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonReuse reuse = report.Execution.Reuse;
        markdown.AppendLine();
        markdown.AppendLine("## EH calculation and validation");
        markdown.AppendLine();
        markdown.Append("- Estimator: `").Append(Escape(report.EstimatorVersion))
            .Append("`; source estimator: `")
            .Append(Escape(report.SourceChangeEstimatorVersion)).AppendLine("`");
        markdown.Append("- Isolated selected-change EHE: ")
            .Append(Hours(report.SourcePortfolio!.IsolatedEffort.Low)).Append(" / ")
            .Append(Hours(report.SourcePortfolio.IsolatedEffort.Expected)).Append(" / ")
            .AppendLine(Hours(report.SourcePortfolio.IsolatedEffort.High));
        markdown.Append("- Jointly repository-normalized EHE: ")
            .Append(Hours(report.SourcePortfolio.TotalEffort.Low)).Append(" / ")
            .Append(Hours(report.SourcePortfolio.TotalEffort.Expected)).Append(" / ")
            .AppendLine(Hours(report.SourcePortfolio.TotalEffort.High));
        markdown.Append("- Internal repository evidence shards: ")
            .Append(report.Execution.RepositoryShardCount).Append(" under `")
            .Append(Escape(report.Execution.ShardPolicy)).AppendLine("`");
        markdown.Append("- Snapshot reuse: ").Append(reuse.SnapshotAnalysisHits).Append('/')
            .Append(reuse.SnapshotAnalysisRequests).Append("; immutable file-artifact reuse: ")
            .Append(reuse.AnalysisArtifactHits).Append('/').Append(reuse.AnalysisArtifactRequests)
            .Append("; inventory reuse: ").Append(reuse.SnapshotInventoryHits).Append('/')
            .Append(reuse.SnapshotInventoryRequests).AppendLine();
        markdown.AppendLine(
            "- Every emitted range was contract-validated as 0 ≤ low ≤ expected ≤ high. Bucket and exact-match-set allocations reconcile exactly to the source portfolio range before presentation rounding.");
    }

    private static void AppendInterpretationLimits(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Interpretation limits");
        markdown.AppendLine();
        markdown.AppendLine("- EHE is replacement effort, not actual elapsed or recorded labor.");
        markdown.AppendLine("- Reference capacity is caller-supplied and is not attendance, a timesheet, or a productivity target.");
        markdown.AppendLine("- Selection is retrospective only for commits reachable from the pinned local heads; excluded or absent non-Git work is not inferred.");
        markdown.AppendLine("- Coverage is limited to repositories explicitly supplied in the manifest and objects present in their pinned local heads; omitted repositories and unavailable work are invisible to this report.");
        markdown.AppendLine("- Partial buckets are labeled explicitly and depend on the caller's capacity calendar policy.");
        markdown.AppendLine("- Identity selects repository-attributed changes; it does not establish sole authorship or personal labor shares.");
        markdown.AppendLine("- The repository and Change EHE models remain experimental and uncalibrated outside their documented admission boundaries.");
        markdown.AppendLine("- Operational timings and working-set samples are observations from this invocation and do not affect EHE or the semantic digest.");
        if (report.Execution.Failures.Count > 0)
        {
            markdown.AppendLine("- This run is incomplete; no complete aggregate or trend should be published.");
        }
    }
}
