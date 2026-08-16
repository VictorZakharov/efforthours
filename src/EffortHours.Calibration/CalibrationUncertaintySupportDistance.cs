using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintySupportDistance
{
    public static CalibrationUncertaintySupportDistanceEvaluation Evaluate(
        IReadOnlyList<CalibrationUncertaintySupportObservation> observations,
        CancellationToken cancellationToken)
    {
        CalibrationUncertaintySupportProfileGroup[] groups =
        [
            .. observations.GroupBy(
                    observation => new CalibrationUncertaintySupportProfileGroupKey(
                        observation.RepositoryId,
                        observation.ProfileSignature))
                .Select(group => new CalibrationUncertaintySupportProfileGroup
                {
                    RepositoryId = group.Key.RepositoryId,
                    ProfileSignature = group.Key.ProfileSignature,
                    ObservationCount = group.Count(),
                    Representative = group
                        .OrderBy(item => item.RecordId, StringComparer.Ordinal)
                        .ThenBy(item => item.WorkItemId, StringComparer.Ordinal)
                        .First(),
                })
                .OrderBy(group => group.RepositoryId, StringComparer.Ordinal)
                .ThenBy(group => group.Representative.RecordId, StringComparer.Ordinal)
                .ThenBy(group => group.Representative.WorkItemId, StringComparer.Ordinal),
        ];
        Dictionary<string, CalibrationUncertaintySupportProfileGroup[]> bySignature = groups
            .GroupBy(group => group.ProfileSignature, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, int> groupCountByRepository = groups
            .GroupBy(group => group.RepositoryId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        long comparisons = groups.Sum(group =>
            (long)groups.Length - groupCountByRepository[group.RepositoryId]);
        if (comparisons > CalibrationUncertaintySupportProfiler.MaximumProfileComparisonCount)
        {
            throw new CalibrationEvaluationException(
                [
                    $"Uncertainty support profiling would compare {comparisons} profile pairs; " +
                    $"the v1 bound is " +
                    $"{CalibrationUncertaintySupportProfiler.MaximumProfileComparisonCount}.",
                ]);
        }

        Dictionary<CalibrationUncertaintySupportProfileGroupKey,
            CalibrationUncertaintyOutOfDistributionResult> results = [];
        foreach (CalibrationUncertaintySupportProfileGroup source in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CalibrationUncertaintySupportProfileGroup[] exact = bySignature[
                source.ProfileSignature];
            CalibrationUncertaintySupportProfileGroup[] crossRepositoryExact =
            [
                .. exact.Where(candidate => !string.Equals(
                    candidate.RepositoryId,
                    source.RepositoryId,
                    StringComparison.Ordinal)),
            ];
            CalibrationUncertaintySupportProfileGroup nearestGroup;
            decimal structuralDistance;
            decimal featureDistance;
            decimal score;
            if (crossRepositoryExact.Length > 0)
            {
                nearestGroup = crossRepositoryExact[0];
                structuralDistance = 0m;
                featureDistance = 0m;
                score = 0m;
            }
            else
            {
                (nearestGroup, structuralDistance, featureDistance, score) = FindNearest(
                    source,
                    groups,
                    cancellationToken);
            }

            CalibrationUncertaintySupportProfileGroup[] nearestProfile = bySignature[
                nearestGroup.ProfileSignature];
            CalibrationUncertaintySupportProfileGroup[] nearestTraining =
            [
                .. nearestProfile.Where(candidate => !string.Equals(
                    candidate.RepositoryId,
                    source.RepositoryId,
                    StringComparison.Ordinal)),
            ];
            results.Add(
                new CalibrationUncertaintySupportProfileGroupKey(
                    source.RepositoryId,
                    source.ProfileSignature),
                new CalibrationUncertaintyOutOfDistributionResult
                {
                    Score = score,
                    StructuralDistance = structuralDistance,
                    FeatureDistance = featureDistance,
                    ExactObservationCount = crossRepositoryExact.Sum(
                        candidate => candidate.ObservationCount),
                    ExactRepositoryCount = crossRepositoryExact.Select(
                            candidate => candidate.RepositoryId)
                        .Distinct(StringComparer.Ordinal).Count(),
                    Nearest = nearestGroup.Representative,
                    NearestProfileObservationCount = nearestTraining.Sum(
                        candidate => candidate.ObservationCount),
                    NearestProfileRepositoryCount = nearestTraining.Select(
                            candidate => candidate.RepositoryId)
                        .Distinct(StringComparer.Ordinal).Count(),
                });
        }

        return new CalibrationUncertaintySupportDistanceEvaluation
        {
            Results = results,
            UniqueProfileCount = bySignature.Count,
            ProfileEvaluationCount = groups.Length,
            ProfileComparisonCount = comparisons,
        };
    }

    private static (
        CalibrationUncertaintySupportProfileGroup Group,
        decimal Structural,
        decimal Feature,
        decimal Score) FindNearest(
            CalibrationUncertaintySupportProfileGroup source,
            IReadOnlyList<CalibrationUncertaintySupportProfileGroup> groups,
            CancellationToken cancellationToken)
    {
        CalibrationUncertaintySupportProfileGroup? nearest = null;
        decimal nearestStructural = 0m;
        decimal nearestFeature = 0m;
        decimal nearestScore = decimal.MaxValue;
        int iteration = 0;
        foreach (CalibrationUncertaintySupportProfileGroup candidate in groups)
        {
            if (string.Equals(
                    source.RepositoryId,
                    candidate.RepositoryId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if ((iteration++ & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            (decimal structural, decimal feature, decimal score) = Calculate(
                source.Representative,
                candidate.Representative);
            if (score < nearestScore)
            {
                nearest = candidate;
                nearestStructural = structural;
                nearestFeature = feature;
                nearestScore = score;
            }
        }

        if (nearest is null)
        {
            throw new CalibrationEvaluationException(
                [
                    $"Repository '{source.RepositoryId}' has no work-item profile in another " +
                    "repository family for OOD comparison.",
                ]);
        }

        return (nearest, nearestStructural, nearestFeature, nearestScore);
    }

    private static (decimal Structural, decimal Feature, decimal Score) Calculate(
        CalibrationUncertaintySupportObservation source,
        CalibrationUncertaintySupportObservation candidate)
    {
        decimal category = source.Category == candidate.Category ? 0m : 1m;
        decimal size = decimal.Abs(
            CalibrationUncertaintyBucketing.SizeBandOrder(source.ExpectedSizeBand) -
            CalibrationUncertaintyBucketing.SizeBandOrder(candidate.ExpectedSizeBand)) / 8m;
        decimal complexity = decimal.Abs(
            CalibrationUncertaintyBucketing.ComplexityOrder(source.SourceComplexity) -
            CalibrationUncertaintyBucketing.ComplexityOrder(candidate.SourceComplexity)) / 3m;
        decimal ecosystems = JaccardDistance(source.Ecosystems, candidate.Ecosystems);
        decimal structural = CalibrationUncertaintyEvaluationMath.Round6(
            (category + size + complexity + ecosystems) / 4m);

        decimal featureTotal = 0m;
        for (int index = 0; index < source.Features.Count; index++)
        {
            CalibrationUncertaintySupportFeaturePoint left = source.Features[index];
            CalibrationUncertaintySupportFeaturePoint right = candidate.Features[index];
            if (!string.Equals(left.Id, right.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Uncertainty support profiles do not share feature order.");
            }

            featureTotal += FeatureDistance(left, right);
        }

        decimal feature = CalibrationUncertaintyEvaluationMath.Round6(
            featureTotal / source.Features.Count);
        decimal score = CalibrationUncertaintyEvaluationMath.Round6(
            ((4m * structural) + (source.Features.Count * feature)) /
            (4m + source.Features.Count));
        return (structural, feature, score);
    }

    private static decimal FeatureDistance(
        CalibrationUncertaintySupportFeaturePoint left,
        CalibrationUncertaintySupportFeaturePoint right)
    {
        if (left.Availability != right.Availability)
        {
            return 1m;
        }

        if (left.Availability != CalibrationUncertaintyFeatureAvailability.Available)
        {
            return 0m;
        }

        return (decimal)Math.Abs(left.BucketOrder - right.BucketOrder) / left.MaximumOrder;
    }

    private static decimal JaccardDistance(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 0m;
        }

        int intersection = left.Intersect(right, StringComparer.Ordinal).Count();
        int union = left.Union(right, StringComparer.Ordinal).Count();
        return 1m - ((decimal)intersection / union);
    }
}
