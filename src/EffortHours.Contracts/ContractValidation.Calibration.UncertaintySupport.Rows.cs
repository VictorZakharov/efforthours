using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateUncertaintySupportRows(
        CalibrationUncertaintySupportProfile report,
        List<string> errors)
    {
        Dictionary<string, CalibrationUncertaintySupportRepositorySummary> repositories =
            new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintySupportRepositorySummary repository in report.Repositories)
        {
            string path = $"repository[{repository.RecordId}]";
            RequireText(repository.RecordId, "repository.recordId", errors);
            RequireText(repository.RepositoryId, $"{path}.repositoryId", errors);
            RequireDigest(repository.SourceDigest, $"{path}.sourceDigest", errors);
            RequireDigest(repository.FeatureReportDigest, $"{path}.featureReportDigest", errors);
            RequireDigest(repository.EstimateDigest, $"{path}.estimateDigest", errors);
            RequireDigest(repository.EvidenceDigest, $"{path}.evidenceDigest", errors);
            if (!repositories.TryAdd(repository.RecordId, repository))
            {
                errors.Add($"Uncertainty support repository record '{repository.RecordId}' is duplicated.");
            }
        }

        HashSet<(string RecordId, string WorkItemId)> itemIds = [];
        foreach (CalibrationUncertaintySupportWorkItem item in report.WorkItems)
        {
            ValidateUncertaintySupportWorkItem(item, repositories, itemIds, report.Policy, errors);
        }

        ValidateUncertaintySupportRepositorySummaries(report, errors);
        ValidateUncertaintySupportSummary(report, errors);
        string[] repositoryOrder =
        [
            .. report.Repositories.Select(repository =>
                $"{repository.RepositoryId}\u001f{repository.RecordId}"),
        ];
        if (!repositoryOrder.SequenceEqual(
                repositoryOrder.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            errors.Add("Uncertainty support repository rows are not in canonical order.");
        }

        string[] itemOrder =
        [
            .. report.WorkItems.Select(item =>
                $"{item.RepositoryId}\u001f{item.RecordId}\u001f{item.WorkItemId}"),
        ];
        if (!itemOrder.SequenceEqual(itemOrder.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            errors.Add("Uncertainty support work-item rows are not in canonical order.");
        }
    }

    private static void ValidateUncertaintySupportWorkItem(
        CalibrationUncertaintySupportWorkItem item,
        Dictionary<string, CalibrationUncertaintySupportRepositorySummary> repositories,
        HashSet<(string RecordId, string WorkItemId)> ids,
        CalibrationUncertaintySupportPolicy policy,
        List<string> errors)
    {
        string path = $"workItem[{item.RecordId}/{item.WorkItemId}]";
        RequireText(item.RecordId, "workItem.recordId", errors);
        RequireText(item.RepositoryId, $"{path}.repositoryId", errors);
        RequireText(item.WorkItemId, $"{path}.workItemId", errors);
        RequireText(item.ExpectedSizeBand, $"{path}.expectedSizeBand", errors);
        RequireUniqueText(item.Ecosystems, $"{path}.ecosystems", errors);
        if (!item.Ecosystems.SequenceEqual(item.Ecosystems.Order(StringComparer.Ordinal)))
        {
            errors.Add($"{path}.ecosystems must use canonical ordinal order.");
        }

        if (!ids.Add((item.RecordId, item.WorkItemId)))
        {
            errors.Add($"Uncertainty support work item '{item.RecordId}/{item.WorkItemId}' is duplicated.");
        }

        if (!repositories.TryGetValue(
                item.RecordId,
                out CalibrationUncertaintySupportRepositorySummary? repository) ||
            repository.RepositoryId != item.RepositoryId)
        {
            errors.Add($"{path} does not match a repository summary row.");
        }

        if (item.SupportCells.Count != UncertaintySupportHierarchy.Length ||
            !item.SupportCells.Select(cell => cell.Level)
                .SequenceEqual(UncertaintySupportHierarchy))
        {
            errors.Add($"{path}.supportCells does not match the v1 hierarchy.");
        }

        CalibrationUncertaintySupportCell? selected = null;
        foreach (CalibrationUncertaintySupportCell cell in item.SupportCells)
        {
            bool sufficient = cell.TrainingObservationCount >=
                    policy.MinimumCellObservationCount &&
                cell.TrainingRepositoryCount >= policy.MinimumCellRepositoryCount;
            if (cell.TrainingObservationCount < 0 || cell.TrainingRepositoryCount < 0 ||
                cell.Sufficient != sufficient)
            {
                errors.Add($"{path}.supportCell[{cell.Level}] contains invalid support counts.");
            }

            if (cell.Selected)
            {
                if (selected is not null)
                {
                    errors.Add($"{path} selects more than one support cell.");
                }

                selected = cell;
            }
        }

        CalibrationUncertaintySupportCell? expectedSelected =
            item.SupportCells.FirstOrDefault(cell => cell.Sufficient) ??
            (item.SupportCells.Count == 0 ? null : item.SupportCells[^1]);
        if (selected is null || selected != expectedSelected ||
            item.SelectedSupportLevel != selected.Level ||
            item.SupportSufficient != selected.Sufficient)
        {
            errors.Add($"{path} does not select the first sufficient support cell or global fallback.");
        }

        ValidateUncertaintyOutOfDistribution(item, path, policy, errors);
    }

    private static void ValidateUncertaintyOutOfDistribution(
        CalibrationUncertaintySupportWorkItem item,
        string path,
        CalibrationUncertaintySupportPolicy policy,
        List<string> errors)
    {
        CalibrationUncertaintyOutOfDistribution value = item.OutOfDistribution;
        RequireDigest(value.ProfileDigest, $"{path}.outOfDistribution.profileDigest", errors);
        RequireText(value.NearestRecordId, $"{path}.outOfDistribution.nearestRecordId", errors);
        RequireText(
            value.NearestRepositoryId,
            $"{path}.outOfDistribution.nearestRepositoryId",
            errors);
        RequireText(value.NearestWorkItemId, $"{path}.outOfDistribution.nearestWorkItemId", errors);
        if (value.Score is < 0m or > 1m ||
            value.StructuralDistance is < 0m or > 1m ||
            value.FeatureDistance is < 0m or > 1m ||
            value.ComparedFeatureCount != policy.FeatureDimensionCount ||
            value.ExactProfileTrainingObservationCount < 0 ||
            value.ExactProfileTrainingRepositoryCount < 0 ||
            value.NearestProfileTrainingObservationCount < 1 ||
            value.NearestProfileTrainingRepositoryCount < 1 ||
            string.Equals(item.RepositoryId, value.NearestRepositoryId, StringComparison.Ordinal))
        {
            errors.Add($"{path}.outOfDistribution contains invalid distances, counts, or lineage.");
        }

        decimal expectedScore = RoundUncertaintySupport(
            ((policy.StructuralDimensionCount * value.StructuralDistance) +
                (policy.FeatureDimensionCount * value.FeatureDistance)) /
            (policy.StructuralDimensionCount + policy.FeatureDimensionCount));
        if (value.Score != expectedScore ||
            (value.ExactProfileTrainingObservationCount > 0) != (value.Score == 0m))
        {
            errors.Add($"{path}.outOfDistribution score or exact-profile support is inconsistent.");
        }
    }
}
