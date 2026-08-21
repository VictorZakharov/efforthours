using System.Globalization;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static partial class ChangePortfolioComparisonMarkdownRenderer
{
    private static void AppendFindings(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        AppendHeader(markdown, report);
        AppendFindingsStatus(markdown, report);
        AppendEnvironment(markdown, report);
        AppendRepositoryOutcomes(markdown, report);
        AppendFailures(markdown, report);
        AppendPhaseAndProgress(markdown, report);
        AppendReuseAndVolume(markdown, report);
        AppendSuccessfulBaseline(markdown, report);
        AppendSanitizedReproduction(markdown, report);
        AppendConfirmedFacts(markdown, report);
        AppendFindingsDataHandling(markdown, report);
    }

    private static void AppendFindingsStatus(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonSeries? portfolio = report.Series.SingleOrDefault(
            series => series.Kind == ChangePortfolioSeriesKind.Portfolio);
        markdown.AppendLine();
        markdown.AppendLine("## Summary");
        markdown.AppendLine();
        markdown.Append("This invocation finished with status **")
            .Append(Kebab(report.Status)).Append("** across ")
            .Append(report.Execution.RepositoryShardCount).Append(" repository evidence shard(s), ")
            .Append(report.Buckets.Count).Append(" bucket(s), and ")
            .Append(report.Selection.AuthorPeriodManifest!.ContributorIds.Count)
            .Append(" contributor(s). It selected ")
            .Append(report.SourcePortfolio?.Items.Count ??
                report.Execution.Repositories.Sum(repository => repository.SelectedChangeCount))
            .AppendLine(" immutable change(s) in completed repository evidence.");
        if (portfolio is not null)
        {
            markdown.AppendLine();
            markdown.Append("Portfolio EHE (low / expected / high): **")
                .Append(Hours(portfolio.TotalEffort.Low)).Append(" / ")
                .Append(Hours(portfolio.TotalEffort.Expected)).Append(" / ")
                .Append(Hours(portfolio.TotalEffort.High)).AppendLine(" hours**.");
        }
        if (report.Execution.Failures.Count == 0)
        {
            markdown.AppendLine("No execution failure was recorded in this invocation.");
        }
        else
        {
            markdown.Append(report.Execution.Failures.Count)
                .AppendLine(" execution failure(s) were recorded; complete aggregates must not be published.");
        }
    }

    private static void AppendEnvironment(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonExecution execution = report.Execution;
        markdown.AppendLine();
        markdown.AppendLine("## Version and environment boundary");
        markdown.AppendLine();
        markdown.Append("- CLI: `").Append(Escape(report.CliVersion)).AppendLine("`");
        markdown.Append("- Portfolio estimator: `")
            .Append(Escape(report.EstimatorVersion)).AppendLine("`");
        markdown.Append("- Source Change estimator: `")
            .Append(Escape(report.SourceChangeEstimatorVersion)).AppendLine("`");
        markdown.Append("- Profile: `").Append(Kebab(report.Profile)).AppendLine("`");
        markdown.Append("- Runtime: `").Append(Escape(execution.RuntimeFramework)).AppendLine("`");
        markdown.Append("- OS family / process architecture: `")
            .Append(Escape(execution.OperatingSystemFamily)).Append("` / `")
            .Append(Escape(execution.ProcessArchitecture)).AppendLine("`");
        markdown.Append("- Logical processors visible to the process: ")
            .AppendLine(execution.LogicalProcessorCount.ToString(CultureInfo.InvariantCulture));
        markdown.AppendLine("- Model boundary: experimental and uncalibrated outside documented admission limits.");
    }

    private static void AppendRepositoryOutcomes(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Repository outcomes");
        markdown.AppendLine();
        markdown.AppendLine("| Repository ID | Status | Checkpoint | Selected changes | Wall time | Last phase | Last progress (UTC) | Input digest |");
        markdown.AppendLine("| --- | --- | --- | ---: | ---: | --- | --- | --- |");
        foreach (ChangePortfolioComparisonRepositoryExecution repository in
            report.Execution.Repositories)
        {
            markdown.Append("| ").Append(Escape(repository.RepositoryId)).Append(" | ")
                .Append(Kebab(repository.Status)).Append(" | ")
                .Append(Kebab(repository.CheckpointDisposition)).Append(" | ")
                .Append(repository.SelectedChangeCount).Append(" | ")
                .Append(repository.ElapsedMilliseconds.ToString("#,0.000", CultureInfo.InvariantCulture))
                .Append(" ms | `")
                .Append(Escape(repository.LastProgress?.Phase ?? "none")).Append("` | ")
                .Append(repository.LastProgress?.ObservedAt.ToString("O", CultureInfo.InvariantCulture) ?? "not-recorded")
                .Append(" | `")
                .Append(Escape(repository.InputDigest)).AppendLine("` |");
        }

        markdown.AppendLine();
        markdown.Append("Internal sharding policy: `")
            .Append(Escape(report.Execution.ShardPolicy))
            .AppendLine("`. Shards remain one jointly reconciled portfolio; callers do not join reports or rounded totals.");
        ChangePortfolioComparisonCheckpoint checkpoint = report.Execution.Checkpoint;
        markdown.Append("Checkpoint protocol: `").Append(Escape(checkpoint.Protocol))
            .Append("`; enabled: ").Append(checkpoint.Enabled ? "yes" : "no")
            .Append("; hits/misses/writes/failures: ").Append(checkpoint.HitCount).Append('/')
            .Append(checkpoint.MissCount).Append('/').Append(checkpoint.WriteCount).Append('/')
            .Append(checkpoint.FailureCount).AppendLine(".");
    }

    private static void AppendFailures(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Execution failures");
        markdown.AppendLine();
        if (report.Execution.Failures.Count == 0)
        {
            markdown.AppendLine("None recorded.");
            return;
        }

        int index = 0;
        foreach (ChangePortfolioComparisonFailure failure in report.Execution.Failures)
        {
            index++;
            markdown.Append("### ").Append(index).Append(". ")
                .Append(Escape(failure.RepositoryId));
            if (failure.BucketId is not null)
            {
                markdown.Append(" / ").Append(Escape(failure.BucketId));
            }

            markdown.AppendLine();
            markdown.AppendLine();
            markdown.Append("- Phase: `").Append(Escape(failure.Phase)).AppendLine("`");
            markdown.Append("- Category: `").Append(Escape(failure.Category)).AppendLine("`");
            markdown.Append("- Root message: ").AppendLine(Escape(failure.Message));
            markdown.Append("- Message digest: `").Append(Escape(failure.MessageDigest)).AppendLine("`");
            ChangePortfolioComparisonProgress? progress = report.Execution.Repositories
                .Single(repository => repository.RepositoryId == failure.RepositoryId)
                .LastProgress;
            if (progress is not null)
            {
                markdown.Append("- Last progress: `")
                    .Append(progress.ObservedAt.ToString("O", CultureInfo.InvariantCulture))
                    .Append("`, phase `").Append(Escape(progress.Phase)).Append("`, units ")
                    .Append(progress.ProcessedUnits).Append('/').Append(progress.TotalUnits)
                    .Append(", cache ").Append(progress.AnalysisCacheHits).Append('/')
                    .Append(progress.AnalysisCacheRequests).Append(", elapsed ")
                    .Append(progress.ElapsedMilliseconds.ToString("#,0.000", CultureInfo.InvariantCulture))
                    .Append(" ms, working set ").Append(Mebibytes(progress.WorkingSetBytes))
                    .Append(", observed peak ").Append(Mebibytes(progress.PeakWorkingSetBytes))
                    .AppendLine(".");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine(
            "Only evidence reported by the execution contract is stated here. This report does not infer a likely cause from a downstream cancellation, timing correlation, or resource sample.");
    }

    private static void AppendPhaseAndProgress(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Phase timings and last progress");
        markdown.AppendLine();
        if (report.Execution.PhaseTimings.Count == 0)
        {
            markdown.AppendLine("No phase timing was retained.");
        }
        else
        {
            markdown.AppendLine("| Phase | Wall time |");
            markdown.AppendLine("| --- | ---: |");
            foreach (ChangePortfolioComparisonPhaseTiming timing in report.Execution.PhaseTimings)
            {
                markdown.Append("| ").Append(Escape(timing.Phase)).Append(" | ")
                    .Append(timing.ElapsedMilliseconds.ToString("#,0.000", CultureInfo.InvariantCulture))
                    .AppendLine(" ms |");
            }
        }

        ChangePortfolioComparisonProgress? progress = report.Execution.LastProgress;
        markdown.AppendLine();
        if (progress is null)
        {
            markdown.AppendLine("No progress sample was retained.");
            return;
        }

        markdown.Append("Last progress at `")
            .Append(progress.ObservedAt.ToString("O", CultureInfo.InvariantCulture))
            .Append("`: phase `").Append(Escape(progress.Phase)).Append("`, ")
            .Append(progress.ProcessedUnits).Append('/').Append(progress.TotalUnits)
            .Append(" units, cache ").Append(progress.AnalysisCacheHits).Append('/')
            .Append(progress.AnalysisCacheRequests).Append(", elapsed ")
            .Append(progress.ElapsedMilliseconds.ToString("#,0.000", CultureInfo.InvariantCulture))
            .AppendLine(" ms.");
        markdown.Append("Working set at sample: ").Append(Mebibytes(progress.WorkingSetBytes))
            .Append("; observed invocation peak: ")
            .Append(Mebibytes(progress.PeakWorkingSetBytes)).AppendLine(".");
        AppendRepositoryPhaseTimings(markdown, report.Execution.Repositories);
    }

    private static void AppendRepositoryPhaseTimings(
        StringBuilder markdown,
        IReadOnlyList<ChangePortfolioComparisonRepositoryExecution> repositories)
    {
        ChangePortfolioComparisonRepositoryExecution[] measured =
            [.. repositories.Where(repository => repository.PhaseTimings.Count > 0)];
        if (measured.Length == 0)
        {
            return;
        }

        markdown.AppendLine();
        markdown.AppendLine("### Per-repository phase timings");
        markdown.AppendLine();
        markdown.AppendLine("| Repository ID | Phase | Wall time |");
        markdown.AppendLine("| --- | --- | ---: |");
        foreach (ChangePortfolioComparisonRepositoryExecution repository in measured)
        {
            foreach (ChangePortfolioComparisonPhaseTiming timing in repository.PhaseTimings)
            {
                markdown.Append("| ").Append(Escape(repository.RepositoryId)).Append(" | ")
                    .Append(Escape(timing.Phase)).Append(" | ")
                    .Append(timing.ElapsedMilliseconds.ToString("#,0.000", CultureInfo.InvariantCulture))
                    .AppendLine(" ms |");
            }
        }
    }

    private static void AppendReuseAndVolume(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        ChangePortfolioComparisonReuse reuse = report.Execution.Reuse;
        markdown.AppendLine();
        markdown.AppendLine("## Reuse and processing volume");
        markdown.AppendLine();
        markdown.AppendLine("| Counter | Requests | Hits | Unique objects/keys |");
        markdown.AppendLine("| --- | ---: | ---: | ---: |");
        AppendReuseRow(markdown, "Snapshot analysis", reuse.SnapshotAnalysisRequests, reuse.SnapshotAnalysisHits, reuse.UniqueSnapshotAnalysisKeys);
        AppendReuseRow(markdown, "Immutable file-analysis artifacts", reuse.AnalysisArtifactRequests, reuse.AnalysisArtifactHits, reuse.UniqueAnalysisArtifactKeys);
        AppendReuseRow(markdown, "Immutable inventories", reuse.SnapshotInventoryRequests, reuse.SnapshotInventoryHits, reuse.UniqueSnapshotInventoryObjects);
        AppendReuseRow(markdown, "Git blobs", reuse.BlobRequests, reuse.BlobCacheHits, reuse.UniqueBlobObjects);
        markdown.AppendLine();
        markdown.Append("Bytes read from Git: ")
            .Append(reuse.BlobReadBytes.ToString("#,0", CultureInfo.InvariantCulture))
            .Append(" bytes; observed peak working set: ")
            .Append(Mebibytes(reuse.PeakWorkingSetBytes)).AppendLine(".");
    }

    private static void AppendReuseRow(
        StringBuilder markdown,
        string name,
        long requests,
        long hits,
        long unique)
    {
        markdown.Append("| ").Append(Escape(name)).Append(" | ").Append(requests)
            .Append(" | ").Append(hits).Append(" | ").Append(unique).AppendLine(" |");
    }

    private static void AppendSuccessfulBaseline(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Successful-run baseline");
        markdown.AppendLine();
        if (report.Status != ChangePortfolioComparisonStatus.Complete)
        {
            markdown.AppendLine("This invocation is incomplete and is not presented as a successful baseline.");
            return;
        }

        decimal[] repositoryTimes = [.. report.Execution.Repositories
            .Where(repository => repository.Status != ChangePortfolioRepositoryExecutionStatus.Failed)
            .Select(repository => repository.ElapsedMilliseconds)
            .Order()];
        decimal combined = repositoryTimes.Sum();
        decimal mean = repositoryTimes.Length == 0 ? 0m : combined / repositoryTimes.Length;
        decimal median = repositoryTimes.Length == 0
            ? 0m
            : repositoryTimes.Length % 2 == 1
                ? repositoryTimes[repositoryTimes.Length / 2]
                : (repositoryTimes[(repositoryTimes.Length / 2) - 1] +
                    repositoryTimes[repositoryTimes.Length / 2]) / 2m;
        decimal maximum = repositoryTimes.Length == 0 ? 0m : repositoryTimes[^1];
        markdown.Append("- Successful/reused repository shards: ")
            .Append(repositoryTimes.Length).AppendLine(".");
        markdown.Append("- Repository wall time median / mean / maximum: ")
            .Append(median.ToString("#,0.000", CultureInfo.InvariantCulture)).Append(" / ")
            .Append(mean.ToString("#,0.000", CultureInfo.InvariantCulture)).Append(" / ")
            .Append(maximum.ToString("#,0.000", CultureInfo.InvariantCulture)).AppendLine(" ms.");
        markdown.Append("- Combined repository wall time: ")
            .Append(combined.ToString("#,0.000", CultureInfo.InvariantCulture)).AppendLine(" ms.");
        markdown.Append("- Selected changes: ").Append(report.SourcePortfolio!.Items.Count)
            .Append("; repositories: ").Append(report.Execution.RepositoryShardCount)
            .Append("; buckets: ").Append(report.Buckets.Count).AppendLine(".");
        markdown.AppendLine(
            "Phase measurements may overlap and must not be summed to reconstruct process wall time. They are an operational comparison baseline, not part of semantic output.");
    }

    private static void AppendSanitizedReproduction(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        string bucketOption = report.BucketPolicy.Kind == ChangePortfolioBucketPolicyKind.Custom
            ? "--bucket-manifest <bucket-manifest.json>"
            : "--bucket " + (report.BucketPolicy.Kind == ChangePortfolioBucketPolicyKind.CalendarMonth
                ? "calendar-month"
                : "calendar-week");
        markdown.AppendLine();
        markdown.AppendLine("## Sanitized reproduction shape");
        markdown.AppendLine();
        markdown.AppendLine("```text");
        markdown.AppendLine("eh change portfolio --author-period-manifest <manifest.json> \\");
        markdown.Append("  ").Append(bucketOption).AppendLine(" \\");
        if (report.BucketPolicy.CapacityInputDigest is not null)
        {
            markdown.AppendLine("  --capacity-manifest <capacity.json> \\");
        }

        markdown.Append("  --normalization ")
            .Append(Kebab(report.BucketPolicy.ContributorNormalization)).AppendLine(" \\");

        if (report.Execution.Checkpoint.Enabled)
        {
            markdown.AppendLine("  --checkpoint <checkpoint-directory> \\");
        }

        markdown.Append("  --profile ").Append(Kebab(report.Profile)).AppendLine(" \\");
        markdown.AppendLine("  --generated-at <generation-instant> \\");
        markdown.AppendLine("  --report-view findings --format markdown \\");
        markdown.AppendLine("  --output <findings.md>");
        markdown.AppendLine("```");
        markdown.AppendLine();
        markdown.AppendLine(
            "The placeholders are deliberate: execution-only repository paths and raw identity aliases are not copied into this report.");
    }

    private static void AppendConfirmedFacts(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Confirmed facts and invariants");
        markdown.AppendLine();
        markdown.AppendLine("- Completed repository evidence was analyzed offline without executing target source code or fetching network data.");
        markdown.AppendLine("- Within each completed repository, head reachability was unioned before selection; duplicate reachability does not multiply EHE.");
        markdown.AppendLine("- Coverage is limited to repositories explicitly supplied in the manifest and objects reachable from their pinned local heads; omitted repositories and unavailable work are invisible.");
        markdown.AppendLine("- Shared-contributor groups are counted once and are not divided into invented personal percentages.");
        markdown.AppendLine(report.BucketPolicy.ContributorNormalization ==
            ChangePortfolioContributorNormalization.Isolated
            ? "- Contributor series use canonical isolated item ranges, remain stable when unrelated contributors are added, and are explicitly non-additive; the portfolio remains jointly normalized."
            : "- Contributor series use additive jointly normalized exact-match-set allocations and can change when manifest membership changes.");
        markdown.AppendLine(report.Verification.CompleteAggregates
            ? "- Each selected change belongs to exactly one time bucket and exact contributor match set; bucket allocations sum exactly to the jointly reconciled source portfolio total."
            : "- No bucket allocation, aggregate EHE, or trend is published because at least one repository shard is incomplete.");
        markdown.Append("- Complete aggregate contract: `")
            .Append(report.Verification.CompleteAggregates ? "satisfied" : "not-satisfied")
            .AppendLine("`.");
    }

    private static void AppendFindingsDataHandling(
        StringBuilder markdown,
        ChangePortfolioComparisonReport report)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Data-handling notes");
        markdown.AppendLine();
        markdown.AppendLine("- No repository source was executed and no implicit network fetch occurred.");
        markdown.AppendLine("- Input heads were requested as immutable object IDs; completed shards used local objects and target worktrees were not modified by estimation.");
        markdown.AppendLine("- Execution-only repository paths and raw contributor aliases are excluded from serialized output.");
        markdown.AppendLine("- Source excerpts, commit subjects, and business-domain details are not included.");
        markdown.AppendLine("- Repository, contributor, bucket, and head IDs appear only because the caller admitted them as public report identifiers.");
        markdown.Append("- Input lineage: source `")
            .Append(Escape(report.Verification.SourcePortfolioDigest ?? "unavailable-incomplete"))
            .Append("`; buckets `").Append(Escape(report.BucketPolicy.InputDigest)).Append('`');
        if (report.BucketPolicy.CapacityInputDigest is not null)
        {
            markdown.Append("; capacity `").Append(Escape(report.BucketPolicy.CapacityInputDigest)).Append('`');
        }

        markdown.AppendLine(".");
    }
}
