using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.ChangeBenchmarks;

internal sealed class CountingEstimator : IEstimator
{
    private readonly SeedEstimator _inner = new();

    public int InvocationCount { get; private set; }

    public EstimateReport Estimate(
        RepositoryEvidence evidence,
        EstimationProfile profile,
        RateCard? rateCard = null)
    {
        InvocationCount++;
        return _inner.Estimate(evidence, profile, rateCard);
    }
}
