using System.Diagnostics;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal sealed record ChangeBenchmarkExecution(
    int PlannedComponents,
    int SelectedChanges,
    int ExpectedSnapshotAnalyses,
    bool AuditBounded,
    bool ChangedScopeAnalysis,
    decimal ExpectedEffort,
    ChangePortfolioExecutionStatistics? PortfolioStatistics = null,
    TimeSpan? CombinedElapsed = null,
    TimeSpan? IndependentElapsed = null,
    int? IndependentSnapshotAnalyses = null,
    bool? IndependentReportsEquivalent = null);

internal static class ChangeBenchmarkRunner
{
    public static Task<ChangeBenchmarkExecution> RunAsync(
        ChangeBenchmarkOptions options,
        GitBenchmarkRepository repository,
        CountingEstimator repositoryEstimator,
        CancellationToken cancellationToken) => options.Mode == ChangeBenchmarkMode.AuthorPeriod
            ? RunAuthorPeriodAsync(options, repository, repositoryEstimator, cancellationToken)
            : RunSingleChangeAsync(options, repository, repositoryEstimator, cancellationToken);

    private static async Task<ChangeBenchmarkExecution> RunSingleChangeAsync(
        ChangeBenchmarkOptions options,
        GitBenchmarkRepository repository,
        CountingEstimator repositoryEstimator,
        CancellationToken cancellationToken)
    {
        GitChangePlanner planner = new(
            new GitClient(),
            new UnsupportedPullRequestResolver(),
            new GitChangePlannerOptions
            {
                MaximumRangeComponents = options.MaximumRangeComponents,
            });
        GitChangePlan plan = options.Mode == ChangeBenchmarkMode.LargeTree
            ? await planner.PlanBaseHeadAsync(
                repository.RootPath,
                repository.BaseObjectId,
                repository.HeadObjectId,
                cancellationToken).ConfigureAwait(false)
            : await planner.PlanRangeAsync(
                repository.RootPath,
                $"{repository.BaseObjectId}..{repository.HeadObjectId}",
                cancellationToken).ConfigureAwait(false);
        ChangeEstimateReport report = await new ChangeEstimator(repositoryEstimator).EstimateAsync(
            plan,
            EstimationProfile.Implementation,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        bool auditBounded = plan.Diagnostics.Any(diagnostic => diagnostic.Code == "FB5105");
        return new ChangeBenchmarkExecution(
            plan.Components.Count,
            SelectedChanges: 1,
            ExpectedSnapshotAnalyses: auditBounded ? 2 : options.Commits + 1,
            auditBounded,
            report.Diagnostics.Any(diagnostic => diagnostic.Code == "FB5205"),
            report.TotalEffort.Expected);
    }

    private static async Task<ChangeBenchmarkExecution> RunAuthorPeriodAsync(
        ChangeBenchmarkOptions options,
        GitBenchmarkRepository repository,
        CountingEstimator repositoryEstimator,
        CancellationToken cancellationToken)
    {
        GitAuthorPeriodPortfolioPlan plan = await new GitPortfolioPlanner().PlanAuthorPeriodAsync(
            repository.RootPath,
            new GitAuthorPeriodPortfolioOptions
            {
                Aliases = [GitBenchmarkRepository.SelectedAuthorEmail],
                SinceInclusive = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UntilExclusive = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                HeadRevision = repository.HeadObjectId,
            },
            cancellationToken).ConfigureAwait(false);
        ChangeEstimator estimator = new(repositoryEstimator);
        Stopwatch combinedTimer = Stopwatch.StartNew();
        ChangePortfolioEstimateBatch estimate =
            await estimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                [.. plan.Items.Select(item => item.Plan)],
                EstimationProfile.Implementation,
                cancellationToken).ConfigureAwait(false);
        combinedTimer.Stop();
        IReadOnlyList<ChangeEstimateReport> reports = estimate.Reports;
        TimeSpan? independentElapsed = null;
        int? independentSnapshotAnalyses = null;
        bool? independentReportsEquivalent = null;
        if (options.CompareIndependent)
        {
            CountingEstimator independentEstimator = new();
            List<ChangeEstimateReport> independentReports = [];
            List<GitChangePlan> independentPlans = [];
            foreach (GitAuthorPeriodPortfolioItem item in plan.Items)
            {
                independentPlans.Add(await new GitChangePlanner().PlanCommitAsync(
                    repository.RootPath,
                    item.Plan.Selection.Head.ObjectId,
                    parentRevision: null,
                    cancellationToken).ConfigureAwait(false));
            }

            Stopwatch independentTimer = Stopwatch.StartNew();
            foreach (GitChangePlan independentPlan in independentPlans)
            {
                independentReports.Add(await new ChangeEstimator(independentEstimator).EstimateAsync(
                    independentPlan,
                    EstimationProfile.Implementation,
                    cancellationToken: cancellationToken).ConfigureAwait(false));
            }

            independentTimer.Stop();
            independentElapsed = independentTimer.Elapsed;
            independentSnapshotAnalyses = independentEstimator.InvocationCount;
            independentReportsEquivalent = reports.Select(ContractJson.Serialize)
                .SequenceEqual(independentReports.Select(ContractJson.Serialize), StringComparer.Ordinal);
        }
        ChangePortfolioCandidate[] candidates = [.. plan.Items.Select((item, index) =>
            new ChangePortfolioCandidate
            {
                RepositoryId = plan.RepositoryId,
                SelectorId = item.SelectorId,
                Report = reports[index],
                Attribution = item.Attribution,
            })];
        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            plan.Selection,
            candidates,
            EstimationProfile.Implementation,
            planningDiagnostics: plan.Diagnostics);
        return new ChangeBenchmarkExecution(
            PlannedComponents: plan.Items.Sum(item => item.Plan.Components.Count),
            SelectedChanges: plan.Items.Count,
            ExpectedSnapshotAnalyses: options.Commits + 1,
            AuditBounded: false,
            ChangedScopeAnalysis: reports.Any(report =>
                report.Diagnostics.Any(diagnostic => diagnostic.Code == "FB5205")),
            report.TotalEffort.Expected,
            estimate.Statistics,
            combinedTimer.Elapsed,
            independentElapsed,
            independentSnapshotAnalyses,
            independentReportsEquivalent);
    }

    private sealed class UnsupportedPullRequestResolver : IPullRequestResolver
    {
        public Task<ResolvedPullRequest> ResolveAsync(
            string repositoryPath,
            string input,
            string? repository,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The Change benchmark does not resolve pull requests.");
    }
}
