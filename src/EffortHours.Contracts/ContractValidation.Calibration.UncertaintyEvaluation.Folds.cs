using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateUncertaintyEvaluationFeatureFolds(
        CalibrationUncertaintyFeatureEvaluation feature,
        CalibrationUncertaintyEvaluationReport report,
        string path,
        List<string> errors)
    {
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        Dictionary<string, CalibrationUncertaintyRepositoryEvaluation> repositories =
            report.Repositories.GroupBy(repository => repository.RecordId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (CalibrationUncertaintyFeatureRepositoryFold fold in feature.RepositoryFolds)
        {
            string foldPath = $"{path}.repositoryFold[{fold.RecordId}]";
            RequireText(fold.RecordId, $"{foldPath}.recordId", errors);
            RequireText(fold.RepositoryId, $"{foldPath}.repositoryId", errors);
            ValidateUncertaintyEvaluationPerformance(fold.Intervals, $"{foldPath}.intervals", errors);
            if (!recordIds.Add(fold.RecordId) ||
                !repositories.TryGetValue(
                    fold.RecordId,
                    out CalibrationUncertaintyRepositoryEvaluation? repository) ||
                repository.RepositoryId != fold.RepositoryId ||
                fold.ConditionedPredictionCount < 0 ||
                fold.BaselineFallbackCount < 0 ||
                fold.ConditionedPredictionCount + fold.BaselineFallbackCount !=
                    fold.Intervals.ObservationCount ||
                fold.Intervals.ObservationCount != repository.MatchedTargetCount)
            {
                errors.Add($"{foldPath} contains inconsistent identity or prediction counts.");
            }
        }

        if (feature.RepositoryFolds.Count != report.Repositories.Count ||
            feature.RepositoryFolds.Sum(fold => fold.ConditionedPredictionCount) !=
                feature.ConditionedPredictionCount ||
            feature.RepositoryFolds.Sum(fold => fold.BaselineFallbackCount) !=
                feature.BaselineFallbackCount ||
            feature.RepositoryFolds.Sum(fold => fold.Intervals.ObservationCount) !=
                feature.CrossValidatedIntervals.ObservationCount ||
            feature.RepositoryFolds.Sum(fold => fold.Intervals.ReviewedExpectedCoveredCount) !=
                feature.CrossValidatedIntervals.ReviewedExpectedCoveredCount)
        {
            errors.Add($"{path}.repositoryFolds do not reconcile to feature totals.");
        }
    }
}
