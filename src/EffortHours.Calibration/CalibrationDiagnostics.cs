using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static class CalibrationDiagnostics
{
    public const string DiagnosticVersion = "calibration-residual-diagnostic/1.0.0";

    public const decimal DefaultMaterialContributionThreshold = 0.8m;

    public static CalibrationDiagnosticReport Diagnose(
        CalibrationCorpus corpus,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationPartition partition,
        decimal materialContributionThreshold = DefaultMaterialContributionThreshold)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(candidates);
        if (materialContributionThreshold is <= 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(materialContributionThreshold),
                materialContributionThreshold,
                "The material contribution threshold must be greater than zero and at most one.");
        }

        CalibrationEvaluator.ThrowIfInvalid(corpus);
        IReadOnlyList<CalibrationCandidateView> views =
            CalibrationEvaluator.CreateCandidateViews(candidates);
        return CalibrationDiagnosticEngine.Diagnose(
            corpus,
            views,
            partition,
            materialContributionThreshold,
            DiagnosticVersion);
    }
}
