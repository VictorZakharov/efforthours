using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(
        CalibrationUncertaintySupportPopulation population)
    {
        ArgumentNullException.ThrowIfNull(population);
        List<string> errors = [];
        RequireVersion(population.SchemaVersion, "uncertainty support population", errors);
        if (population.ManifestVersion != CalibrationUncertaintyVersions.SupportPopulationV1)
        {
            errors.Add(
                $"Unsupported uncertainty support population manifest " +
                $"'{population.ManifestVersion}'.");
        }

        RequireText(population.Id, "id", errors);
        RequireText(population.Version, "version", errors);
        RequireText(population.Description, "description", errors);
        if (population.Partition != CalibrationPartition.Development)
        {
            errors.Add("Uncertainty support population v1 must be development-only.");
        }

        if (population.FeatureContractVersion !=
                CalibrationUncertaintyVersions.FeatureContractV1 ||
            population.FeatureContractDigest !=
                CalibrationUncertaintyVersions.FeatureContractDigestV1)
        {
            errors.Add("Uncertainty support population must pin the canonical v1 feature contract.");
        }

        RequireDigest(population.FeatureContractDigest, "featureContractDigest", errors);
        RequireText(population.BaselineId, "baselineId", errors);
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        HashSet<string> sourceDigests = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintySupportPopulationRepository repository in
                 population.Repositories)
        {
            string path = $"repository[{repository.RecordId}]";
            RequireText(repository.RecordId, "repository.recordId", errors);
            RequireText(repository.RepositoryId, $"{path}.repositoryId", errors);
            RequireDigest(repository.SourceDigest, $"{path}.sourceDigest", errors);
            if (!recordIds.Add(repository.RecordId))
            {
                errors.Add($"Support population record '{repository.RecordId}' is duplicated.");
            }

            if (!sourceDigests.Add(repository.SourceDigest))
            {
                errors.Add(
                    $"Support population source digest '{repository.SourceDigest}' is duplicated.");
            }
        }

        if (population.Repositories.Count < 3 ||
            population.Repositories.Select(repository => repository.RepositoryId)
                .Distinct(StringComparer.Ordinal).Count() < 3)
        {
            errors.Add(
                "Uncertainty support population requires at least three repository families.");
        }

        string[] order = [.. population.Repositories.Select(repository => repository.RecordId)];
        if (!order.SequenceEqual(order.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            errors.Add("Support population repositories must be ordered by recordId.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationUncertaintySupportProfile report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "uncertainty support profile", errors);
        if (report.ProfilerVersion != CalibrationUncertaintyVersions.SupportProfilerV1)
        {
            errors.Add($"Unsupported uncertainty support profiler '{report.ProfilerVersion}'.");
        }

        RequireText(report.PopulationId, "populationId", errors);
        RequireText(report.PopulationVersion, "populationVersion", errors);
        RequireDigest(report.PopulationDigest, "populationDigest", errors);
        if (report.Partition != CalibrationPartition.Development)
        {
            errors.Add("Uncertainty support profile v1 must be development-only.");
        }

        if (report.FeatureContractVersion !=
                CalibrationUncertaintyVersions.FeatureContractV1 ||
            report.FeatureContractDigest !=
                CalibrationUncertaintyVersions.FeatureContractDigestV1)
        {
            errors.Add("Uncertainty support profile must pin the canonical v1 feature contract.");
        }

        RequireDigest(report.FeatureContractDigest, "featureContractDigest", errors);
        RequireUniqueText(report.ProjectorVersions, "projectorVersions", errors);
        RequireUniqueText(report.EstimatorVersions, "estimatorVersions", errors);
        if (report.ProjectorVersions.Count == 0 || report.EstimatorVersions.Count == 0)
        {
            errors.Add("Uncertainty support profile must identify projector and estimator versions.");
        }

        RequireText(report.BaselineId, "baselineId", errors);
        ValidateUncertaintySupportPolicy(report.Policy, errors);
        ValidateUncertaintySupportRows(report, errors);
        return errors;
    }

    private static void ValidateUncertaintySupportPolicy(
        CalibrationUncertaintySupportPolicy policy,
        List<string> errors)
    {
        if (policy.Version != CalibrationUncertaintyVersions.SupportPolicyV1 ||
            policy.FoldUnit != "repository-family" ||
            !policy.CellHierarchy.SequenceEqual(UncertaintySupportHierarchy) ||
            policy.MinimumCellObservationCount != 3 ||
            policy.MinimumCellRepositoryCount != 2 ||
            policy.DistanceMetric != "gower-bucket-distance/1.0.0" ||
            policy.StructuralDimensionCount != 4 ||
            policy.FeatureDimensionCount != 11 ||
            policy.AvailabilityMismatchDistance != 1m ||
            !policy.SameRepositoryExcluded ||
            !policy.LabelIndependent ||
            policy.UsesReviewedValues ||
            policy.MaximumWorkItemCount != 250_000 ||
            policy.MaximumProfileComparisonCount != 50_000_000)
        {
            errors.Add("Uncertainty support policy does not satisfy its frozen v1 invariants.");
        }
    }

    private static readonly CalibrationUncertaintySupportLevel[] UncertaintySupportHierarchy =
    [
        CalibrationUncertaintySupportLevel.Exact,
        CalibrationUncertaintySupportLevel.CategorySizeEcosystem,
        CalibrationUncertaintySupportLevel.CategorySize,
        CalibrationUncertaintySupportLevel.Category,
        CalibrationUncertaintySupportLevel.Global,
    ];
}
