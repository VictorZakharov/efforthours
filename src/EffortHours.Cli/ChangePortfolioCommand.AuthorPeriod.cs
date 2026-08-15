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
        GitAuthorPeriodPortfolioPlan plan = await _planAuthorPeriod(
            options.RepositoryPath!,
            new GitAuthorPeriodPortfolioOptions
            {
                Aliases = options.AuthorAliases,
                SinceInclusive = options.SinceInclusive!.Value,
                UntilExclusive = options.UntilExclusive!.Value,
                TimeZone = options.TimeZone,
                DateField = options.DateField,
                MergePolicy = options.MergePolicy,
                CoauthorPolicy = options.CoauthorPolicy,
                HeadRevision = options.HeadRevision,
            },
            executionTelemetry,
            cancellationToken).ConfigureAwait(false);
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
