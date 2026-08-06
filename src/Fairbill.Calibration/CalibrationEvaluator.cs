using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Calibration;

public static class CalibrationEvaluator
{
    public const string EvaluatorVersion = "calibration-evaluator/0.1.0";
    public const string MetricVersion = "calibration-metrics/1.0.0";

    public static CalibrationValidationSummary Summarize(CalibrationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ThrowIfInvalid(corpus);

        CalibrationValidationSummary summary = new()
        {
            CorpusId = corpus.Id,
            CorpusVersion = corpus.Version,
            Valid = true,
            RecordCount = corpus.Records.Count,
            RepositoryCount = corpus.Records
                .Select(record => record.Repository.Id)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            Partitions = [.. corpus.Records
                .GroupBy(record => record.Partition)
                .OrderBy(group => group.Key)
                .Select(group => new CalibrationPartitionSummary
                {
                    Partition = group.Key,
                    RecordCount = group.Count(),
                    RepositoryCount = group
                        .Select(record => record.Repository.Id)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                })],
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(summary);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Calibration validation summary is invalid: {string.Join("; ", errors)}");
        }

        return summary;
    }

    public static CalibrationEvaluationReport Evaluate(
        CalibrationCorpus corpus,
        IReadOnlyList<EstimateReport> candidates,
        CalibrationPartition partition)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(candidates);
        ThrowIfInvalid(corpus);

        List<string> errors = [];
        List<CalibrationCandidateView> views = [];
        foreach (EstimateReport candidate in candidates)
        {
            errors.AddRange(ContractValidation.Validate(candidate).Select(error =>
                $"Candidate '{candidate.Repository.Name}/{candidate.Profile}': {error}"));
            if (string.IsNullOrWhiteSpace(candidate.Repository.SourceDigest))
            {
                errors.Add(
                    $"Candidate '{candidate.Repository.Name}/{candidate.Profile}' requires " +
                    "repository.sourceDigest.");
                continue;
            }

            views.Add(new CalibrationCandidateView
            {
                SourceDigest = candidate.Repository.SourceDigest,
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
            expectChangeRecords: false,
            EvaluatorVersion,
            MetricVersion);
    }

    internal static void ThrowIfInvalid(CalibrationCorpus corpus)
    {
        IReadOnlyList<string> errors = ContractValidation.Validate(corpus);
        if (errors.Count > 0)
        {
            throw new CalibrationEvaluationException(errors);
        }
    }
}
