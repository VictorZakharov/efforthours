using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintySupportSummaries
{
    public static CalibrationUncertaintySupportSummary Overall(
        IReadOnlyList<CalibrationUncertaintySupportInput> inputs,
        IReadOnlyList<CalibrationUncertaintySupportWorkItem> workItems,
        CalibrationUncertaintySupportDistanceEvaluation distances) => new()
        {
            RepositoryCount = inputs.Select(input => input.Population.RepositoryId)
                .Distinct(StringComparer.Ordinal).Count(),
            FeatureReportCount = inputs.Count,
            WorkItemCount = workItems.Count,
            UniqueProfileCount = distances.UniqueProfileCount,
            ProfileEvaluationCount = distances.ProfileEvaluationCount,
            ProfileComparisonCount = distances.ProfileComparisonCount,
            ExactProfileMatchWorkItemCount = workItems.Count(item =>
                item.OutOfDistribution.ExactProfileTrainingObservationCount > 0),
            InsufficientSupportWorkItemCount = workItems.Count(item => !item.SupportSufficient),
            MeanOutOfDistributionScore = Mean(workItems.Select(
                item => item.OutOfDistribution.Score)),
            P90OutOfDistributionScore = P90(workItems.Select(
                item => item.OutOfDistribution.Score)),
            MaximumOutOfDistributionScore = Maximum(workItems.Select(
                item => item.OutOfDistribution.Score)),
            SupportLevels = SupportLevelSummaries(workItems),
        };

    public static IReadOnlyList<CalibrationUncertaintySupportRepositorySummary> Repositories(
        IReadOnlyList<CalibrationUncertaintySupportInput> inputs,
        IReadOnlyList<CalibrationUncertaintySupportWorkItem> workItems) =>
    [
        .. inputs.OrderBy(input => input.Population.RepositoryId, StringComparer.Ordinal)
            .ThenBy(input => input.Population.RecordId, StringComparer.Ordinal)
            .Select(input => Repository(
                input,
                [.. workItems.Where(item => string.Equals(
                    item.RecordId,
                    input.Population.RecordId,
                    StringComparison.Ordinal))])),
    ];

    private static CalibrationUncertaintySupportRepositorySummary Repository(
        CalibrationUncertaintySupportInput input,
        IReadOnlyList<CalibrationUncertaintySupportWorkItem> items) => new()
        {
            RecordId = input.Population.RecordId,
            RepositoryId = input.Population.RepositoryId,
            SourceDigest = input.Population.SourceDigest,
            FeatureReportDigest = CalibrationDigest.Compute(input.Report),
            EstimateDigest = input.Report.EstimateDigest,
            EvidenceDigest = input.Report.EvidenceDigest,
            WorkItemCount = items.Count,
            UniqueProfileCount = items.Select(item => item.OutOfDistribution.ProfileDigest)
                .Distinct(StringComparer.Ordinal).Count(),
            ExactProfileMatchWorkItemCount = items.Count(item =>
                item.OutOfDistribution.ExactProfileTrainingObservationCount > 0),
            InsufficientSupportWorkItemCount = items.Count(item => !item.SupportSufficient),
            MeanOutOfDistributionScore = Mean(items.Select(
                item => item.OutOfDistribution.Score)),
            P90OutOfDistributionScore = P90(items.Select(
                item => item.OutOfDistribution.Score)),
            MaximumOutOfDistributionScore = Maximum(items.Select(
                item => item.OutOfDistribution.Score)),
            SupportLevels = SupportLevelSummaries(items),
        };

    private static IReadOnlyList<CalibrationUncertaintySupportLevelSummary> SupportLevelSummaries(
        IReadOnlyList<CalibrationUncertaintySupportWorkItem> items) =>
    [
        .. CalibrationUncertaintySupportProfiler.SupportHierarchy.Select(level =>
            new CalibrationUncertaintySupportLevelSummary
            {
                Level = level,
                WorkItemCount = items.Count(item => item.SelectedSupportLevel == level),
            }),
    ];

    private static decimal Mean(IEnumerable<decimal> values)
    {
        decimal[] materialized = [.. values];
        return materialized.Length == 0
            ? 0m
            : CalibrationUncertaintyEvaluationMath.Round6(materialized.Average());
    }

    private static decimal P90(IEnumerable<decimal> values)
    {
        decimal[] ordered = [.. values.Order()];
        if (ordered.Length == 0)
        {
            return 0m;
        }

        int rank = (int)decimal.Ceiling(0.90m * ordered.Length);
        return ordered[Math.Max(0, rank - 1)];
    }

    private static decimal Maximum(IEnumerable<decimal> values)
    {
        decimal[] materialized = [.. values];
        return materialized.Length == 0 ? 0m : materialized.Max();
    }
}
