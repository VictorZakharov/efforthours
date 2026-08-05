using Fairbill.Contracts.V1;

namespace Fairbill.Estimation;

public interface IEstimator
{
    public EstimateReport Estimate(
        RepositoryEvidence evidence,
        EstimationProfile profile,
        RateCard? rateCard = null);
}
