using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<PortfolioCandidates> PlanAuthorPeriodManifestAsync(
        ChangePortfolioCommandOptions options,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken)
    {
        GitAuthorPeriodManifestPortfolioPlan plan =
            _planAuthorPeriodManifestWithAcquisition is null
                ? await _planAuthorPeriodManifest(
                    options.AuthorPeriodManifestPath!,
                    executionTelemetry,
                    cancellationToken).ConfigureAwait(false)
                : await _planAuthorPeriodManifestWithAcquisition(
                    options.AuthorPeriodManifestPath!,
                    options.FetchMissing,
                    executionTelemetry,
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
            plan.ExecutionTelemetry,
            estimate.Statistics,
            plan.Manifest);
    }
}
