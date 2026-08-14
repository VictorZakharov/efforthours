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
            new ChangeAuthorPeriodManifestCommandPlanner(portfolioPlanner).PlanAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod)
        : this(
            changeEstimator,
            planPullRequest,
            planAuthorPeriod,
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
            planPullRequest,
            planAuthorPeriod,
            loadManifest,
            new ChangeAuthorPeriodManifestCommandPlanner(new GitPortfolioPlanner()).PlanAsync)
    {
    }

    internal ChangePortfolioCommand(
        ChangeEstimator changeEstimator,
        Func<string, string, string?, CancellationToken, Task<GitChangePlan>> planPullRequest,
        Func<string, GitAuthorPeriodPortfolioOptions, CancellationToken,
            Task<GitAuthorPeriodPortfolioPlan>> planAuthorPeriod,
        Func<string, CancellationToken,
            Task<IReadOnlyList<ResolvedChangePortfolioManifestItem>>> loadManifest,
        Func<string, CancellationToken, Task<GitAuthorPeriodManifestPortfolioPlan>>
            planAuthorPeriodManifest)
    {
        _changeEstimator = changeEstimator ?? throw new ArgumentNullException(nameof(changeEstimator));
        _planPullRequest = planPullRequest ?? throw new ArgumentNullException(nameof(planPullRequest));
        _planAuthorPeriod = planAuthorPeriod ?? throw new ArgumentNullException(nameof(planAuthorPeriod));
        _loadManifest = loadManifest ?? throw new ArgumentNullException(nameof(loadManifest));
        _planAuthorPeriodManifest = planAuthorPeriodManifest ??
            throw new ArgumentNullException(nameof(planAuthorPeriodManifest));
    }
}
