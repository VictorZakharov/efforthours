using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

internal static class CalibrationMetricCalculator
{
    public static CalibrationRangeMetrics BuildMetrics(
        IReadOnlyList<CalibrationRangeObservation> observations) =>
        new()
        {
            Low = BuildPointMetrics(observations, range => range.Low),
            Expected = BuildPointMetrics(observations, range => range.Expected),
            High = BuildPointMetrics(observations, range => range.High),
            Interval = BuildIntervalMetrics(observations),
        };

    public static bool ContainsExpected(EffortRange reviewed, EffortRange candidate) =>
        candidate.Low <= reviewed.Expected && reviewed.Expected <= candidate.High;

    public static bool ContainsRange(EffortRange reviewed, EffortRange candidate) =>
        candidate.Low <= reviewed.Low && candidate.High >= reviewed.High;

    private static CalibrationPointMetrics BuildPointMetrics(
        IReadOnlyList<CalibrationRangeObservation> observations,
        Func<EffortRange, decimal> selector)
    {
        if (observations.Count == 0)
        {
            return new CalibrationPointMetrics
            {
                SampleCount = 0,
                ReviewedHours = 0m,
                CandidateHours = 0m,
                MeanAbsoluteErrorHours = 0m,
                MedianAbsoluteErrorHours = 0m,
                RootMeanSquaredErrorHours = 0m,
                MeanSignedErrorHours = 0m,
            };
        }

        decimal[] reviewed = [.. observations.Select(observation => selector(observation.Reviewed))];
        decimal[] candidate = [.. observations.Select(observation => selector(observation.Candidate))];
        decimal[] errors = [.. candidate.Zip(reviewed, (prediction, target) => prediction - target)];
        decimal[] absoluteErrors = [.. errors.Select(decimal.Abs).Order()];
        decimal reviewedHours = reviewed.Sum();
        decimal candidateHours = candidate.Sum();
        decimal absoluteErrorHours = absoluteErrors.Sum();
        decimal squaredErrorMean = errors.Sum(error => error * error) / observations.Count;

        return new CalibrationPointMetrics
        {
            SampleCount = observations.Count,
            ReviewedHours = Round4(reviewedHours),
            CandidateHours = Round4(candidateHours),
            MeanAbsoluteErrorHours = Round4(absoluteErrorHours / observations.Count),
            MedianAbsoluteErrorHours = Round4(Median(absoluteErrors)),
            RootMeanSquaredErrorHours = Round4((decimal)Math.Sqrt((double)squaredErrorMean)),
            MeanSignedErrorHours = Round4(errors.Sum() / observations.Count),
            WeightedAbsolutePercentageError = DivideOrNull(absoluteErrorHours, reviewedHours),
            AggregateBiasRate = DivideOrNull(candidateHours - reviewedHours, reviewedHours),
        };
    }

    private static CalibrationIntervalMetrics BuildIntervalMetrics(
        IReadOnlyList<CalibrationRangeObservation> observations)
    {
        if (observations.Count == 0)
        {
            return new CalibrationIntervalMetrics
            {
                SampleCount = 0,
                ReviewedExpectedCoveredCount = 0,
                ReviewedRangeFullyCoveredCount = 0,
                MeanCandidateWidthHours = 0m,
                MeanReviewedWidthHours = 0m,
            };
        }

        int expectedCovered = observations.Count(observation =>
            ContainsExpected(observation.Reviewed, observation.Candidate));
        int rangeCovered = observations.Count(observation =>
            ContainsRange(observation.Reviewed, observation.Candidate));

        return new CalibrationIntervalMetrics
        {
            SampleCount = observations.Count,
            ReviewedExpectedCoveredCount = expectedCovered,
            ReviewedExpectedCoverage = DivideOrNull(expectedCovered, observations.Count),
            ReviewedRangeFullyCoveredCount = rangeCovered,
            ReviewedRangeFullyCoveredRate = DivideOrNull(rangeCovered, observations.Count),
            MeanCandidateWidthHours = Round4(
                observations.Average(observation =>
                    observation.Candidate.High - observation.Candidate.Low)),
            MeanReviewedWidthHours = Round4(
                observations.Average(observation =>
                    observation.Reviewed.High - observation.Reviewed.Low)),
        };
    }

    private static decimal Median(decimal[] orderedValues)
    {
        int middle = orderedValues.Length / 2;
        return orderedValues.Length % 2 == 0
            ? (orderedValues[middle - 1] + orderedValues[middle]) / 2m
            : orderedValues[middle];
    }

    private static decimal? DivideOrNull(decimal numerator, decimal denominator) =>
        denominator == 0m ? null : Round4(numerator / denominator);

    private static decimal? DivideOrNull(int numerator, int denominator) =>
        denominator == 0 ? null : Round4((decimal)numerator / denominator);

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

internal sealed record CalibrationRangeObservation(EffortRange Reviewed, EffortRange Candidate);
