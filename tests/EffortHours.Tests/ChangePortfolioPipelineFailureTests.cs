using EffortHours.Change;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioCommandTests
{
    [Fact]
    public async Task PortfolioPipelinePreservesTheFirstAnalysisFailure()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        GitChangePlan[] plans =
        [
            .. Enumerable.Range(0, 64).Select(index =>
                CommitPlan($"commit-{index}", before, after)),
        ];
        const string expectedMessage = "Synthetic primary portfolio failure.";

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ChangeEstimator(new FirstCallFailureEstimator(expectedMessage))
                .EstimatePortfolioCandidatesWithStatisticsAsync(
                    plans,
                    EstimationProfile.Implementation));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task PortfolioPipelineCompletesAfterEveryPreparedPlanIsAnalyzed()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        GitChangePlan[] plans =
        [
            .. Enumerable.Range(0, 377).Select(index =>
                CommitPlan($"commit-{index}", before, after)),
        ];
        List<ChangePortfolioProgress> progress = [];
        ChangePortfolioExecutionTelemetry telemetry = new(progressReported: progress.Add);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        ChangePortfolioEstimateBatch batch = await new ChangeEstimator()
            .EstimatePortfolioCandidatesWithStatisticsAsync(
                plans,
                EstimationProfile.Implementation,
                telemetry,
                timeout.Token);

        Assert.Equal(377, batch.Reports.Count);
        Assert.Equal(377, progress[^1].ProcessedUnits);
        Assert.Equal(377, progress[^1].TotalUnits);
    }

    private sealed class FirstCallFailureEstimator(string message) : IThreadSafeEstimator
    {
        private readonly SeedEstimator _inner = new();
        private int _invocations;

        public EstimateReport Estimate(
            RepositoryEvidence evidence,
            EstimationProfile profile,
            RateCard? rateCard = null)
        {
            if (Interlocked.Increment(ref _invocations) == 1)
            {
                throw new InvalidOperationException(message);
            }

            return _inner.Estimate(evidence, profile, rateCard);
        }
    }
}
