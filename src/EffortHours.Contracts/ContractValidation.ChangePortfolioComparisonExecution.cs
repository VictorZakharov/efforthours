using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateComparisonExecution(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        RequireText(report.Execution.RuntimeFramework, "execution.runtimeFramework", errors);
        RequireText(report.Execution.OperatingSystemFamily, "execution.operatingSystemFamily", errors);
        RequireText(report.Execution.ProcessArchitecture, "execution.processArchitecture", errors);
        if (report.Execution.LogicalProcessorCount <= 0)
        {
            errors.Add("execution.logicalProcessorCount must be positive.");
        }

        if (report.Execution.ShardPolicy !=
            ChangePortfolioComparisonPolicies.RepositoryEvidenceShardsV1)
        {
            errors.Add("The comparison report uses an unsupported repository-shard policy.");
        }

        IReadOnlyList<ChangePortfolioAuthorPeriodManifestRepository>? selectedRepositories =
            report.Selection.AuthorPeriodManifest?.Repositories;
        if (selectedRepositories is null ||
            report.Execution.RepositoryShardCount != selectedRepositories.Count ||
            !report.Execution.Repositories.Select(repository => repository.RepositoryId)
                .SequenceEqual(selectedRepositories.Select(repository => repository.Id), StringComparer.Ordinal))
        {
            errors.Add("Execution repository shards must match the canonical source selection.");
        }

        foreach (ChangePortfolioComparisonRepositoryExecution repository in report.Execution.Repositories)
        {
            ValidateRepositoryExecution(repository, errors);
        }

        if (report.SourcePortfolio is not null &&
            report.Execution.Repositories.Sum(repository => repository.SelectedChangeCount) !=
            report.SourcePortfolio.Items.Count)
        {
            errors.Add("Execution repository selected-change counts must match the source portfolio.");
        }

        ValidateCheckpoint(report.Execution, errors);
        ValidatePhaseTimings(report.Execution.PhaseTimings, "execution", errors);
        ValidateProgress(report.Execution.LastProgress, "execution.lastProgress", errors);
        ValidateFailures(report, errors);
    }

    private static void ValidateRepositoryExecution(
        ChangePortfolioComparisonRepositoryExecution repository,
        List<string> errors)
    {
        ValidateDigest(repository.InputDigest, $"execution.repository[{repository.RepositoryId}]", errors);
        if (repository.SelectedChangeCount < 0 || !Enum.IsDefined(repository.Status) ||
            !Enum.IsDefined(repository.CheckpointDisposition) || repository.ElapsedMilliseconds < 0m)
        {
            errors.Add($"Execution repository '{repository.RepositoryId}' has invalid status or counts.");
        }

        if (repository.Status == ChangePortfolioRepositoryExecutionStatus.Reused &&
            repository.CheckpointDisposition != ChangePortfolioCheckpointDisposition.Hit ||
            repository.Status == ChangePortfolioRepositoryExecutionStatus.Complete &&
            repository.CheckpointDisposition == ChangePortfolioCheckpointDisposition.Hit)
        {
            errors.Add($"Execution repository '{repository.RepositoryId}' has inconsistent reuse status.");
        }

        ValidatePhaseTimings(
            repository.PhaseTimings,
            $"execution.repository[{repository.RepositoryId}]",
            errors);
        ValidateProgress(
            repository.LastProgress,
            $"execution.repository[{repository.RepositoryId}].lastProgress",
            errors);
    }

    private static void ValidateCheckpoint(
        ChangePortfolioComparisonExecution execution,
        List<string> errors)
    {
        ChangePortfolioComparisonCheckpoint checkpoint = execution.Checkpoint;
        if (checkpoint.Protocol != "repository-evidence-checkpoint/1.0.0" ||
            checkpoint.HitCount < 0 || checkpoint.MissCount < 0 ||
            checkpoint.WriteCount < 0 || checkpoint.FailureCount < 0)
        {
            errors.Add("Execution checkpoint counters or protocol are invalid.");
        }

        int expectedHits = execution.Repositories.Count(repository =>
            repository.CheckpointDisposition == ChangePortfolioCheckpointDisposition.Hit);
        int expectedMisses = execution.Repositories.Count(repository =>
            repository.CheckpointDisposition is ChangePortfolioCheckpointDisposition.MissWritten or
                ChangePortfolioCheckpointDisposition.MissFailed);
        int expectedWrites = execution.Repositories.Count(repository =>
            repository.CheckpointDisposition == ChangePortfolioCheckpointDisposition.MissWritten);
        int expectedFailures = execution.Repositories.Count(repository =>
            repository.CheckpointDisposition == ChangePortfolioCheckpointDisposition.MissFailed);
        if (checkpoint.Enabled)
        {
            if (execution.Repositories.Any(repository =>
                    repository.CheckpointDisposition == ChangePortfolioCheckpointDisposition.Disabled) ||
                checkpoint.HitCount != expectedHits || checkpoint.MissCount != expectedMisses ||
                checkpoint.WriteCount != expectedWrites || checkpoint.FailureCount != expectedFailures)
            {
                errors.Add("Enabled checkpoint counters must match repository dispositions.");
            }
        }
        else if (checkpoint.HitCount != 0 || checkpoint.MissCount != 0 ||
                 checkpoint.WriteCount != 0 || checkpoint.FailureCount != 0 ||
                 execution.Repositories.Any(repository =>
                     repository.CheckpointDisposition != ChangePortfolioCheckpointDisposition.Disabled))
        {
            errors.Add("Disabled checkpoints require zero counters and disabled repository dispositions.");
        }
    }

    private static void ValidateFailures(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        HashSet<string> failedRepositories = [.. report.Execution.Repositories
            .Where(repository => repository.Status == ChangePortfolioRepositoryExecutionStatus.Failed)
            .Select(repository => repository.RepositoryId)];
        HashSet<string> failureRepositories = [];
        HashSet<string> bucketIds = [.. report.Buckets.Select(bucket => bucket.Id)];
        foreach (ChangePortfolioComparisonFailure failure in report.Execution.Failures)
        {
            RequireText(failure.RepositoryId, "execution.failure.repositoryId", errors);
            RequireText(failure.Phase, "execution.failure.phase", errors);
            RequireText(failure.Category, "execution.failure.category", errors);
            RequireText(failure.Message, "execution.failure.message", errors);
            ValidateDigest(failure.MessageDigest, "execution.failure.messageDigest", errors);
            if (!failedRepositories.Contains(failure.RepositoryId) ||
                !failureRepositories.Add(failure.RepositoryId))
            {
                errors.Add("Each execution failure must identify one distinct failed repository.");
            }

            if (failure.BucketId is not null && !bucketIds.Contains(failure.BucketId))
            {
                errors.Add($"Execution failure bucket '{failure.BucketId}' is not in the report.");
            }

            if (failure.MessageDigest != ChangePortfolioComparisonIdentity.ComputeTextDigest(
                    failure.Category + "\n" + failure.Message))
            {
                errors.Add("Execution failure messageDigest does not bind its root category and message.");
            }
        }

        if (!failedRepositories.SetEquals(failureRepositories))
        {
            errors.Add("Every failed repository must have exactly one preserved root failure.");
        }
    }

    private static void ValidatePhaseTimings(
        IReadOnlyList<ChangePortfolioComparisonPhaseTiming> timings,
        string path,
        List<string> errors)
    {
        HashSet<string> phases = new(StringComparer.Ordinal);
        foreach (ChangePortfolioComparisonPhaseTiming timing in timings)
        {
            RequireText(timing.Phase, $"{path}.phaseTiming.phase", errors);
            if (timing.ElapsedMilliseconds < 0m || !phases.Add(timing.Phase))
            {
                errors.Add($"{path} phase timings must be nonnegative and unique by phase.");
            }
        }
    }

    private static void ValidateProgress(
        ChangePortfolioComparisonProgress? progress,
        string path,
        List<string> errors)
    {
        if (progress is null)
        {
            return;
        }

        RequireText(progress.Phase, $"{path}.phase", errors);
        if (progress.ObservedAt == default || progress.ProcessedUnits < 0 ||
            progress.TotalUnits < 0 || progress.ProcessedUnits > progress.TotalUnits ||
            progress.AnalysisCacheRequests < 0 || progress.AnalysisCacheHits < 0 ||
            progress.AnalysisCacheHits > progress.AnalysisCacheRequests ||
            progress.ElapsedMilliseconds < 0m || progress.WorkingSetBytes < 0 ||
            progress.PeakWorkingSetBytes < progress.WorkingSetBytes)
        {
            errors.Add($"{path} contains invalid progress, cache, timing, or resource values.");
        }
    }
}
