using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    /// <summary>
    /// Serves exact per-commit hits from <paramref name="resultCache"/> and
    /// analyzes only misses, in their original relative order, through the ordinary
    /// portfolio pipeline. Each fresh report is stored after its batch completes.
    /// Statistics describe only the analyzed misses.
    /// </summary>
    public async Task<ChangePortfolioEstimateBatch> EstimatePortfolioCandidatesWithStatisticsAsync(
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        IChangePortfolioResultCache? resultCache,
        ChangePortfolioResultCacheCounters? counters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(executionTelemetry);
        if (resultCache is null)
        {
            return await EstimatePortfolioCandidatesWithStatisticsCoreAsync(
                plans, profile, executionTelemetry, cancellationToken).ConfigureAwait(false);
        }

        ChangeEstimateReport?[] reports = new ChangeEstimateReport?[plans.Count];
        string[] keys = new string[plans.Count];
        List<int> misses = [];
        try
        {
            for (int index = 0; index < plans.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GitChangePlan plan = ValidatePlan(plans[index], index).Plan;
                keys[index] = ChangePortfolioResultCacheKeys.ForCommit(resultCache, plan, profile);
                ChangePortfolioCachedResult? cached = await resultCache
                    .TryLoadAsync(keys[index], cancellationToken).ConfigureAwait(false);
                counters?.Commit(cached?.Report is not null);
                if (cached?.Report is { } report)
                {
                    reports[index] = report;
                }
                else
                {
                    misses.Add(index);
                }
            }

            ChangePortfolioEstimateBatch analyzed = await EstimatePortfolioCandidatesWithStatisticsCoreAsync(
                [.. misses.Select(index => plans[index])],
                profile,
                executionTelemetry,
                cancellationToken).ConfigureAwait(false);
            for (int miss = 0; miss < misses.Count; miss++)
            {
                int index = misses[miss];
                reports[index] = analyzed.Reports[miss];
                await resultCache.StoreAsync(
                    keys[index],
                    new ChangePortfolioCachedResult { Report = analyzed.Reports[miss] },
                    cancellationToken).ConfigureAwait(false);
            }

            return analyzed with { Reports = [.. reports.Select(report => report!)] };
        }
        finally
        {
            // Sessions owned only by hits were never opened by the pipeline; disposal
            // is idempotent for sessions the pipeline already released.
            foreach (GitSnapshotSession session in plans
                .Select(plan => plan?.SnapshotSession)
                .OfType<GitSnapshotSession>()
                .Distinct())
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
