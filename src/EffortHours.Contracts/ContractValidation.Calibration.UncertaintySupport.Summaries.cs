using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateUncertaintySupportRepositorySummaries(
        CalibrationUncertaintySupportProfile report,
        List<string> errors)
    {
        foreach (CalibrationUncertaintySupportRepositorySummary repository in report.Repositories)
        {
            CalibrationUncertaintySupportWorkItem[] items =
            [
                .. report.WorkItems.Where(item => string.Equals(
                    item.RecordId,
                    repository.RecordId,
                    StringComparison.Ordinal)),
            ];
            if (repository.WorkItemCount != items.Length ||
                repository.UniqueProfileCount != items.Select(
                        item => item.OutOfDistribution.ProfileDigest)
                    .Distinct(StringComparer.Ordinal).Count() ||
                repository.ExactProfileMatchWorkItemCount != items.Count(item =>
                    item.OutOfDistribution.ExactProfileTrainingObservationCount > 0) ||
                repository.InsufficientSupportWorkItemCount != items.Count(
                    item => !item.SupportSufficient) ||
                repository.MeanOutOfDistributionScore != MeanUncertaintySupport(items.Select(
                    item => item.OutOfDistribution.Score)) ||
                repository.P90OutOfDistributionScore != P90UncertaintySupport(items.Select(
                    item => item.OutOfDistribution.Score)) ||
                repository.MaximumOutOfDistributionScore != MaximumUncertaintySupport(items.Select(
                    item => item.OutOfDistribution.Score)))
            {
                errors.Add(
                    $"Repository summary '{repository.RecordId}' does not reconcile to work items.");
            }

            ValidateUncertaintySupportLevelSummaries(
                repository.SupportLevels,
                items,
                $"repository[{repository.RecordId}]",
                errors);
        }
    }

    private static void ValidateUncertaintySupportSummary(
        CalibrationUncertaintySupportProfile report,
        List<string> errors)
    {
        CalibrationUncertaintySupportSummary summary = report.Summary;
        if (summary.RepositoryCount != report.Repositories.Select(
                    repository => repository.RepositoryId)
                .Distinct(StringComparer.Ordinal).Count() ||
            summary.RepositoryCount < 3 ||
            summary.FeatureReportCount != report.Repositories.Count ||
            summary.WorkItemCount != report.WorkItems.Count ||
            summary.UniqueProfileCount != report.WorkItems.Select(
                    item => item.OutOfDistribution.ProfileDigest)
                .Distinct(StringComparer.Ordinal).Count() ||
            summary.ProfileEvaluationCount < summary.UniqueProfileCount ||
            summary.ProfileEvaluationCount > summary.WorkItemCount ||
            summary.ProfileComparisonCount < summary.ProfileEvaluationCount ||
            summary.ProfileComparisonCount > report.Policy.MaximumProfileComparisonCount ||
            summary.ExactProfileMatchWorkItemCount != report.WorkItems.Count(item =>
                item.OutOfDistribution.ExactProfileTrainingObservationCount > 0) ||
            summary.InsufficientSupportWorkItemCount != report.WorkItems.Count(
                item => !item.SupportSufficient) ||
            summary.MeanOutOfDistributionScore != MeanUncertaintySupport(report.WorkItems.Select(
                item => item.OutOfDistribution.Score)) ||
            summary.P90OutOfDistributionScore != P90UncertaintySupport(report.WorkItems.Select(
                item => item.OutOfDistribution.Score)) ||
            summary.MaximumOutOfDistributionScore != MaximumUncertaintySupport(
                report.WorkItems.Select(item => item.OutOfDistribution.Score)))
        {
            errors.Add("Uncertainty support summary does not reconcile to report rows.");
        }

        ValidateUncertaintySupportLevelSummaries(
            summary.SupportLevels,
            report.WorkItems,
            "summary",
            errors);
    }

    private static void ValidateUncertaintySupportLevelSummaries(
        IReadOnlyList<CalibrationUncertaintySupportLevelSummary> summaries,
        IReadOnlyList<CalibrationUncertaintySupportWorkItem> items,
        string path,
        List<string> errors)
    {
        if (summaries.Count != UncertaintySupportHierarchy.Length ||
            !summaries.Select(summary => summary.Level).SequenceEqual(UncertaintySupportHierarchy))
        {
            errors.Add($"{path}.supportLevels does not match the v1 hierarchy.");
            return;
        }

        foreach (CalibrationUncertaintySupportLevelSummary summary in summaries)
        {
            if (summary.WorkItemCount < 0 || summary.WorkItemCount != items.Count(
                    item => item.SelectedSupportLevel == summary.Level))
            {
                errors.Add($"{path}.supportLevel[{summary.Level}] does not reconcile to work items.");
            }
        }
    }

    private static decimal MeanUncertaintySupport(IEnumerable<decimal> values)
    {
        decimal[] materialized = [.. values];
        return materialized.Length == 0
            ? 0m
            : RoundUncertaintySupport(materialized.Average());
    }

    private static decimal P90UncertaintySupport(IEnumerable<decimal> values)
    {
        decimal[] ordered = [.. values.Order()];
        if (ordered.Length == 0)
        {
            return 0m;
        }

        int rank = (int)decimal.Ceiling(0.90m * ordered.Length);
        return ordered[Math.Max(0, rank - 1)];
    }

    private static decimal MaximumUncertaintySupport(IEnumerable<decimal> values)
    {
        decimal[] materialized = [.. values];
        return materialized.Length == 0 ? 0m : materialized.Max();
    }

    private static decimal RoundUncertaintySupport(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
