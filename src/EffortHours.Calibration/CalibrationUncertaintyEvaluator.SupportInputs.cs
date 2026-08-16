using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    private static void ValidateSupportEvaluationInputs(
        CalibrationUncertaintyEvaluationReport source,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> reports,
        CalibrationUncertaintySupportProfile support)
    {
        List<string> errors = [.. ContractValidation.Validate(support)];
        if (source.Summary.IgnoredFeatureReportCount != 0 ||
            reports.Count != source.Repositories.Count ||
            support.Repositories.Count != source.Repositories.Count)
        {
            errors.Add(
                "Support evaluation requires exactly one used feature report and support " +
                "repository row per development record.");
        }

        if (support.Partition != source.Partition ||
            support.FeatureContractVersion != source.FeatureContractVersion ||
            support.FeatureContractDigest != source.FeatureContractDigest ||
            !support.ProjectorVersions.SequenceEqual(source.ProjectorVersions) ||
            !support.EstimatorVersions.SequenceEqual(source.EstimatorVersions))
        {
            errors.Add(
                "Support profile does not match the source evaluation partition, feature " +
                "contract, projector, or estimator identity.");
        }

        if (reports.Any(report => report.Profile != support.Profile ||
                !string.Equals(report.BaselineId, support.BaselineId, StringComparison.Ordinal)))
        {
            errors.Add("Support profile does not match feature-report profile or baseline.");
        }

        Dictionary<string, CalibrationUncertaintySupportRepositorySummary> supportRepositories =
            support.Repositories.ToDictionary(repository => repository.RecordId, StringComparer.Ordinal);
        Dictionary<string, CalibrationUncertaintyFeatureReport> reportsBySource = reports
            .GroupBy(report => report.RepositorySourceDigest, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (CalibrationUncertaintyRepositoryEvaluation repository in source.Repositories)
        {
            if (!supportRepositories.TryGetValue(
                    repository.RecordId,
                    out CalibrationUncertaintySupportRepositorySummary? supportRepository) ||
                supportRepository.RepositoryId != repository.RepositoryId ||
                supportRepository.SourceDigest != repository.SourceDigest ||
                supportRepository.FeatureReportDigest != repository.FeatureReportDigest ||
                supportRepository.EstimateDigest != repository.EstimateDigest)
            {
                errors.Add(
                    $"Support repository '{repository.RecordId}' does not match source " +
                    "evaluation lineage.");
                continue;
            }

            if (!reportsBySource.TryGetValue(
                    repository.SourceDigest,
                    out CalibrationUncertaintyFeatureReport? report))
            {
                errors.Add(
                    $"No feature report matches support repository '{repository.RecordId}'.");
                continue;
            }

            CalibrationUncertaintySupportWorkItem[] supportItems =
            [
                .. support.WorkItems.Where(item => string.Equals(
                    item.RecordId,
                    repository.RecordId,
                    StringComparison.Ordinal)),
            ];
            Dictionary<string, CalibrationUncertaintySupportWorkItem> supportById = supportItems
                .ToDictionary(item => item.WorkItemId, StringComparer.Ordinal);
            if (supportItems.Length != report.WorkItems.Count)
            {
                errors.Add(
                    $"Support repository '{repository.RecordId}' work-item count does not " +
                    "match its feature report.");
            }

            foreach (CalibrationUncertaintyWorkItemFeatures item in report.WorkItems)
            {
                if (!supportById.TryGetValue(
                        item.WorkItemId,
                        out CalibrationUncertaintySupportWorkItem? supportItem) ||
                    supportItem.Category != item.Category ||
                    supportItem.ExpectedSizeBand !=
                        CalibrationUncertaintyBucketing.SizeBand(item.ExpectedHours) ||
                    !supportItem.Ecosystems.SequenceEqual(
                        item.Ecosystems.Order(StringComparer.Ordinal)))
                {
                    errors.Add(
                        $"Support work item '{repository.RecordId}/{item.WorkItemId}' does not " +
                        "match its immutable feature row.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors.Distinct(StringComparer.Ordinal));
        }
    }
}
