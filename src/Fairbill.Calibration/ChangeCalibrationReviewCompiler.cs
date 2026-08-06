using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class ChangeCalibrationReviewCompiler
{
    public const string CompilerVersion = "change-calibration-review-compiler/0.1.0";

    public static CalibrationCorpus Compile(
        CalibrationReviewPlan plan,
        IReadOnlyList<ChangeEstimateReport> sourceEstimates)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sourceEstimates);

        List<string> errors = [.. ContractValidation.Validate(plan)];
        if (!string.Equals(plan.CompilerVersion, CompilerVersion, StringComparison.Ordinal))
        {
            errors.Add(
                $"Review plan requires compiler '{plan.CompilerVersion}', but this build provides " +
                $"'{CompilerVersion}'.");
        }

        Dictionary<CandidateKey, ChangeEstimateReport> candidates = [];
        foreach (ChangeEstimateReport estimate in sourceEstimates)
        {
            errors.AddRange(ContractValidation.Validate(estimate).Select(error =>
                $"Source change estimate '{estimate.Repository.Name}/{estimate.Profile}': {error}"));
            string finalDeltaDigest = ChangeCalibrationIdentity.ComputeFinalDeltaDigest(estimate);
            CandidateKey key = new(finalDeltaDigest, estimate.Profile, estimate.Baseline.Id);
            if (!candidates.TryAdd(key, estimate))
            {
                errors.Add(
                    $"More than one source change estimate matches final delta '{key.SourceDigest}', " +
                    $"profile '{key.Profile}', and baseline '{key.BaselineId}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        List<CalibrationRecord> records = [];
        foreach (CalibrationReviewPlanRecord planned in plan.Records
                     .OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            ChangeCalibrationReference change = planned.Change!;
            CandidateKey key = new(
                planned.Repository.SourceDigest,
                planned.Profile,
                planned.BaselineId);
            if (!candidates.TryGetValue(key, out ChangeEstimateReport? estimate))
            {
                errors.Add(
                    $"No source change estimate matches review-plan record '{planned.Id}' with final " +
                    $"delta '{key.SourceDigest}', profile '{key.Profile}', and baseline '{key.BaselineId}'.");
                continue;
            }

            ChangeCalibrationReference actual = ChangeCalibrationIdentity.CreateReference(
                estimate,
                change.Id,
                change.CoverageTags);
            ValidateReference(planned.Id, change, actual, errors);

            string actualDigest = CalibrationDigest.Compute(estimate);
            if (!string.Equals(
                    planned.SourceEstimatorVersion,
                    estimate.EstimatorVersion,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' expects estimator " +
                    $"'{planned.SourceEstimatorVersion}', but the source change estimate uses " +
                    $"'{estimate.EstimatorVersion}'.");
            }

            if (!string.Equals(planned.SourceEstimateDigest, actualDigest, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' expects estimate digest " +
                    $"'{planned.SourceEstimateDigest}', but received '{actualDigest}'.");
            }

            int beforeTargets = errors.Count;
            IReadOnlyList<CalibrationTarget> targets = CalibrationTargetCompiler.Compile(
                planned.Id,
                planned.Capabilities,
                estimate.WorkItems,
                errors);
            if (errors.Count > beforeTargets)
            {
                continue;
            }

            records.Add(new CalibrationRecord
            {
                Id = planned.Id,
                Repository = planned.Repository,
                Change = change,
                Profile = planned.Profile,
                BaselineId = planned.BaselineId,
                Partition = planned.Partition,
                SourceEstimatorVersion = planned.SourceEstimatorVersion,
                SourceEstimateDigest = planned.SourceEstimateDigest,
                Source = planned.Source,
                Review = planned.Review,
                Targets = targets,
            });
        }

        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }

        CalibrationCorpus corpus = new()
        {
            Id = plan.Id,
            Version = plan.Version,
            Description = plan.Description,
            Rubric = plan.Rubric,
            Records = records,
        };
        IReadOnlyList<string> corpusErrors = ContractValidation.Validate(corpus);
        if (corpusErrors.Count > 0)
        {
            throw new CalibrationEvaluationException([.. corpusErrors.Select(error =>
                $"Compiled Change calibration corpus: {error}")]);
        }

        return corpus;
    }

    private static void ValidateReference(
        string recordId,
        ChangeCalibrationReference expected,
        ChangeCalibrationReference actual,
        List<string> errors)
    {
        if (expected.SelectionKind != actual.SelectionKind ||
            !string.Equals(expected.BaseObjectId, actual.BaseObjectId, StringComparison.Ordinal) ||
            !string.Equals(expected.HeadObjectId, actual.HeadObjectId, StringComparison.Ordinal) ||
            !string.Equals(expected.BaseEvidenceDigest, actual.BaseEvidenceDigest, StringComparison.Ordinal) ||
            !string.Equals(expected.HeadEvidenceDigest, actual.HeadEvidenceDigest, StringComparison.Ordinal) ||
            !string.Equals(expected.FinalDeltaDigest, actual.FinalDeltaDigest, StringComparison.Ordinal))
        {
            errors.Add(
                $"Review-plan record '{recordId}' Change provenance does not match the source estimate.");
        }
    }

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);
}
