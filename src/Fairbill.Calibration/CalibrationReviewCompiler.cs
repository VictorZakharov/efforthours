using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationReviewCompiler
{
    public const string CompilerVersion = "calibration-review-compiler/0.2.0";

    private const string LegacyCompilerVersion = "calibration-review-compiler/0.1.0";

    public static CalibrationCorpus Compile(
        CalibrationReviewPlan plan,
        IReadOnlyList<EstimateReport> sourceEstimates)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sourceEstimates);

        List<string> errors = [.. ContractValidation.Validate(plan)];
        if (!IsSupportedCompilerVersion(plan.CompilerVersion))
        {
            errors.Add(
                $"Review plan requires compiler '{plan.CompilerVersion}', but this build provides " +
                $"'{CompilerVersion}' and compatibility for '{LegacyCompilerVersion}'.");
        }
        else if (string.Equals(
                     plan.CompilerVersion,
                     LegacyCompilerVersion,
                     StringComparison.Ordinal) &&
                 plan.Records.SelectMany(record => record.Capabilities)
                     .SelectMany(capability => capability.Targets)
                     .Any(target => target.Hours.Expected == 0m))
        {
            errors.Add(
                $"Review plan compiler '{LegacyCompilerVersion}' does not support explicit " +
                $"zero-hour exclusions; use '{CompilerVersion}'.");
        }

        Dictionary<CandidateKey, EstimateReport> candidates = [];
        foreach (EstimateReport estimate in sourceEstimates)
        {
            errors.AddRange(ContractValidation.Validate(estimate).Select(error =>
                $"Source estimate '{estimate.Repository.Name}/{estimate.Profile}': {error}"));

            if (string.IsNullOrWhiteSpace(estimate.Repository.SourceDigest))
            {
                errors.Add(
                    $"Source estimate '{estimate.Repository.Name}/{estimate.Profile}' requires " +
                    "repository.sourceDigest.");
                continue;
            }

            CandidateKey key = new(
                estimate.Repository.SourceDigest,
                estimate.Profile,
                estimate.Baseline.Id);
            if (!candidates.TryAdd(key, estimate))
            {
                errors.Add(
                    $"More than one source estimate matches digest '{key.SourceDigest}', " +
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
            CandidateKey key = new(
                planned.Repository.SourceDigest,
                planned.Profile,
                planned.BaselineId);
            if (!candidates.TryGetValue(key, out EstimateReport? estimate))
            {
                errors.Add(
                    $"No source estimate matches review-plan record '{planned.Id}' with digest " +
                    $"'{key.SourceDigest}', profile '{key.Profile}', and baseline '{key.BaselineId}'.");
                continue;
            }

            string actualDigest = CalibrationDigest.Compute(estimate);
            if (!string.Equals(
                    planned.SourceEstimatorVersion,
                    estimate.EstimatorVersion,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' expects estimator " +
                    $"'{planned.SourceEstimatorVersion}', but the source estimate uses " +
                    $"'{estimate.EstimatorVersion}'.");
            }

            if (!string.Equals(planned.SourceEstimateDigest, actualDigest, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Review-plan record '{planned.Id}' expects estimate digest " +
                    $"'{planned.SourceEstimateDigest}', but received '{actualDigest}'.");
            }

            IReadOnlyList<CalibrationTarget> targets = CalibrationTargetCompiler.Compile(
                planned.Id,
                planned.Capabilities,
                estimate.WorkItems,
                errors);

            records.Add(new CalibrationRecord
            {
                Id = planned.Id,
                Repository = planned.Repository,
                Change = planned.Change,
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
                $"Compiled corpus: {error}")]);
        }

        return corpus;
    }

    private readonly record struct CandidateKey(
        string SourceDigest,
        EstimationProfile Profile,
        string BaselineId);

    private static bool IsSupportedCompilerVersion(string version) =>
        string.Equals(version, CompilerVersion, StringComparison.Ordinal) ||
        string.Equals(version, LegacyCompilerVersion, StringComparison.Ordinal);
}
