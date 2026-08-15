using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintyIntervalRules
{
    public static EffortRange BuildSymmetric(decimal expectedHours, decimal halfWidthHours)
    {
        if (expectedHours < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedHours),
                expectedHours,
                "Expected hours cannot be negative.");
        }

        if (halfWidthHours < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(halfWidthHours),
                halfWidthHours,
                "Interval half-width cannot be negative.");
        }

        return new EffortRange
        {
            Low = decimal.Max(0m, expectedHours - halfWidthHours),
            Expected = expectedHours,
            High = expectedHours + halfWidthHours,
        };
    }

    public static bool IsPolicyCompliant(EffortRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (range.Low < 0m || range.Low > range.Expected || range.Expected > range.High)
        {
            return false;
        }

        decimal halfWidth = range.High - range.Expected;
        return range.Low == decimal.Max(0m, range.Expected - halfWidth);
    }

    public static decimal? CalculateNormalizedWidth(EffortRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (range.Low < 0m || range.Low > range.Expected || range.Expected > range.High)
        {
            throw new ArgumentException("Effort range ordering is invalid.", nameof(range));
        }

        return range.Expected == 0m
            ? range.High == 0m ? 0m : null
            : (range.High - range.Low) / range.Expected;
    }

    public static IReadOnlyList<string> ValidateMonotonicChange(
        CalibrationUncertaintyFeatureDefinition definition,
        decimal referenceValue,
        decimal comparisonValue,
        decimal referenceNormalizedWidth,
        decimal comparisonNormalizedWidth)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (referenceNormalizedWidth < 0m || comparisonNormalizedWidth < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referenceNormalizedWidth),
                "Normalized interval widths cannot be negative.");
        }

        bool weaker = definition.Monotonicity switch
        {
            CalibrationUncertaintyFeatureMonotonicity.HigherMustNotNarrow =>
                comparisonValue > referenceValue,
            CalibrationUncertaintyFeatureMonotonicity.LowerMustNotNarrow =>
                comparisonValue < referenceValue,
            CalibrationUncertaintyFeatureMonotonicity.HigherMustWiden =>
                comparisonValue > referenceValue,
            _ => false,
        };
        if (!weaker)
        {
            return [];
        }

        bool valid = definition.Monotonicity ==
            CalibrationUncertaintyFeatureMonotonicity.HigherMustWiden
                ? comparisonNormalizedWidth > referenceNormalizedWidth
                : comparisonNormalizedWidth >= referenceNormalizedWidth;
        return valid
            ? []
            :
            [
                $"Feature '{definition.Id}' moved toward weaker evidence, but normalized interval " +
                $"width changed from {referenceNormalizedWidth} to {comparisonNormalizedWidth} contrary to " +
                $"'{definition.Monotonicity}'.",
            ];
    }
}
