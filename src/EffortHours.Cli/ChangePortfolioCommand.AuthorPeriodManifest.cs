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
        EngineeringScopeProfile? scope = options.Scope == "engineering"
            ? EngineeringScopeProfile.Load() : null;
        Dictionary<string, ChangePathAdmission>? admissions = scope is null ? null : plan.Manifest.Repositories.ToDictionary(
            repository => repository.Id,
            repository => scope.CreateAdmission(repository.ScopeRepository ?? repository.GitHubRepository ?? repository.Id),
            StringComparer.Ordinal);
        GitChangePlan[] plans = [.. plan.Items.Select(item => item.Plan with
        {
            PathAdmission = admissions?.GetValueOrDefault(item.RepositoryId),
        })];
        ChangePortfolioEstimateBatch estimate =
            await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                plans,
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

        if (scope is null)
        {
            ChangePortfolioPreparedCandidates prepared = await _changeEstimator.PreparePortfolioFinalDeltasAsync(
                plan.Selection, candidates, plans, options.Profile, estimate.Statistics,
                telemetry: executionTelemetry, cancellationToken: cancellationToken).ConfigureAwait(false);
            candidates = [.. prepared.Candidates];
            estimate = estimate with { Statistics = prepared.Statistics };
        }
        else
        {
            List<ChangePortfolioCandidate> preparedCandidates = [];
            Dictionary<string, int[]> indicesByRepository = candidates.Select((candidate, index) =>
                (candidate.RepositoryId, Index: index)).GroupBy(value => value.RepositoryId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(value => value.Index).ToArray(), StringComparer.Ordinal);
            ChangePortfolioExecutionStatistics statistics = estimate.Statistics;
            foreach (ChangeAuthorPeriodManifestRepository repository in plan.Manifest.Repositories)
            {
                if (!indicesByRepository.TryGetValue(repository.Id, out int[]? indices)) continue;
                ChangePortfolioPreparedCandidates prepared = await _changeEstimator.PreparePortfolioFinalDeltasAsync(
                    plan.Selection, [.. indices.Select(index => candidates[index])], [.. indices.Select(index => plans[index])],
                    options.Profile, statistics,
                    pathAdmission: admissions!.GetValueOrDefault(repository.Id),
                    telemetry: executionTelemetry, cancellationToken: cancellationToken).ConfigureAwait(false);
                preparedCandidates.AddRange(prepared.Candidates);
                statistics = prepared.Statistics;
            }
            candidates = preparedCandidates;
            estimate = estimate with { Statistics = statistics };

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
