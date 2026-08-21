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
        if (report.Execution.EndToEndElapsedMilliseconds < 0m)
        {
            errors.Add("execution.endToEndElapsedMilliseconds must be nonnegative when present.");
        }

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
        ValidateResourceUsage(report, errors);
        ValidatePhaseTimings(report.Execution.PhaseTimings, "execution", errors);
        ValidateProgress(report.Execution.LastProgress, "execution.lastProgress", errors);
        ValidateFailures(report, errors);
    }

    private static void ValidateRepositoryExecution(
        ChangePortfolioComparisonRepositoryExecution repository,
        List<string> errors)
    {
        ValidateDigest(repository.InputDigest, $"execution.repository[{repository.RepositoryId}]", errors);
        if (repository.SelectedChangeCount < 0 || repository.CandidateCount < 0 ||
            repository.ChargedCandidateLedgerBytes < 0 || repository.SelectionChunkCount < 0 ||
            repository.AnalysisChunkCount < 0 || repository.ProjectedSnapshotRequests < 0 ||
            repository.CheckpointReadBytes < 0 || repository.CheckpointWrittenBytes < 0 ||
            !Enum.IsDefined(repository.Status) ||
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
        if (checkpoint.Protocol is not (
                "repository-evidence-checkpoint/1.0.0" or
                ChangePortfolioComparisonPolicies.RepositoryEvidenceCheckpointV2) ||
            checkpoint.HitCount < 0 || checkpoint.MissCount < 0 ||
            checkpoint.WriteCount < 0 || checkpoint.FailureCount < 0 ||
            checkpoint.ReadBytes < 0 || checkpoint.WrittenBytes < 0)
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
                checkpoint.WriteCount != expectedWrites || checkpoint.FailureCount != expectedFailures ||
                checkpoint.ReadBytes != execution.Repositories.Sum(repository =>
                    repository.CheckpointReadBytes) ||
                checkpoint.WrittenBytes != execution.Repositories.Sum(repository =>
                    repository.CheckpointWrittenBytes))
            {
                errors.Add("Enabled checkpoint counters must match repository dispositions.");
            }
        }
        else if (checkpoint.HitCount != 0 || checkpoint.MissCount != 0 ||
                 checkpoint.WriteCount != 0 || checkpoint.FailureCount != 0 ||
                 checkpoint.ReadBytes != 0 || checkpoint.WrittenBytes != 0 ||
                 execution.Repositories.Any(repository =>
                     repository.CheckpointDisposition != ChangePortfolioCheckpointDisposition.Disabled))
        {
            errors.Add("Disabled checkpoints require zero counters and disabled repository dispositions.");
        }

        if (checkpoint.Protocol == ChangePortfolioComparisonPolicies.RepositoryEvidenceCheckpointV2 &&
            checkpoint.MaximumBytesPerRepository !=
                ChangePortfolioLimits.MaximumCheckpointBytesPerRepository)
        {
            errors.Add("The checkpoint byte bound is inconsistent with the current protocol.");
        }
    }

    private static void ValidateResourceUsage(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        ChangePortfolioComparisonResourceUsage? resources = report.Execution.Resources;
        if (resources is null)
        {
            return;
        }

        if (resources.CandidateCount < 0 || resources.ChargedCandidateLedgerBytes < 0 ||
            resources.CandidateLedgerChargePolicy !=
                ChangePortfolioPreflightPolicies.CandidateLedgerChargeV1 ||
            resources.SelectionChunkCount < 0 || resources.SelectedChangeCount < 0 ||
            resources.ProjectedSnapshotRequests < 0 || resources.AnalysisChunkCount < 0 ||
            resources.SnapshotAnalysisRequests < 0 || resources.PeakWorkingSetBytes < 0 ||
            resources.MaximumCandidateLedgerBytesPerRepository !=
                ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository ||
            resources.MaximumCheckpointBytesPerRepository !=
                ChangePortfolioLimits.MaximumCheckpointBytesPerRepository ||
            resources.MaximumConcurrentRepositories != 2 ||
            resources.MaximumBufferedChangesPerRepository != 4 ||
            resources.MaximumConcurrentCpuWorkItems is < 1 or > 24 ||
            resources.MaximumConcurrentGitTreeReads is < 1 or > 8 ||
            resources.MaximumPendingFileInspections is < 2 or > 8 ||
            resources.MaximumBufferedFileBytes != 1024 * 1024 ||
            resources.RenderedOutputBytes < 0 ||
            resources.SelectionChunkSize != ChangeAuthorPeriodManifestLimits.SelectionChunkSize ||
            resources.AnalysisChunkSize != ChangeAuthorPeriodManifestLimits.AnalysisChunkSize ||
            resources.MaximumRenderedOutputBytes != ChangePortfolioLimits.MaximumRenderedOutputBytes ||
            resources.RenderedOutputBytes > resources.MaximumRenderedOutputBytes)
        {
            errors.Add("Execution resource usage contains invalid counts or declared bounds.");
        }

        if (resources.SelectedChangeCount != report.Execution.Repositories.Sum(repository =>
                (long)repository.SelectedChangeCount) ||
            resources.CandidateCount != report.Execution.Repositories.Sum(repository =>
                (long)repository.CandidateCount) ||
            resources.ChargedCandidateLedgerBytes != report.Execution.Repositories.Sum(repository =>
                repository.ChargedCandidateLedgerBytes) ||
            resources.SelectionChunkCount != report.Execution.Repositories.Sum(repository =>
                repository.SelectionChunkCount) ||
            resources.AnalysisChunkCount != report.Execution.Repositories.Sum(repository =>
                repository.AnalysisChunkCount) ||
            resources.ProjectedSnapshotRequests != report.Execution.Repositories.Sum(repository =>
                repository.ProjectedSnapshotRequests) ||
            resources.SnapshotAnalysisRequests != report.Execution.Reuse.SnapshotAnalysisRequests ||
            resources.PeakWorkingSetBytes != report.Execution.Reuse.PeakWorkingSetBytes)
        {
            errors.Add("Execution resource totals do not match repository resource rows.");
        }

        if (resources.SelectionScopeComplete &&
            (resources.SelectedChangeCount > long.MaxValue / 2 ||
             resources.ProjectedSnapshotRequests != resources.SelectedChangeCount * 2))
        {
            errors.Add("Complete selection scope must project two snapshot requests per change.");
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
