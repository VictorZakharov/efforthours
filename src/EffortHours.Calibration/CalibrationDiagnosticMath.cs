using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationDiagnosticMath
{
    public static EffortRange ZeroRange { get; } = new()
    {
        Low = 0m,
        Expected = 0m,
        High = 0m,
    };

    public static CalibrationRangeDiagnostic Describe(EffortRange reviewed, EffortRange candidate)
    {
        decimal lowerDistance = candidate.Expected - candidate.Low;
        decimal upperDistance = candidate.High - candidate.Expected;
        decimal candidateWidth = candidate.High - candidate.Low;
        decimal asymmetry = decimal.Abs(upperDistance - lowerDistance);
        CalibrationIntervalStatus status = reviewed.Expected < candidate.Low
            ? CalibrationIntervalStatus.CandidateTooHigh
            : reviewed.Expected > candidate.High
                ? CalibrationIntervalStatus.CandidateTooLow
                : CalibrationIntervalStatus.Covered;
        decimal miss = status switch
        {
            CalibrationIntervalStatus.CandidateTooHigh => candidate.Low - reviewed.Expected,
            CalibrationIntervalStatus.CandidateTooLow => reviewed.Expected - candidate.High,
            _ => 0m,
        };

        return new CalibrationRangeDiagnostic
        {
            Reviewed = reviewed,
            Candidate = candidate,
            SignedError = Difference(candidate, reviewed),
            ExpectedAbsoluteErrorHours = decimal.Abs(candidate.Expected - reviewed.Expected),
            ReviewedExpectedStatus = status,
            IntervalMissHours = miss,
            CandidateWidthHours = candidateWidth,
            ReviewedWidthHours = reviewed.High - reviewed.Low,
            CandidateLowerDistanceHours = lowerDistance,
            CandidateUpperDistanceHours = upperDistance,
            CandidateAbsoluteAsymmetryHours = asymmetry,
            CandidateRelativeAsymmetry = candidateWidth == 0m
                ? 0m
                : Round4(asymmetry / candidateWidth),
            CandidateSymmetric = lowerDistance == upperDistance,
        };
    }

    public static CalibrationSignedEffortRange Difference(EffortRange left, EffortRange right) => new()
    {
        Low = left.Low - right.Low,
        Expected = left.Expected - right.Expected,
        High = left.High - right.High,
    };

    public static CalibrationSignedEffortRange Difference(
        CalibrationSignedEffortRange left,
        CalibrationSignedEffortRange right) => new()
        {
            Low = left.Low - right.Low,
            Expected = left.Expected - right.Expected,
            High = left.High - right.High,
        };

    public static CalibrationSignedEffortRange Sum(
        IEnumerable<CalibrationSignedEffortRange> ranges)
    {
        decimal low = 0m;
        decimal expected = 0m;
        decimal high = 0m;
        foreach (CalibrationSignedEffortRange range in ranges)
        {
            low += range.Low;
            expected += range.Expected;
            high += range.High;
        }

        return new CalibrationSignedEffortRange
        {
            Low = low,
            Expected = expected,
            High = high,
        };
    }

    public static CalibrationSignedEffortRange Round4(CalibrationSignedEffortRange value) => new()
    {
        Low = Round4(value.Low),
        Expected = Round4(value.Expected),
        High = Round4(value.High),
    };

    public static EffortRange Sum(IEnumerable<EffortRange> ranges) => ContractValidation.Sum(ranges);

    public static decimal? Correlation(IReadOnlyList<(decimal Left, decimal Right)> observations)
    {
        if (observations.Count < 2)
        {
            return null;
        }

        double leftMean = observations.Average(item => (double)item.Left);
        double rightMean = observations.Average(item => (double)item.Right);
        double covariance = 0d;
        double leftSquares = 0d;
        double rightSquares = 0d;
        foreach ((decimal left, decimal right) in observations)
        {
            double leftDelta = (double)left - leftMean;
            double rightDelta = (double)right - rightMean;
            covariance += leftDelta * rightDelta;
            leftSquares += leftDelta * leftDelta;
            rightSquares += rightDelta * rightDelta;
        }

        if (leftSquares == 0d || rightSquares == 0d)
        {
            return null;
        }

        double value = covariance / Math.Sqrt(leftSquares * rightSquares);
        return Round4((decimal)Math.Clamp(value, -1d, 1d));
    }

    public static decimal Share(decimal value, decimal total) =>
        total == 0m ? 0m : Round4(value / total);

    public static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
