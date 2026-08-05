using Fairbill.Contracts.V1;

namespace Fairbill.Reporting;

public interface IReportRenderer
{
    public string Render(EstimateReport report);
}
