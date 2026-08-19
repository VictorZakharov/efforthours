using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.ChangeBenchmarks;

internal sealed class CountingEstimator : IThreadSafeEstimator
{
    private readonly SeedEstimator _inner = new();
    private int _invocationCount;

    public int InvocationCount => Volatile.Read(ref _invocationCount);

    public EstimateReport Estimate(
        RepositoryEvidence evidence,
        EstimationProfile profile,
        RateCard? rateCard = null)
    {
        Interlocked.Increment(ref _invocationCount);
        return _inner.Estimate(evidence, profile, rateCard);
    }
}
