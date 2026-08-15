using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed record CalibrationUncertaintySupportInput
{
    public required CalibrationUncertaintySupportPopulationRepository Population { get; init; }

    public required CalibrationUncertaintyFeatureReport Report { get; init; }
}

internal sealed record CalibrationUncertaintySupportFeaturePoint
{
    public required string Id { get; init; }

    public required CalibrationUncertaintyFeatureAvailability Availability { get; init; }

    public required string BucketId { get; init; }

    public required int BucketOrder { get; init; }

    public required int MaximumOrder { get; init; }
}

internal sealed record CalibrationUncertaintySupportObservation
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string WorkItemId { get; init; }

    public required EffortCategory Category { get; init; }

    public required string ExpectedSizeBand { get; init; }

    public required ComplexityLevel SourceComplexity { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required string EcosystemKey { get; init; }

    public IReadOnlyList<CalibrationUncertaintySupportFeaturePoint> Features { get; init; } = [];

    public required string ProfileSignature { get; init; }

    public required string ProfileDigest { get; init; }
}

internal sealed record CalibrationUncertaintySupportProfileGroup
{
    public required string RepositoryId { get; init; }

    public required string ProfileSignature { get; init; }

    public required int ObservationCount { get; init; }

    public required CalibrationUncertaintySupportObservation Representative { get; init; }
}

internal sealed record CalibrationUncertaintyOutOfDistributionResult
{
    public required decimal Score { get; init; }

    public required decimal StructuralDistance { get; init; }

    public required decimal FeatureDistance { get; init; }

    public required int ExactObservationCount { get; init; }

    public required int ExactRepositoryCount { get; init; }

    public required CalibrationUncertaintySupportObservation Nearest { get; init; }

    public required int NearestProfileObservationCount { get; init; }

    public required int NearestProfileRepositoryCount { get; init; }
}

internal readonly record struct CalibrationUncertaintyExactSupportKey(
    EffortCategory Category,
    string ExpectedSizeBand,
    string EcosystemKey,
    ComplexityLevel SourceComplexity);

internal readonly record struct CalibrationUncertaintyCategorySizeEcosystemKey(
    EffortCategory Category,
    string ExpectedSizeBand,
    string EcosystemKey);

internal readonly record struct CalibrationUncertaintyCategorySizeKey(
    EffortCategory Category,
    string ExpectedSizeBand);

internal readonly record struct CalibrationUncertaintySupportCount(
    int ObservationCount,
    int RepositoryCount);

internal readonly record struct CalibrationUncertaintySupportProfileGroupKey(
    string RepositoryId,
    string ProfileSignature);

internal sealed record CalibrationUncertaintySupportDistanceEvaluation
{
    public IReadOnlyDictionary<CalibrationUncertaintySupportProfileGroupKey,
        CalibrationUncertaintyOutOfDistributionResult> Results
    { get; init; } =
        new Dictionary<CalibrationUncertaintySupportProfileGroupKey,
            CalibrationUncertaintyOutOfDistributionResult>();

    public required int UniqueProfileCount { get; init; }

    public required int ProfileEvaluationCount { get; init; }

    public required long ProfileComparisonCount { get; init; }
}
