using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal sealed record CalibrationUncertaintySupportEvaluationData
{
    public required CalibrationUncertaintyEvaluationReport SourceEvaluation { get; init; }

    public IReadOnlyList<CalibrationUncertaintyObservation> Observations { get; init; } = [];

    public IReadOnlyList<CalibrationUncertaintySupportTargetEvaluation> Targets { get; init; } = [];

    public required int MatchedSupportWorkItemReferenceCount { get; init; }
}
