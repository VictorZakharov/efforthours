using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class ChangeCalibrationEvaluator
{
    public const string EvaluatorVersion = "change-calibration-evaluator/0.1.0";

    public static CalibrationEvaluationReport Evaluate(
        CalibrationCorpus corpus,
        IReadOnlyList<ChangeEstimateReport> candidates,
        CalibrationPartition partition)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(candidates);
        CalibrationEvaluator.ThrowIfInvalid(corpus);

        List<string> errors = [];
        List<CalibrationCandidateView> views = [];
        foreach (ChangeEstimateReport candidate in candidates)
        {
            errors.AddRange(ContractValidation.Validate(candidate).Select(error =>
                $"Change candidate '{candidate.Repository.Name}/{candidate.Profile}': {error}"));
            views.Add(new CalibrationCandidateView
            {
                SourceDigest = ChangeCalibrationIdentity.ComputeFinalDeltaDigest(candidate),
                Profile = candidate.Profile,
                BaselineId = candidate.Baseline.Id,
                EstimatorVersion = candidate.EstimatorVersion,
                EstimateDigest = CalibrationDigest.Compute(candidate),
                TotalEffort = candidate.TotalEffort,
                Categories = candidate.Categories,
                WorkItems = candidate.WorkItems,
            });
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        return CalibrationEvaluationEngine.Evaluate(
            corpus,
            views,
            partition,
            expectChangeRecords: true,
            EvaluatorVersion,
            CalibrationEvaluator.MetricVersion);
    }
}
