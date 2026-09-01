using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<PortfolioCandidates> PlanAuthorPeriodAsync(
        ChangePortfolioCommandOptions options,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken)
    {
        ManagedRepositoryHead? managedHead = options.RepositoryPath is null
            ? await _managedGitQueries.PrepareHeadAsync(
                options.GitHubRepository!,
                options.HeadRevision,
                options.FetchMissing,
                cancellationToken).ConfigureAwait(false)
            : null;
        GitAuthorPeriodPortfolioPlan plan = await _planAuthorPeriod(
            managedHead?.RepositoryPath ?? options.RepositoryPath!,
            new GitAuthorPeriodPortfolioOptions
            {
                Aliases = options.AuthorAliases,
                SinceInclusive = options.SinceInclusive!.Value,
                UntilExclusive = options.UntilExclusive!.Value,
                TimeZone = options.TimeZone,
                DateField = options.DateField,
                MergePolicy = options.MergePolicy,
                CoauthorPolicy = options.CoauthorPolicy,
                HeadRevision = managedHead?.ObjectId ?? options.HeadRevision,
            },
            executionTelemetry,
            cancellationToken).ConfigureAwait(false);
        if (managedHead is not null)
        {
            plan = plan with
            {
                Selection = plan.Selection with
                {
                    AuthorPeriod = plan.Selection.AuthorPeriod! with
                    {
                        HeadSelector = options.HeadRevision,
                    },
                },
                Diagnostics =
                [
                    .. plan.Diagnostics,
                    new Diagnostic
                    {
                        Code = "FB5108",
                        Severity = DiagnosticSeverity.Information,
                        Message = managedHead.Fetched
                            ? "Checkout-free author-period selection populated the private EffortHours bare cache with the exact provider-resolved reachable head; no checkout or user repository was changed."
                            : managedHead.ProviderResolved
                                ? "Checkout-free author-period selection refreshed the immutable provider head and reused its objects from the private EffortHours bare cache; no checkout or user repository was changed."
                            : "Checkout-free author-period selection reused an immutable reachable head from the private EffortHours bare cache without provider or network access.",
                    },
                ],
            };
        }
        ChangePortfolioEstimateBatch estimate =
            await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                [.. plan.Items.Select(item => item.Plan)],
                options.Profile,
                executionTelemetry,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChangeEstimateReport> reports = estimate.Reports;
        List<ChangePortfolioCandidate> candidates = [];
        for (int index = 0; index < plan.Items.Count; index++)
        {
            GitAuthorPeriodPortfolioItem item = plan.Items[index];
            candidates.Add(new ChangePortfolioCandidate
            {
                RepositoryId = plan.RepositoryId,
                SelectorId = item.SelectorId,
                Report = reports[index],
                Attribution = item.Attribution,
            });
        }

        return new PortfolioCandidates(
            plan.Selection,
            candidates,
            [.. plan.Diagnostics, estimate.Statistics.CreateDiagnostic()],
            executionTelemetry);
    }
}
