using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class CandidateOperationalGateTests
{
    [Fact]
    public void MaterialCategoryGateRejectsBiasDespitePooledWapeImprovement()
    {
        CalibrationCategoryMetrics seed = Category(
            sampleCount: 5,
            reviewedHours: 100m,
            wape: 0.30m,
            bias: 0.05m);
        CalibrationCategoryMetrics candidate = Category(
            sampleCount: 5,
            reviewedHours: 100m,
            wape: 0.20m,
            bias: -0.25m);

        CandidatePreflightGate gate = CandidateOperationalGateAssessment.BuildCategoryGate(
            [seed],
            [candidate]);

        Assert.Equal("failed", gate.Status);
        Assert.False(gate.Passed);
        Assert.Contains("pooled-wape=0.2", gate.Observed, StringComparison.Ordinal);
        Assert.Contains("bias=-0.25", gate.Observed, StringComparison.Ordinal);
        Assert.Contains("violations=1", gate.Observed, StringComparison.Ordinal);
    }

    private static CalibrationCategoryMetrics Category(
        int sampleCount,
        decimal reviewedHours,
        decimal wape,
        decimal bias) => new()
        {
            Category = EffortCategory.SpecificationComprehensionAndDomainLearning,
            Metrics = new CalibrationRangeMetrics
            {
                Low = Point(sampleCount, reviewedHours, wape, bias),
                Expected = Point(sampleCount, reviewedHours, wape, bias),
                High = Point(sampleCount, reviewedHours, wape, bias),
                Interval = new CalibrationIntervalMetrics
                {
                    SampleCount = sampleCount,
                    ReviewedExpectedCoveredCount = 0,
                    ReviewedExpectedCoverage = 0m,
                    ReviewedRangeFullyCoveredCount = 0,
                    ReviewedRangeFullyCoveredRate = 0m,
                    MeanCandidateWidthHours = 0m,
                    MeanReviewedWidthHours = 0m,
                },
            },
        };

    private static CalibrationPointMetrics Point(
        int sampleCount,
        decimal reviewedHours,
        decimal wape,
        decimal bias) => new()
        {
            SampleCount = sampleCount,
            ReviewedHours = reviewedHours,
            CandidateHours = reviewedHours * (1m + bias),
            MeanAbsoluteErrorHours = wape * reviewedHours / sampleCount,
            MedianAbsoluteErrorHours = 0m,
            RootMeanSquaredErrorHours = 0m,
            MeanSignedErrorHours = bias * reviewedHours / sampleCount,
            WeightedAbsolutePercentageError = wape,
            AggregateBiasRate = bias,
        };
}
