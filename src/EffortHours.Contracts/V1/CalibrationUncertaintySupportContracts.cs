namespace EffortHours.Contracts.V1;

public enum CalibrationUncertaintySupportLevel
{
    Exact,
    CategorySizeEcosystem,
    CategorySize,
    Category,
    Global,
}

public sealed record CalibrationUncertaintySupportPopulationRepository
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }
}

public sealed record CalibrationUncertaintySupportPopulation
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string ManifestVersion { get; init; } =
        CalibrationUncertaintyVersions.SupportPopulationV1;

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string FeatureContractVersion { get; init; }

    public required string FeatureContractDigest { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportPopulationRepository> Repositories
    { get; init; } = [];
}

public sealed record CalibrationUncertaintySupportPolicy
{
    public required string Version { get; init; }

    public required string FoldUnit { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportLevel> CellHierarchy { get; init; } = [];

    public required int MinimumCellObservationCount { get; init; }

    public required int MinimumCellRepositoryCount { get; init; }

    public required string DistanceMetric { get; init; }

    public required int StructuralDimensionCount { get; init; }

    public required int FeatureDimensionCount { get; init; }

    public required decimal AvailabilityMismatchDistance { get; init; }

    public required bool SameRepositoryExcluded { get; init; }

    public required bool LabelIndependent { get; init; }

    public required bool UsesReviewedValues { get; init; }

    public required int MaximumWorkItemCount { get; init; }

    public required long MaximumProfileComparisonCount { get; init; }
}

public sealed record CalibrationUncertaintySupportCell
{
    public required CalibrationUncertaintySupportLevel Level { get; init; }

    public required int TrainingObservationCount { get; init; }

    public required int TrainingRepositoryCount { get; init; }

    public required bool Sufficient { get; init; }

    public required bool Selected { get; init; }
}

public sealed record CalibrationUncertaintyOutOfDistribution
{
    public required string ProfileDigest { get; init; }

    public required decimal Score { get; init; }

    public required decimal StructuralDistance { get; init; }

    public required decimal FeatureDistance { get; init; }

    public required int ComparedFeatureCount { get; init; }

    public required int ExactProfileTrainingObservationCount { get; init; }

    public required int ExactProfileTrainingRepositoryCount { get; init; }

    public required string NearestRecordId { get; init; }

    public required string NearestRepositoryId { get; init; }

    public required string NearestWorkItemId { get; init; }

    public required int NearestProfileTrainingObservationCount { get; init; }

    public required int NearestProfileTrainingRepositoryCount { get; init; }
}

public sealed record CalibrationUncertaintySupportWorkItem
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string WorkItemId { get; init; }

    public required EffortCategory Category { get; init; }

    public required string ExpectedSizeBand { get; init; }

    public required ComplexityLevel SourceComplexity { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required CalibrationUncertaintySupportLevel SelectedSupportLevel { get; init; }

    public required bool SupportSufficient { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportCell> SupportCells { get; init; } = [];

    public required CalibrationUncertaintyOutOfDistribution OutOfDistribution { get; init; }
}

public sealed record CalibrationUncertaintySupportLevelSummary
{
    public required CalibrationUncertaintySupportLevel Level { get; init; }

    public required int WorkItemCount { get; init; }
}

public sealed record CalibrationUncertaintySupportRepositorySummary
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required string FeatureReportDigest { get; init; }

    public required string EstimateDigest { get; init; }

    public required string EvidenceDigest { get; init; }

    public required int WorkItemCount { get; init; }

    public required int UniqueProfileCount { get; init; }

    public required int ExactProfileMatchWorkItemCount { get; init; }

    public required int InsufficientSupportWorkItemCount { get; init; }

    public required decimal MeanOutOfDistributionScore { get; init; }

    public required decimal P90OutOfDistributionScore { get; init; }

    public required decimal MaximumOutOfDistributionScore { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportLevelSummary> SupportLevels
    { get; init; } = [];
}

public sealed record CalibrationUncertaintySupportSummary
{
    public required int RepositoryCount { get; init; }

    public required int FeatureReportCount { get; init; }

    public required int WorkItemCount { get; init; }

    public required int UniqueProfileCount { get; init; }

    public required int ProfileEvaluationCount { get; init; }

    public required long ProfileComparisonCount { get; init; }

    public required int ExactProfileMatchWorkItemCount { get; init; }

    public required int InsufficientSupportWorkItemCount { get; init; }

    public required decimal MeanOutOfDistributionScore { get; init; }

    public required decimal P90OutOfDistributionScore { get; init; }

    public required decimal MaximumOutOfDistributionScore { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportLevelSummary> SupportLevels
    { get; init; } = [];
}

public sealed record CalibrationUncertaintySupportProfile
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string ProfilerVersion { get; init; }

    public required string PopulationId { get; init; }

    public required string PopulationVersion { get; init; }

    public required string PopulationDigest { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string FeatureContractVersion { get; init; }

    public required string FeatureContractDigest { get; init; }

    public IReadOnlyList<string> ProjectorVersions { get; init; } = [];

    public IReadOnlyList<string> EstimatorVersions { get; init; } = [];

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationUncertaintySupportPolicy Policy { get; init; }

    public required CalibrationUncertaintySupportSummary Summary { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportRepositorySummary> Repositories
    { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintySupportWorkItem> WorkItems { get; init; } = [];
}
