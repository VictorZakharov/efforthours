using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private const int PortfolioSnapshotCacheEntries = 2;

    public async Task<IReadOnlyList<ChangeEstimateReport>> EstimatePortfolioCandidatesAsync(
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);
        SnapshotAnalysisCache snapshotAnalyses = new(PortfolioSnapshotCacheEntries);
        List<ChangeEstimateReport> reports = [];
        foreach (GitChangePlan plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(plan);
            if (plan.Selection.Kind is not (ChangeSelectionKind.Commit or ChangeSelectionKind.PullRequest))
            {
                throw new ArgumentException(
                    "Portfolio candidates must be immutable commit or pull-request changes.",
                    nameof(plans));
            }

            string cacheNamespace = Path.GetFullPath(plan.RepositoryPath);
            reports.Add(await EstimateCoreAsync(
                CreateInput(plan),
                profile,
                rateCard: null,
                snapshotAnalyses,
                cacheNamespace,
                cancellationToken).ConfigureAwait(false));
        }

        return reports;
    }
}
