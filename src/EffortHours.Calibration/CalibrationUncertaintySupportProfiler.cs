using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationUncertaintySupportProfiler
{
    public const int MinimumObservationCount = 3;
    public const int MinimumRepositoryCount = 2;
    public const int StructuralDimensionCount = 4;
    public const int MaximumWorkItemCount = 250_000;
    public const long MaximumProfileComparisonCount = 50_000_000;

    internal static IReadOnlyList<CalibrationUncertaintySupportLevel> SupportHierarchy { get; } =
    [
        CalibrationUncertaintySupportLevel.Exact,
        CalibrationUncertaintySupportLevel.CategorySizeEcosystem,
        CalibrationUncertaintySupportLevel.CategorySize,
        CalibrationUncertaintySupportLevel.Category,
        CalibrationUncertaintySupportLevel.Global,
    ];

    public static CalibrationUncertaintySupportProfile Profile(
        CalibrationUncertaintySupportPopulation population,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> featureReports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(featureReports);
        IReadOnlyList<CalibrationUncertaintySupportInput> inputs =
            CalibrationUncertaintySupportInputs.Match(population, featureReports);
        IReadOnlyList<CalibrationUncertaintySupportObservation> observations =
            CalibrationUncertaintySupportObservations.Create(inputs, cancellationToken);
        CalibrationUncertaintySupportIndex supportIndex = new(observations);
        CalibrationUncertaintySupportDistanceEvaluation distances =
            CalibrationUncertaintySupportDistance.Evaluate(observations, cancellationToken);

        CalibrationUncertaintySupportWorkItem[] workItems =
        [
            .. observations.OrderBy(item => item.RepositoryId, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkItemId, StringComparer.Ordinal)
                .Select(observation => ToWorkItem(
                    observation,
                    supportIndex.Cells(observation),
                    distances.Results[new CalibrationUncertaintySupportProfileGroupKey(
                        observation.RepositoryId,
                        observation.ProfileSignature)])),
        ];
        CalibrationUncertaintyFeatureReport first = inputs[0].Report;
        CalibrationUncertaintySupportProfile result = new()
        {
            ProfilerVersion = CalibrationUncertaintyVersions.SupportProfilerV1,
            PopulationId = population.Id,
            PopulationVersion = population.Version,
            PopulationDigest = CalibrationDigest.Compute(population),
            Partition = population.Partition,
            FeatureContractVersion = population.FeatureContractVersion,
            FeatureContractDigest = population.FeatureContractDigest,
            ProjectorVersions = [.. inputs.Select(input => input.Report.ProjectorVersion)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            EstimatorVersions = [.. inputs.Select(input => input.Report.EstimatorVersion)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Profile = population.Profile,
            BaselineId = population.BaselineId,
            Policy = CreatePolicy(first.FeatureContract.Features.Count),
            Summary = CalibrationUncertaintySupportSummaries.Overall(
                inputs,
                workItems,
                distances),
            Repositories = CalibrationUncertaintySupportSummaries.Repositories(inputs, workItems),
            WorkItems = workItems,
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(result);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Uncertainty support profiler produced an invalid report: " +
                string.Join("; ", errors));
        }

        return result;
    }

    private static CalibrationUncertaintySupportWorkItem ToWorkItem(
        CalibrationUncertaintySupportObservation observation,
        IReadOnlyList<CalibrationUncertaintySupportCell> cells,
        CalibrationUncertaintyOutOfDistributionResult distance)
    {
        CalibrationUncertaintySupportCell selected = cells.Single(cell => cell.Selected);
        return new CalibrationUncertaintySupportWorkItem
        {
            RecordId = observation.RecordId,
            RepositoryId = observation.RepositoryId,
            WorkItemId = observation.WorkItemId,
            Category = observation.Category,
            ExpectedSizeBand = observation.ExpectedSizeBand,
            SourceComplexity = observation.SourceComplexity,
            Ecosystems = observation.Ecosystems,
            SelectedSupportLevel = selected.Level,
            SupportSufficient = selected.Sufficient,
            SupportCells = cells,
            OutOfDistribution = new CalibrationUncertaintyOutOfDistribution
            {
                ProfileDigest = observation.ProfileDigest,
                Score = distance.Score,
                StructuralDistance = distance.StructuralDistance,
                FeatureDistance = distance.FeatureDistance,
                ComparedFeatureCount = observation.Features.Count,
                ExactProfileTrainingObservationCount = distance.ExactObservationCount,
                ExactProfileTrainingRepositoryCount = distance.ExactRepositoryCount,
                NearestRecordId = distance.Nearest.RecordId,
                NearestRepositoryId = distance.Nearest.RepositoryId,
                NearestWorkItemId = distance.Nearest.WorkItemId,
                NearestProfileTrainingObservationCount =
                    distance.NearestProfileObservationCount,
                NearestProfileTrainingRepositoryCount =
                    distance.NearestProfileRepositoryCount,
            },
        };
    }

    private static CalibrationUncertaintySupportPolicy CreatePolicy(int featureCount) => new()
    {
        Version = CalibrationUncertaintyVersions.SupportPolicyV1,
        FoldUnit = "repository-family",
        CellHierarchy = SupportHierarchy,
        MinimumCellObservationCount = MinimumObservationCount,
        MinimumCellRepositoryCount = MinimumRepositoryCount,
        DistanceMetric = "gower-bucket-distance/1.0.0",
        StructuralDimensionCount = StructuralDimensionCount,
        FeatureDimensionCount = featureCount,
        AvailabilityMismatchDistance = 1m,
        SameRepositoryExcluded = true,
        LabelIndependent = true,
        UsesReviewedValues = false,
        MaximumWorkItemCount = MaximumWorkItemCount,
        MaximumProfileComparisonCount = MaximumProfileComparisonCount,
    };
}
