using EffortHours.Contracts.V1;

namespace EffortHours.Review;

internal static class HostReviewComparisonMetrics
{
    public static HostReviewLevelComparison Build(
        HostReviewComparisonLevel level,
        IReadOnlyList<HostReviewComparisonObservation> observations)
    {
        HostReviewRangeAgreementMetrics baseline = BuildAgreement(
            observations,
            observation => observation.Baseline);
        HostReviewRangeAgreementMetrics compact = BuildAgreement(
            observations,
            observation => observation.Compact);
        decimal baselineCorrection = observations.Sum(observation =>
            decimal.Abs(observation.Compact.Expected - observation.Baseline.Expected));
        decimal referenceCorrection = observations.Sum(observation =>
            decimal.Abs(observation.Reference.Expected - observation.Baseline.Expected));
        decimal baselineError = baseline.Expected.AbsoluteErrorHours;
        decimal compactError = compact.Expected.AbsoluteErrorHours;
        decimal reduction = baselineError - compactError;

        return new HostReviewLevelComparison
        {
            Level = level,
            BaselineAgreement = baseline,
            CompactAgreement = compact,
            BaselineToCompactAbsoluteExpectedCorrectionHours = Round4(baselineCorrection),
            BaselineToReferenceAbsoluteExpectedCorrectionHours = Round4(referenceCorrection),
            ExpectedAbsoluteErrorReductionHours = Round4(reduction),
            ExpectedAbsoluteErrorReductionRate = baselineError == 0m
                ? null
                : Round4(reduction / baselineError),
        };
    }

    private static HostReviewRangeAgreementMetrics BuildAgreement(
        IReadOnlyList<HostReviewComparisonObservation> observations,
        Func<HostReviewComparisonObservation, EffortRange> candidateSelector) => new()
        {
            Low = BuildPoint(observations, candidateSelector, range => range.Low),
            Expected = BuildPoint(observations, candidateSelector, range => range.Expected),
            High = BuildPoint(observations, candidateSelector, range => range.High),
            Interval = BuildInterval(observations, candidateSelector),
        };

    private static HostReviewPointAgreementMetrics BuildPoint(
        IReadOnlyList<HostReviewComparisonObservation> observations,
        Func<HostReviewComparisonObservation, EffortRange> candidateSelector,
        Func<EffortRange, decimal> pointSelector)
    {
        int count = observations.Count;
        decimal referenceHours = observations.Sum(observation =>
            pointSelector(observation.Reference));
        decimal candidateHours = observations.Sum(observation =>
            pointSelector(candidateSelector(observation)));
        decimal absoluteError = observations.Sum(observation => decimal.Abs(
            pointSelector(candidateSelector(observation)) - pointSelector(observation.Reference)));
        decimal signedError = candidateHours - referenceHours;
        return new HostReviewPointAgreementMetrics
        {
            SampleCount = count,
            ReferenceHours = Round4(referenceHours),
            CandidateHours = Round4(candidateHours),
            AbsoluteErrorHours = Round4(absoluteError),
            MeanAbsoluteErrorHours = count == 0 ? 0m : Round4(absoluteError / count),
            SignedErrorHours = Round4(signedError),
            WeightedAbsolutePercentageError = referenceHours == 0m
                ? null
                : Round4(absoluteError / referenceHours),
            AggregateBiasRate = referenceHours == 0m
                ? null
                : Round4(signedError / referenceHours),
        };
    }

    private static HostReviewIntervalAgreementMetrics BuildInterval(
        IReadOnlyList<HostReviewComparisonObservation> observations,
        Func<HostReviewComparisonObservation, EffortRange> candidateSelector)
    {
        int count = observations.Count;
        int expectedCovered = observations.Count(observation =>
        {
            EffortRange candidate = candidateSelector(observation);
            return candidate.Low <= observation.Reference.Expected &&
                observation.Reference.Expected <= candidate.High;
        });
        int rangeCovered = observations.Count(observation =>
        {
            EffortRange candidate = candidateSelector(observation);
            return candidate.Low <= observation.Reference.Low &&
                candidate.High >= observation.Reference.High;
        });
        int overlap = observations.Count(observation =>
        {
            EffortRange candidate = candidateSelector(observation);
            return candidate.Low <= observation.Reference.High &&
                observation.Reference.Low <= candidate.High;
        });

        return new HostReviewIntervalAgreementMetrics
        {
            SampleCount = count,
            ReferenceExpectedCoveredCount = expectedCovered,
            ReferenceExpectedCoverage = DivideOrNull(expectedCovered, count),
            ReferenceRangeFullyCoveredCount = rangeCovered,
            ReferenceRangeFullyCoveredRate = DivideOrNull(rangeCovered, count),
            RangeOverlapCount = overlap,
            RangeOverlapRate = DivideOrNull(overlap, count),
        };
    }

    private static decimal? DivideOrNull(int numerator, int denominator) =>
        denominator == 0 ? null : Round4((decimal)numerator / denominator);

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}

internal sealed record HostReviewComparisonObservation(
    EffortRange Baseline,
    EffortRange Compact,
    EffortRange Reference);
