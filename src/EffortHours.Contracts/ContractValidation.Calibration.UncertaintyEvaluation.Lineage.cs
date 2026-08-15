using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateUncertaintyEvaluationProtocol(
        CalibrationUncertaintyEvaluationProtocol protocol,
        List<string> errors)
    {
        if (protocol.Version != CalibrationUncertaintyVersions.EvaluationProtocolV1 ||
            protocol.Partition != CalibrationPartition.Development ||
            protocol.FoldUnit != "repository" ||
            protocol.TargetMetric != "absolute-reviewed-expected-residual" ||
            protocol.Normalization != "candidate-expected-with-0.5-hour-floor" ||
            protocol.NormalizationFloorHours != 0.5m ||
            protocol.IntendedCoverageTarget != 0.80m ||
            protocol.QuantileMethod != "nearest-rank" ||
            protocol.MinimumBucketObservationCount != 3 ||
            protocol.MinimumBucketRepositoryCount != 2 ||
            !protocol.RepositoryIsolated ||
            !protocol.DevelopmentOnly ||
            protocol.FitsProductionModel ||
            protocol.FormalProbabilityInterval)
        {
            errors.Add("Uncertainty evaluation protocol does not satisfy v1 development-only invariants.");
        }
    }

    private static void ValidateUncertaintyEvaluationRepositories(
        CalibrationUncertaintyEvaluationReport report,
        List<string> errors)
    {
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyRepositoryEvaluation repository in report.Repositories)
        {
            string path = $"repository[{repository.RecordId}]";
            RequireText(repository.RecordId, "repository.recordId", errors);
            RequireText(repository.RepositoryId, $"{path}.repositoryId", errors);
            RequireDigest(repository.SourceDigest, $"{path}.sourceDigest", errors);
            RequireDigest(repository.FeatureReportDigest, $"{path}.featureReportDigest", errors);
            RequireDigest(repository.EstimateDigest, $"{path}.estimateDigest", errors);
            RequireUniqueText(repository.UnmatchedTargetIds, $"{path}.unmatchedTargetIds", errors);
            RequireUniqueText(
                repository.CategoryMismatchTargetIds,
                $"{path}.categoryMismatchTargetIds",
                errors);
            if (!recordIds.Add(repository.RecordId))
            {
                errors.Add($"Repository evaluation record '{repository.RecordId}' is duplicated.");
            }

            if (repository.TargetCount < 0 ||
                repository.MatchedTargetCount < 0 ||
                repository.MatchedTargetCount > repository.TargetCount ||
                repository.SourceWorkItemReferenceCount < 0 ||
                repository.MatchedSourceWorkItemReferenceCount < 0 ||
                repository.MatchedSourceWorkItemReferenceCount >
                    repository.SourceWorkItemReferenceCount ||
                repository.UnmatchedTargetIds.Count !=
                    repository.TargetCount - repository.MatchedTargetCount ||
                repository.CategoryMismatchTargetIds.Any(id =>
                    !repository.UnmatchedTargetIds.Contains(id, StringComparer.Ordinal)))
            {
                errors.Add($"{path} contains inconsistent match counts.");
            }

            CalibrationUncertaintyTargetEvaluation[] targets = [.. report.Targets.Where(target =>
                target.RecordId == repository.RecordId)];
            ValidateUncertaintyEvaluationPerformance(
                repository.CurrentIntervals,
                $"{path}.currentIntervals",
                errors);
            ValidateUncertaintyEvaluationPerformance(
                repository.CrossValidatedBaseline,
                $"{path}.crossValidatedBaseline",
                errors);
            if (repository.MatchedTargetCount != targets.Length ||
                repository.CurrentIntervals != BuildUncertaintyEvaluationPerformance(
                    targets,
                    useBaseline: false) ||
                repository.CrossValidatedBaseline != BuildUncertaintyEvaluationPerformance(
                    targets,
                    useBaseline: true))
            {
                errors.Add($"{path} interval metrics do not reconcile to its target rows.");
            }
        }
    }

    private static void ValidateUncertaintyEvaluationTargets(
        CalibrationUncertaintyEvaluationReport report,
        List<string> errors)
    {
        HashSet<(string RecordId, string TargetId)> ids = [];
        Dictionary<string, CalibrationUncertaintyRepositoryEvaluation> repositories =
            new(StringComparer.Ordinal);
        foreach (CalibrationUncertaintyRepositoryEvaluation repository in report.Repositories)
        {
            repositories.TryAdd(repository.RecordId, repository);
        }
        foreach (CalibrationUncertaintyTargetEvaluation target in report.Targets)
        {
            string path = $"target[{target.RecordId}/{target.TargetId}]";
            RequireText(target.RecordId, "target.recordId", errors);
            RequireText(target.RepositoryId, $"{path}.repositoryId", errors);
            RequireText(target.TargetId, $"{path}.targetId", errors);
            RequireText(target.ExpectedSizeBand, $"{path}.expectedSizeBand", errors);
            RequireUniqueText(target.Ecosystems, $"{path}.ecosystems", errors);
            ValidateRange(target.CandidateRange, $"{path}.candidateRange", errors);
            ValidateRange(target.ReviewedRange, $"{path}.reviewedRange", errors);
            ValidateRange(
                target.CrossValidatedBaselineRange,
                $"{path}.crossValidatedBaselineRange",
                errors);
            if (!IsSymmetricWithZeroFloor(target.CrossValidatedBaselineRange))
            {
                errors.Add($"{path}.crossValidatedBaselineRange must be symmetric except for zero floor.");
            }

            if (!ids.Add((target.RecordId, target.TargetId)))
            {
                errors.Add($"Uncertainty target '{target.RecordId}/{target.TargetId}' is duplicated.");
            }

            if (!repositories.TryGetValue(
                    target.RecordId,
                    out CalibrationUncertaintyRepositoryEvaluation? repository) ||
                repository.RepositoryId != target.RepositoryId)
            {
                errors.Add($"{path} does not match a repository row.");
            }

            decimal residual = decimal.Abs(
                target.CandidateRange.Expected - target.ReviewedRange.Expected);
            decimal normalized = decimal.Round(
                residual / decimal.Max(0.5m, target.CandidateRange.Expected),
                6,
                MidpointRounding.AwayFromZero);
            if (target.AbsoluteExpectedResidualHours != RoundUncertaintyEvaluation(residual) ||
                target.NormalizedAbsoluteResidual != RoundUncertaintyEvaluation(normalized) ||
                target.CurrentReviewedExpectedCovered != ContainsReviewedExpected(
                    target.CandidateRange,
                    target.ReviewedRange.Expected) ||
                target.BaselineReviewedExpectedCovered != ContainsReviewedExpected(
                    target.CrossValidatedBaselineRange,
                    target.ReviewedRange.Expected))
            {
                errors.Add($"{path} residual or coverage values are inconsistent.");
            }
        }
    }
}
