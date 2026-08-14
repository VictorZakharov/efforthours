using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<PortfolioCandidates> PlanAuthorPeriodManifestAsync(
        ChangePortfolioCommandOptions options,
        CancellationToken cancellationToken)
    {
        GitAuthorPeriodManifestPortfolioPlan plan = await _planAuthorPeriodManifest(
            options.AuthorPeriodManifestPath!,
            cancellationToken).ConfigureAwait(false);
        ChangePortfolioEstimateBatch estimate =
            await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                [.. plan.Items.Select(item => item.Plan)],
                options.Profile,
                plan.ExecutionTelemetry,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChangeEstimateReport> reports = estimate.Reports;
        List<ChangePortfolioCandidate> candidates = [];
        for (int index = 0; index < plan.Items.Count; index++)
        {
            GitAuthorPeriodManifestPortfolioItem item = plan.Items[index];
            candidates.Add(new ChangePortfolioCandidate
            {
                RepositoryId = item.RepositoryId,
                SelectorId = item.SelectorId,
                Report = reports[index],
                Attribution = item.Attribution,
            });
        }

        return new PortfolioCandidates(
            plan.Selection,
            candidates,
            [.. plan.Diagnostics, estimate.Statistics.CreateDiagnostic()],
            plan.ExecutionTelemetry);
    }
}
