using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateUncertaintySupportEvaluationTargets(
        CalibrationUncertaintySupportEvaluationReport report,
        List<string> errors)
    {
        HashSet<(string RecordId, string TargetId)> ids = [];
        foreach (CalibrationUncertaintySupportTargetEvaluation target in report.Targets)
        {
            CalibrationUncertaintyTargetEvaluation source = target.Source;
            string path = $"target[{source.RecordId}/{source.TargetId}]";
            int expectedDepth = target.SupportSufficient
                ? target.WorstSupportLevel switch
                {
                    CalibrationUncertaintySupportLevel.Exact => 0,
                    CalibrationUncertaintySupportLevel.CategorySizeEcosystem => 1,
                    CalibrationUncertaintySupportLevel.CategorySize => 2,
                    CalibrationUncertaintySupportLevel.Category => 3,
                    CalibrationUncertaintySupportLevel.Global => 4,
                    _ => -1,
                }
                : 5;
            if (!ids.Add((source.RecordId, source.TargetId)) ||
                target.SourceWorkItemCount < 1 ||
                target.WorstSupportDepth != expectedDepth ||
                target.MinimumSelectedSupportObservationCount < 0 ||
                target.MinimumSelectedSupportRepositoryCount < 0 ||
                target.ExpectedWeightedOutOfDistributionScore is < 0m or > 1m ||
                target.MaximumOutOfDistributionScore is < 0m or > 1m ||
                target.ExpectedWeightedOutOfDistributionScore >
                    target.MaximumOutOfDistributionScore ||
                (target.SupportSufficient &&
                    (target.MinimumSelectedSupportObservationCount < 3 ||
                     target.MinimumSelectedSupportRepositoryCount < 2)) ||
                (!target.SupportSufficient &&
                    (target.WorstSupportLevel != CalibrationUncertaintySupportLevel.Global ||
                     (target.MinimumSelectedSupportObservationCount >= 3 &&
                      target.MinimumSelectedSupportRepositoryCount >= 2))))
            {
                errors.Add($"{path} contains inconsistent support or OOD aggregation.");
            }
        }
    }

    private static void ValidateUncertaintySupportEvaluationSummary(
        CalibrationUncertaintySupportEvaluationReport report,
        List<string> errors)
    {
        CalibrationUncertaintySupportEvaluationSummary summary = report.Summary;
        if (summary.RecordCount != report.Repositories.Count ||
            summary.RepositoryCount != report.Repositories.Select(repository =>
                    repository.RepositoryId)
                .Distinct(StringComparer.Ordinal).Count() ||
            summary.RepositoryCount < 3 ||
            summary.FeatureReportCount != summary.RecordCount ||
            summary.TargetCount != report.Repositories.Sum(repository => repository.TargetCount) ||
            summary.MatchedTargetCount != report.Targets.Count ||
            summary.MatchedTargetCount != report.Repositories.Sum(repository =>
                repository.MatchedTargetCount) ||
            summary.SourceWorkItemReferenceCount != report.Repositories.Sum(repository =>
                repository.SourceWorkItemReferenceCount) ||
            summary.MatchedSourceWorkItemReferenceCount != report.Repositories.Sum(repository =>
                repository.MatchedSourceWorkItemReferenceCount) ||
            summary.SupportProfileWorkItemCount < 1 ||
            summary.MatchedSupportWorkItemReferenceCount != report.Targets.Sum(target =>
                target.SourceWorkItemCount) ||
            summary.SignalCount != SupportEvaluationSignalBoundary.Length ||
            summary.SignalCount != report.Signals.Count)
        {
            errors.Add("Uncertainty support evaluation summary does not reconcile to report rows.");
        }
    }

    private static decimal SupportEvaluationSignalValue(
        CalibrationUncertaintySupportTargetEvaluation target,
        string signalId) => signalId switch
        {
            CalibrationUncertaintySupportSignalIds.FallbackDepth => target.WorstSupportDepth,
            CalibrationUncertaintySupportSignalIds.MinimumRepositoryCount =>
                target.MinimumSelectedSupportRepositoryCount,
            CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution =>
                target.ExpectedWeightedOutOfDistributionScore,
            CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution =>
                target.MaximumOutOfDistributionScore,
            _ => throw new InvalidOperationException($"Unknown support signal '{signalId}'."),
        };
}
