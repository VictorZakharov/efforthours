using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed record CalibrationUncertaintyAggregatedFeature
{
    public required CalibrationUncertaintyFeatureAvailability Availability { get; init; }

    public decimal? Value { get; init; }
}

internal sealed record CalibrationUncertaintyObservation
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string TargetId { get; init; }

    public required EffortCategory Category { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required string ExpectedSizeBand { get; init; }

    public required EffortRange CandidateRange { get; init; }

    public required EffortRange ReviewedRange { get; init; }

    public required decimal AbsoluteResidualHours { get; init; }

    public required decimal NormalizedAbsoluteResidual { get; init; }

    public IReadOnlyDictionary<string, CalibrationUncertaintyAggregatedFeature> Features
    { get; init; } = new Dictionary<string, CalibrationUncertaintyAggregatedFeature>();

    public IReadOnlyList<string> GraphNodeIds { get; init; } = [];

    public EffortRange? BaselineRange { get; init; }
}

internal sealed record CalibrationUncertaintyMatchedData
{
    public IReadOnlyList<CalibrationUncertaintyObservation> Observations { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintyRepositoryMatch> Repositories { get; init; } = [];

    public required int TargetCount { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }
}

internal sealed record CalibrationUncertaintyRepositoryMatch
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required string FeatureReportDigest { get; init; }

    public required string EstimateDigest { get; init; }

    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }

    public IReadOnlyList<string> UnmatchedTargetIds { get; init; } = [];

    public IReadOnlyList<string> CategoryMismatchTargetIds { get; init; } = [];
}

internal readonly record struct CalibrationUncertaintyBucket(string Id, int Order);

internal readonly record struct CalibrationUncertaintyPrediction(
    EffortRange Range,
    bool FeatureConditioned);
