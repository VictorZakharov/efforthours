using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioPreparedCandidates
{
    public required IReadOnlyList<ChangePortfolioCandidate> Candidates { get; init; }

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }
}

public sealed partial class ChangeEstimator
{
    public async Task<ChangePortfolioPreparedCandidates> PreparePortfolioFinalDeltasAsync(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioCandidate> candidates,
        IReadOnlyList<GitChangePlan> plans,
        EstimationProfile profile,
        ChangePortfolioExecutionStatistics statistics,
        bool independentDays = false,
        ChangePathAdmission? pathAdmission = null,
        ChangePortfolioExecutionTelemetry? telemetry = null,
        CancellationToken cancellationToken = default)
    {
        if (plans.Count != candidates.Count)
            throw new ArgumentException("Final-delta plans must correspond to canonical candidate rows.");
        var repositories = candidates.Select((candidate, index) => (candidate.RepositoryId, Plan: plans[index]))
            .GroupBy(item => item.RepositoryId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Plan, StringComparer.Ordinal);
        ExecutionStatisticsAccumulator additional = new(0);
        GitSnapshotSession? current = null;
        string? currentRepository = null;
        async Task<IChangeSnapshot> OpenAsync(string repository, string objectId, CancellationToken token)
        {
            if (currentRepository != repository)
            {
                if (current is not null)
                {
                    additional.Add(current.GetStatistics());
                    await current.DisposeAsync().ConfigureAwait(false);
                }
                GitChangePlan plan = repositories[repository];
                current = plan.SnapshotSession?.CreateSibling() ?? new GitSnapshotSession(plan.RepositoryPath,
                    async (_, id, cancellation) =>
                    {
                        GitChangePlan match = plans.First(item => item.RepositoryPath == plan.RepositoryPath &&
                            (item.Selection.Base.ObjectId == id || item.Selection.Head.ObjectId == id));
                        return await (match.Selection.Base.ObjectId == id ? match.OpenBaseAsync(cancellation) :
                            match.OpenHeadAsync(cancellation)).ConfigureAwait(false);
                    });
                currentRepository = repository;
            }
            return await current!.OpenSnapshotAsync(objectId, token).ConfigureAwait(false);
        }
        try
        {
            ChangePortfolioExecutionStatistics analysisStatistics = new();
            IReadOnlyList<ChangePortfolioCandidate> prepared = await PreparePortfolioFinalDeltasAsync(
                selection, candidates, profile, OpenAsync, independentDays, pathAdmission, telemetry,
                value => analysisStatistics = value, cancellationToken).ConfigureAwait(false);
            if (current is not null)
                additional.Add(current.GetStatistics());
            return new ChangePortfolioPreparedCandidates
            {
                Candidates = prepared,
                Statistics = ChangePortfolioExecutionStatistics.Combine(statistics,
                    ChangePortfolioExecutionStatistics.Combine(additional.Build(), analysisStatistics)),
            };
        }
        finally
        {
            if (current is not null)
                await current.DisposeAsync().ConfigureAwait(false);
        }
    }
}
