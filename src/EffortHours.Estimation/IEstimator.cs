using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

public interface IEstimator
{
    public EstimateReport Estimate(
        RepositoryEvidence evidence,
        EstimationProfile profile,
        RateCard? rateCard = null);
}

/// <summary>
/// Marks an estimator whose <see cref="IEstimator.Estimate"/> implementation can be
/// invoked concurrently with independent immutable evidence inputs.
/// </summary>
public interface IThreadSafeEstimator : IEstimator
{
}
