using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public interface IReportRenderer
{
    public string Render(EstimateReport report);
}
