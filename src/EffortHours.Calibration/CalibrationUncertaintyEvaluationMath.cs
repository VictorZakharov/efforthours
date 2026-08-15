using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyEvaluationMath
{
    public const decimal NormalizationFloorHours = 0.5m;
    public const decimal CoverageTarget = 0.80m;
    public const int MinimumBucketObservationCount = 3;
    public const int MinimumBucketRepositoryCount = 2;

    public static decimal NormalizeResidual(decimal residualHours, decimal expectedHours) =>
        Round6(residualHours / NormalizationDenominator(expectedHours));

    public static decimal NormalizationDenominator(decimal expectedHours) =>
        decimal.Max(NormalizationFloorHours, expectedHours);

    public static string SizeBand(decimal expectedHours) =>
        CalibrationUncertaintyBucketing.SizeBand(expectedHours);

    public static EffortRange PredictRange(decimal expectedHours, decimal normalizedHalfWidth)
    {
        decimal halfWidth = Round6(
            NormalizationDenominator(expectedHours) * normalizedHalfWidth);
        return CalibrationUncertaintyIntervalRules.BuildSymmetric(expectedHours, halfWidth);
    }

    public static bool Covers(EffortRange range, decimal reviewedExpected) =>
        reviewedExpected >= range.Low && reviewedExpected <= range.High;

    public static decimal IntervalMiss(EffortRange range, decimal reviewedExpected) =>
        reviewedExpected < range.Low
            ? range.Low - reviewedExpected
            : reviewedExpected > range.High
                ? reviewedExpected - range.High
                : 0m;

    public static CalibrationUncertaintyIntervalPerformance Performance(
        IReadOnlyList<CalibrationUncertaintyObservation> observations,
        Func<CalibrationUncertaintyObservation, EffortRange> rangeSelector)
    {
        if (observations.Count == 0)
        {
            return new CalibrationUncertaintyIntervalPerformance
            {
                ObservationCount = 0,
                ReviewedExpectedCoveredCount = 0,
                ReviewedExpectedCoverage = null,
                MeanWidthHours = 0m,
                MeanNormalizedWidth = 0m,
                MeanAbsoluteResidualHours = 0m,
                MeanIntervalMissHours = 0m,
            };
        }

        EffortRange[] ranges = [.. observations.Select(rangeSelector)];
        int covered = observations.Select((observation, index) =>
                Covers(ranges[index], observation.ReviewedRange.Expected))
            .Count(value => value);
        return new CalibrationUncertaintyIntervalPerformance
        {
            ObservationCount = observations.Count,
            ReviewedExpectedCoveredCount = covered,
            ReviewedExpectedCoverage = Divide(covered, observations.Count),
            MeanWidthHours = Mean(ranges.Select(range => range.High - range.Low)),
            MeanNormalizedWidth = Mean(observations.Select((observation, index) =>
                (ranges[index].High - ranges[index].Low) /
                NormalizationDenominator(observation.CandidateRange.Expected))),
            MeanAbsoluteResidualHours = Mean(observations.Select(
                observation => observation.AbsoluteResidualHours)),
            MeanIntervalMissHours = Mean(observations.Select((observation, index) =>
                IntervalMiss(ranges[index], observation.ReviewedRange.Expected))),
        };
    }

    public static decimal Quantile80(IEnumerable<decimal> values)
    {
        decimal[] ordered = [.. values.Order()];
        if (ordered.Length == 0)
        {
            throw new InvalidOperationException("Cannot calculate a quantile without observations.");
        }

        int rank = (int)decimal.Ceiling(CoverageTarget * ordered.Length);
        return ordered[Math.Max(0, rank - 1)];
    }

    public static decimal? Spearman(IReadOnlyList<(decimal X, decimal Y)> values)
    {
        if (values.Count < 2)
        {
            return null;
        }

        decimal[] xRanks = Rank([.. values.Select(value => value.X)]);
        decimal[] yRanks = Rank([.. values.Select(value => value.Y)]);
        decimal xMean = xRanks.Average();
        decimal yMean = yRanks.Average();
        decimal covariance = 0m;
        decimal xSquares = 0m;
        decimal ySquares = 0m;
        for (int index = 0; index < values.Count; index++)
        {
            decimal x = xRanks[index] - xMean;
            decimal y = yRanks[index] - yMean;
            covariance += x * y;
            xSquares += x * x;
            ySquares += y * y;
        }

        if (xSquares == 0m || ySquares == 0m)
        {
            return null;
        }

        return Round4(covariance / (SquareRoot(xSquares) * SquareRoot(ySquares)));
    }

    public static CalibrationUncertaintyBucket Bucket(
        CalibrationUncertaintyFeatureValueKind kind,
        decimal value) => CalibrationUncertaintyBucketing.FeatureBucket(kind, value);

    public static decimal Mean(IEnumerable<decimal> values)
    {
        decimal[] materialized = [.. values];
        return materialized.Length == 0 ? 0m : Round4(materialized.Average());
    }

    public static decimal? Difference(decimal? value, decimal? baseline) =>
        value is null || baseline is null ? null : Round4(value.Value - baseline.Value);

    public static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public static decimal Round6(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private static decimal[] Rank(decimal[] values)
    {
        decimal[] ranks = new decimal[values.Length];
        (decimal Value, int Index)[] ordered = [.. values
            .Select((value, index) => (value, index))
            .OrderBy(item => item.value)
            .ThenBy(item => item.index)];
        int start = 0;
        while (start < ordered.Length)
        {
            int end = start + 1;
            while (end < ordered.Length && ordered[end].Value == ordered[start].Value)
            {
                end++;
            }

            decimal averageRank = ((start + 1m) + end) / 2m;
            for (int index = start; index < end; index++)
            {
                ranks[ordered[index].Index] = averageRank;
            }

            start = end;
        }

        return ranks;
    }

    private static decimal SquareRoot(decimal value)
    {
        if (value <= 0m)
        {
            return 0m;
        }

        decimal current = value >= 1m ? value : 1m;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            decimal next = (current + (value / current)) / 2m;
            if (next == current)
            {
                break;
            }

            current = next;
        }

        return current;
    }

    private static decimal? Divide(int numerator, int denominator) =>
        denominator == 0 ? null : Round4((decimal)numerator / denominator);
}
