using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    public ChangePortfolioCommand()
        : this(new ChangeEstimator(), new GitChangePlanner(), new GitPortfolioPlanner())
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        GitChangePlanner gitPlanner,
        GitPortfolioPlanner portfolioPlanner)
        : this(
            changeEstimator,
            gitPlanner.PlanPullRequestAsync,
            portfolioPlanner.PlanAuthorPeriodAsync,
            ChangePortfolioManifestLoader.LoadAsync,
            new ChangeAuthorPeriodManifestCommandPlanner(portfolioPlanner).PlanAsync,
            new ChangeAuthorPeriodManifestCommandPlanner(portfolioPlanner).MeasureAsync,
            new GitHubAuthorPeriodDiscovery().DiscoverTodayAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod)
        : this(
            changeEstimator,
            (path, pullRequest, repository, _, cancellationToken) =>
                planPullRequest(path, pullRequest, repository, cancellationToken),
            (path, options, _, cancellationToken) =>
                planAuthorPeriod(path, options, cancellationToken),
            ChangePortfolioManifestLoader.LoadAsync,
            new ChangeAuthorPeriodManifestCommandPlanner(new GitPortfolioPlanner()).PlanAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, bool, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod)
        : this(
            changeEstimator,
            planPullRequest,
            (path, options, _, cancellationToken) =>
                planAuthorPeriod(path, options, cancellationToken),
            ChangePortfolioManifestLoader.LoadAsync,
            new ChangeAuthorPeriodManifestCommandPlanner(new GitPortfolioPlanner()).PlanAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod,
        Func<string, CancellationToken,
            Task<IReadOnlyList<ResolvedChangePortfolioManifestItem>>> loadManifest)
        : this(
            changeEstimator,
            (path, pullRequest, repository, _, cancellationToken) =>
                planPullRequest(path, pullRequest, repository, cancellationToken),
            (path, options, _, cancellationToken) =>
                planAuthorPeriod(path, options, cancellationToken),
            loadManifest,
            new ChangeAuthorPeriodManifestCommandPlanner(new GitPortfolioPlanner()).PlanAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, bool, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, ChangePortfolioExecutionTelemetry?, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod,
        Func<string, CancellationToken,
            Task<IReadOnlyList<ResolvedChangePortfolioManifestItem>>> loadManifest,
        Func<string, ChangePortfolioExecutionTelemetry, CancellationToken,
            Task<GitAuthorPeriodManifestPortfolioPlan>>
            planAuthorPeriodManifest,
        Func<string, ChangePortfolioExecutionTelemetry, CancellationToken,
            Task<GitAuthorPeriodManifestScopePlan>>? measureAuthorPeriodManifest = null)
        : this(
            changeEstimator,
            planPullRequest,
            planAuthorPeriod,
            loadManifest,
            planAuthorPeriodManifest,
            measureAuthorPeriodManifest,
            new GitHubAuthorPeriodDiscovery().DiscoverTodayAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, bool, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, ChangePortfolioExecutionTelemetry?, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod,
        Func<string, CancellationToken,
            Task<IReadOnlyList<ResolvedChangePortfolioManifestItem>>> loadManifest,
        Func<string, ChangePortfolioExecutionTelemetry, CancellationToken,
            Task<GitAuthorPeriodManifestPortfolioPlan>> planAuthorPeriodManifest,
        Func<string, ChangePortfolioExecutionTelemetry, CancellationToken,
            Task<GitAuthorPeriodManifestScopePlan>>? measureAuthorPeriodManifest,
        Func<GitHubAuthorPeriodDiscoveryRequest, CancellationToken,
            Task<GitHubAuthorPeriodDiscoveryResult>> discoverToday)
    {
        _changeEstimator = changeEstimator ?? throw new ArgumentNullException(nameof(changeEstimator));
        _planPullRequest = planPullRequest ?? throw new ArgumentNullException(nameof(planPullRequest));
        _planAuthorPeriod = planAuthorPeriod ?? throw new ArgumentNullException(nameof(planAuthorPeriod));
        _loadManifest = loadManifest ?? throw new ArgumentNullException(nameof(loadManifest));
        _planAuthorPeriodManifest = planAuthorPeriodManifest ??
            throw new ArgumentNullException(nameof(planAuthorPeriodManifest));
        _measureAuthorPeriodManifest = measureAuthorPeriodManifest ??
            ((_, _, _) => throw new InvalidOperationException(
                "Author-period manifest preflight was not configured for this command instance."));
        _discoverToday = discoverToday ?? throw new ArgumentNullException(nameof(discoverToday));
    }
}
