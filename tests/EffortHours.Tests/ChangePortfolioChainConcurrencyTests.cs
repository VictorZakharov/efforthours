using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioCommandTests
{
    [Fact]
    public async Task ConsecutiveChangesOverlapBoundedAnalysisAndKeepOrderedExactReports()
    {
        SnapshotState[] states = [.. Enumerable.Range(0, 5).Select(index => State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", $"public class Feature {{ public int Value() => {index}; }}")))];
        GitChangePlan[] plans = [.. Enumerable.Range(0, 4).Select(index =>
            CommitPlan($"commit-{index}", states[index], states[index + 1]))];
        using ConcurrentProbeEstimator probe = new();
        ChangePortfolioEstimateBatch overlapped = await new ChangeEstimator(probe)
            .EstimatePortfolioCandidatesWithStatisticsAsync(plans, EstimationProfile.Implementation);
        ChangePortfolioEstimateBatch ordinary = await new ChangeEstimator()
            .EstimatePortfolioCandidatesWithStatisticsAsync(plans, EstimationProfile.Implementation);

        Assert.InRange(probe.MaximumActive, 2, 4);
        Assert.Equal(8, overlapped.Statistics.SnapshotAnalysisRequests);
        Assert.Equal(3, overlapped.Statistics.SnapshotAnalysisHits);
        Assert.Equal(plans.Select(plan => plan.Selection.Head.ObjectId),
            overlapped.Reports.Select(report => report.Selection.Head.ObjectId));
        Assert.Equal(ordinary.Reports.Select(report => ContractJson.Serialize(report)),
            overlapped.Reports.Select(report => ContractJson.Serialize(report)));
    }
}
