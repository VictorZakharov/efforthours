using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(CalibrationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        List<string> errors = [];
        RequireVersion(corpus.SchemaVersion, "calibration corpus", errors);
        RequireText(corpus.Id, "corpus.id", errors);
        RequireText(corpus.Version, "corpus.version", errors);
        RequireText(corpus.Description, "corpus.description", errors);
        RequireText(corpus.Rubric.Id, "corpus.rubric.id", errors);
        RequireText(corpus.Rubric.Version, "corpus.rubric.version", errors);

        if (corpus.Records.Count == 0)
        {
            errors.Add("The calibration corpus must contain at least one record.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        Dictionary<string, CalibrationPartition> repositoryPartitions = new(StringComparer.Ordinal);
        Dictionary<string, (string RepositoryId, CalibrationPartition Partition)> digestOwners =
            new(StringComparer.Ordinal);
        HashSet<(string SourceDigest, EstimationProfile Profile, string BaselineId)> matchKeys = [];

        foreach (CalibrationRecord record in corpus.Records)
        {
            string path = $"record[{record.Id}]";
            RequireText(record.Id, "record.id", errors);
            if (!recordIds.Add(record.Id))
            {
                errors.Add($"Calibration record ID '{record.Id}' is duplicated.");
            }

            RequireText(record.Repository.Id, $"{path}.repository.id", errors);
            RequireText(record.Repository.Name, $"{path}.repository.name", errors);
            RequireDigest(record.Repository.SourceDigest, $"{path}.repository.sourceDigest", errors);
            ValidateChangeCalibrationReference(
                record.Change,
                record.Repository,
                corpus.Rubric.Id,
                path,
                errors);
            RequireText(record.BaselineId, $"{path}.baselineId", errors);
            RequireText(record.SourceEstimatorVersion, $"{path}.sourceEstimatorVersion", errors);
            RequireDigest(record.SourceEstimateDigest, $"{path}.sourceEstimateDigest", errors);

            if (repositoryPartitions.TryGetValue(
                    record.Repository.Id,
                    out CalibrationPartition existingPartition) &&
                existingPartition != record.Partition)
            {
                errors.Add(
                    $"Repository '{record.Repository.Id}' occurs in both " +
                    $"'{existingPartition}' and '{record.Partition}' partitions.");
            }
            else
            {
                repositoryPartitions[record.Repository.Id] = record.Partition;
            }

            if (digestOwners.TryGetValue(
                    record.Repository.SourceDigest,
                    out (string RepositoryId, CalibrationPartition Partition) owner) &&
                (!string.Equals(owner.RepositoryId, record.Repository.Id, StringComparison.Ordinal) ||
                 owner.Partition != record.Partition))
            {
                errors.Add(
                    $"Source digest '{record.Repository.SourceDigest}' is assigned to conflicting " +
                    "repository identities or partitions.");
            }
            else
            {
                digestOwners[record.Repository.SourceDigest] =
                    (record.Repository.Id, record.Partition);
            }

            if (!matchKeys.Add((record.Repository.SourceDigest, record.Profile, record.BaselineId)))
            {
                errors.Add(
                    $"More than one calibration record matches source digest " +
                    $"'{record.Repository.SourceDigest}', profile '{record.Profile}', and " +
                    $"baseline '{record.BaselineId}'.");
            }

            ValidateCalibrationSource(record.Source, path, errors);
            ValidateCalibrationReview(record.Review, path, errors);
            ValidateCalibrationTargets(record, path, errors);
        }

        ValidateCalibrationSubjectConsistency(
            corpus.Rubric.Id,
            corpus.Records.Select(record => record.Change),
            "corpus",
            errors);

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationValidationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        List<string> errors = [];
        RequireVersion(summary.SchemaVersion, "calibration validation summary", errors);
        RequireText(summary.CorpusId, "corpusId", errors);
        RequireText(summary.CorpusVersion, "corpusVersion", errors);
        if (!summary.Valid)
        {
            errors.Add("A successful calibration validation summary must be valid.");
        }

        if (summary.RecordCount <= 0 || summary.RepositoryCount <= 0)
        {
            errors.Add("Calibration validation counts must be positive.");
        }

        HashSet<CalibrationPartition> partitions = [];
        foreach (CalibrationPartitionSummary partition in summary.Partitions)
        {
            if (!partitions.Add(partition.Partition))
            {
                errors.Add($"Partition summary '{partition.Partition}' is duplicated.");
            }

            if (partition.RecordCount <= 0 || partition.RepositoryCount <= 0)
            {
                errors.Add($"Partition '{partition.Partition}' counts must be positive.");
            }
        }

        if (summary.Partitions.Sum(partition => partition.RecordCount) != summary.RecordCount)
        {
            errors.Add("Partition record counts do not equal the validation record count.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(CalibrationEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "calibration evaluation report", errors);
        RequireText(report.EvaluatorVersion, "evaluatorVersion", errors);
        RequireText(report.MetricVersion, "metricVersion", errors);
        RequireText(report.CorpusId, "corpusId", errors);
        RequireText(report.CorpusVersion, "corpusVersion", errors);
        if (report.RecordCount <= 0 || report.RepositoryCount <= 0)
        {
            errors.Add("Calibration evaluation record and repository counts must be positive.");
        }

        if (report.IgnoredCandidateCount < 0)
        {
            errors.Add("ignoredCandidateCount cannot be negative.");
        }

        RequireUniqueText(report.CandidateEstimatorVersions, "candidateEstimatorVersions", errors);
        if (report.CandidateEstimatorVersions.Count == 0)
        {
            errors.Add("At least one candidate estimator version is required.");
        }

        ValidateCalibrationMetrics(report.RepositoryTotals, "repositoryTotals", errors);
        ValidateCalibrationMetrics(report.WorkItems, "workItems", errors);

        HashSet<EffortCategory> categories = [];
        foreach (CalibrationCategoryMetrics category in report.Categories)
        {
            if (!categories.Add(category.Category))
            {
                errors.Add($"Calibration category '{category.Category}' is duplicated.");
            }

            ValidateCalibrationMetrics(category.Metrics, $"category[{category.Category}]", errors);
        }

        if (report.Repositories.Count != report.RecordCount)
        {
            errors.Add("Repository evaluation count does not equal recordCount.");
        }

        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationRepositoryEvaluation repository in report.Repositories)
        {
            RequireText(repository.RecordId, "repository.recordId", errors);
            RequireText(repository.RepositoryId, $"repository[{repository.RecordId}].repositoryId", errors);
            RequireDigest(repository.SourceDigest, $"repository[{repository.RecordId}].sourceDigest", errors);
            RequireText(repository.BaselineId, $"repository[{repository.RecordId}].baselineId", errors);
            RequireText(
                repository.CandidateEstimatorVersion,
                $"repository[{repository.RecordId}].candidateEstimatorVersion",
                errors);
            RequireDigest(
                repository.CandidateEstimateDigest,
                $"repository[{repository.RecordId}].candidateEstimateDigest",
                errors);
            ValidateRange(repository.ReviewedTotal, $"repository[{repository.RecordId}].reviewedTotal", errors);
            ValidateRange(repository.CandidateTotal, $"repository[{repository.RecordId}].candidateTotal", errors);

            if (!recordIds.Add(repository.RecordId))
            {
                errors.Add($"Repository evaluation record ID '{repository.RecordId}' is duplicated.");
            }

            if (repository.ExpectedAbsoluteErrorHours < 0m)
            {
                errors.Add($"Repository '{repository.RecordId}' absolute error cannot be negative.");
            }

            if (repository.TargetCount < 0 ||
                repository.MatchedTargetCount < 0 ||
                repository.MatchedTargetCount > repository.TargetCount ||
                repository.CandidateWorkItemCount < 0 ||
                repository.MatchedCandidateWorkItemCount < 0 ||
                repository.MatchedCandidateWorkItemCount > repository.CandidateWorkItemCount)
            {
                errors.Add($"Repository '{repository.RecordId}' contains invalid match counts.");
            }

            RequireUniqueText(repository.UnmatchedTargetIds, $"repository[{repository.RecordId}].unmatchedTargetIds", errors);
            RequireUniqueText(
                repository.UnmatchedCandidateWorkItemIds,
                $"repository[{repository.RecordId}].unmatchedCandidateWorkItemIds",
                errors);
            RequireUniqueText(
                repository.CategoryMismatchTargetIds,
                $"repository[{repository.RecordId}].categoryMismatchTargetIds",
                errors);
        }

        if (report.Repositories.Select(repository => repository.RepositoryId)
                .Distinct(StringComparer.Ordinal)
                .Count() != report.RepositoryCount)
        {
            errors.Add("Distinct repository evaluation count does not equal repositoryCount.");
        }

        ValidateMatchSummary(report.Match, errors);
        if (report.Match.TargetCount != report.Repositories.Sum(repository => repository.TargetCount) ||
            report.Match.MatchedTargetCount != report.Repositories.Sum(repository => repository.MatchedTargetCount) ||
            report.Match.CandidateWorkItemCount != report.Repositories.Sum(repository => repository.CandidateWorkItemCount) ||
            report.Match.MatchedCandidateWorkItemCount !=
                report.Repositories.Sum(repository => repository.MatchedCandidateWorkItemCount))
        {
            errors.Add("Aggregate match counts do not equal repository match counts.");
        }

        if (report.RepositoryTotals.Expected.SampleCount != report.RecordCount ||
            report.WorkItems.Expected.SampleCount != report.Match.MatchedTargetCount)
        {
            errors.Add("Metric sample counts do not match their evaluation populations.");
        }

        return errors;
    }

}
