namespace EffortHours.RepositoryCalibration;

internal static class CandidateResourceGateAssessment
{
    public static CandidateShapeSummary Build(
        IReadOnlyList<CandidateMeasurementRun> seedRuns,
        IReadOnlyList<CandidateMeasurementRun> candidateRuns)
    {
        if (seedRuns.Count < 5 || candidateRuns.Count != seedRuns.Count)
        {
            throw new InvalidDataException(
                "Resource measurement requires matching seed/candidate sets with at least five fresh processes.");
        }

        decimal seedMedian = Median(seedRuns.Select(run => run.ElapsedMilliseconds));
        decimal candidateMedian = Median(candidateRuns.Select(run => run.ElapsedMilliseconds));
        decimal medianLimit = Max(seedMedian * 1.15m, seedMedian + 250m);
        decimal seedSlowest = seedRuns.Max(run => run.ElapsedMilliseconds);
        decimal candidateSlowest = candidateRuns.Max(run => run.ElapsedMilliseconds);
        decimal slowestLimit = Max(seedSlowest * 1.25m, seedSlowest + 500m);
        decimal seedPeak = seedRuns.Max(run => run.PeakWorkingSetMib);
        decimal candidatePeak = candidateRuns.Max(run => run.PeakWorkingSetMib);
        decimal peakLimit = Max(seedPeak * 1.15m, seedPeak + 64m);
        return new CandidateShapeSummary
        {
            SeedMedianMilliseconds = Round(seedMedian),
            CandidateMedianMilliseconds = Round(candidateMedian),
            MedianLimitMilliseconds = Round(medianLimit),
            MedianPassed = candidateMedian <= medianLimit,
            SeedSlowestMilliseconds = Round(seedSlowest),
            CandidateSlowestMilliseconds = Round(candidateSlowest),
            SlowestLimitMilliseconds = Round(slowestLimit),
            SlowestPassed = candidateSlowest <= slowestLimit,
            SeedPeakWorkingSetMib = Round(seedPeak),
            CandidatePeakWorkingSetMib = Round(candidatePeak),
            PeakWorkingSetLimitMib = Round(peakLimit),
            PeakWorkingSetPassed = candidatePeak <= peakLimit,
        };
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        decimal[] ordered = [.. values.Order()];
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2m;
    }

    private static decimal Max(decimal first, decimal second) =>
        first >= second ? first : second;

    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
