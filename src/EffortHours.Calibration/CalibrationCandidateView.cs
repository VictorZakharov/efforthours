using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed record CalibrationCandidateView
{
    public required string SourceDigest { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required string EstimatorVersion { get; init; }

    public required string EstimateDigest { get; init; }

    public required EffortRange TotalEffort { get; init; }

    public required IReadOnlyList<CategoryEstimate> Categories { get; init; }

    public required IReadOnlyList<WorkItem> WorkItems { get; init; }
}
