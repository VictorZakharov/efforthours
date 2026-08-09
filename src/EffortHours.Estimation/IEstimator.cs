using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

public interface IEstimator
{
    public EstimateReport Estimate(
        RepositoryEvidence evidence,
        EstimationProfile profile,
        RateCard? rateCard = null);
}
