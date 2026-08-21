using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangePortfolioPreflightReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "change portfolio preflight", errors);
        RequireText(report.CliVersion, "preflight.cliVersion", errors);
        if (report.Protocol != ChangePortfolioPreflightPolicies.ProtocolV1)
        {
            errors.Add("The change portfolio preflight protocol is unsupported.");
        }

        if (report.Resources.CandidateLedgerChargePolicy !=
            ChangePortfolioPreflightPolicies.CandidateLedgerChargeV1)
        {
            errors.Add("The candidate-ledger charge policy is unsupported.");
        }

        if (!Enum.IsDefined(report.Profile) || !Enum.IsDefined(report.Status) ||
            !Enum.IsDefined(report.Recommendation.Action))
        {
            errors.Add("The change portfolio preflight uses an unsupported enum value.");
        }

        errors.AddRange(Validate(report.Selection).Select(error => $"selection: {error}"));

        if (report.Repositories.Count != report.Totals.RepositoryCount ||
            report.Totals.HeadCount != report.Repositories.Sum(repository => repository.HeadCount) ||
            report.Totals.ContributorCount !=
                report.Selection.AuthorPeriodManifest?.ContributorIds.Count ||
            report.Totals.CandidateCount !=
                report.Repositories.Sum(repository => (long)repository.CandidateCount) ||
            report.Totals.CandidateCountIsLowerBound !=
                report.Repositories.Any(repository => repository.CandidateCountIsLowerBound))
        {
            errors.Add("Preflight totals do not match its repository rows or selection.");
        }

        bool complete = report.Repositories.All(repository => repository.BlockingResource is null);
        bool blocked = !complete || report.Resources.BlockingResource is not null;
        if (complete != report.Verification.CompleteScope ||
            blocked != (report.Status == ChangePortfolioPreflightStatus.Blocked) ||
            !complete && report.Resources.BlockingResource is null)
        {
            errors.Add("Preflight status is inconsistent with its resource-budget results.");
        }

        ValidatePreflightRepositories(report, complete, errors);
        ValidatePreflightResources(report, complete, errors);

        long? selected = report.Totals.SelectedChangeCount;
        ChangePortfolioPreflightAction expectedAction = report.Status switch
        {
            ChangePortfolioPreflightStatus.Blocked =>
                ChangePortfolioPreflightAction.ResolveResourceBudget,
            ChangePortfolioPreflightStatus.SummaryRecommended =>
                ChangePortfolioPreflightAction.RunCheckpointedSummary,
            _ when selected == 0 => ChangePortfolioPreflightAction.ReviewEmptySelection,
            _ => ChangePortfolioPreflightAction.RunNormally,
        };
        if (report.Recommendation.Action != expectedAction ||
            report.Status == ChangePortfolioPreflightStatus.SummaryRecommended &&
                selected <= ChangePortfolioPreflightPolicies.DetailedReportReviewThreshold ||
            report.Status == ChangePortfolioPreflightStatus.Ready &&
                selected > ChangePortfolioPreflightPolicies.DetailedReportReviewThreshold)
        {
            errors.Add("Preflight recommendation does not match the measured scope.");
        }

        if (report.OutputPolicy.DetailedReportReviewThreshold !=
                ChangePortfolioPreflightPolicies.DetailedReportReviewThreshold ||
            !report.OutputPolicy.FullCalculationIncludesEverySelectedChange ||
            !report.OutputPolicy.SummaryMayOmitDetailRows)
        {
            errors.Add("Preflight output policy is inconsistent with the current protocol.");
        }

        if (report.Verification.CalculationPerformed ||
            !report.Verification.RepositoryPathsExcluded ||
            !report.Verification.RawAliasesExcluded)
        {
            errors.Add("Preflight verification must describe a selection-only privacy-safe report.");
        }

        RequireText(report.Recommendation.Reason, "preflight.recommendation.reason", errors);
        RequireText(report.Verification.ManifestDigest, "preflight.verification.manifestDigest", errors);
        RequireText(report.OutputPolicy.DetailContract, "preflight.outputPolicy.detailContract", errors);
        return errors;
    }

    private static void ValidatePreflightRepositories(
        ChangePortfolioPreflightReport report,
        bool complete,
        List<string> errors)
    {
        if (report.Repositories.Select(repository => repository.RepositoryId)
            .Distinct(StringComparer.Ordinal).Count() != report.Repositories.Count)
        {
            errors.Add("Preflight repository IDs must be unique.");
        }

        IReadOnlyList<string> selectedRepositoryIds = report.Selection.AuthorPeriodManifest?
            .Repositories.Select(repository => repository.Id).ToArray() ?? [];
        if (!selectedRepositoryIds.ToHashSet(StringComparer.Ordinal).SetEquals(
            report.Repositories.Select(repository => repository.RepositoryId)))
        {
            errors.Add("Preflight repository rows must match the manifest selection.");
        }

        IReadOnlyList<string> selectedContributorIds = report.Selection.AuthorPeriodManifest?
            .ContributorIds ?? [];

        foreach (ChangePortfolioPreflightRepository repository in report.Repositories)
        {
            bool repositoryComplete = repository.BlockingResource is null;
            if (repository.HeadCount < 1 || repository.CandidateCount < 0 ||
                repository.SelectionChunkCount < 0 ||
                repository.ChargedCandidateLedgerBytes < 0 ||
                repository.MaximumCandidateLedgerBytes !=
                    ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository ||
                repository.Contributors.Count != report.Totals.ContributorCount ||
                !selectedContributorIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                    repository.Contributors.Select(contributor => contributor.ContributorId)) ||
                repository.Contributors.Any(contributor =>
                    contributor.CandidateCount < 0 ||
                    contributor.DirectAuthorCandidateCount < 0 ||
                    contributor.CoauthorCandidateCount < 0 ||
                    contributor.DirectAuthorCandidateCount + contributor.CoauthorCandidateCount !=
                        contributor.CandidateCount ||
                    contributor.SelectedChangeCount < 0))
            {
                errors.Add($"Preflight repository '{repository.RepositoryId}' has invalid resource counts.");
            }

            int expectedSelectionChunks = repository.CandidateCount == 0
                ? 0
                : ((repository.CandidateCount - 1) /
                    ChangeAuthorPeriodManifestLimits.SelectionChunkSize) + 1;
            if (repository.SelectionChunkCount != expectedSelectionChunks ||
                repositoryComplete &&
                    (repository.CandidateCountIsLowerBound ||
                     repository.SelectedChangeCount is null ||
                     repository.SharedContributorChangeCount is null ||
                     repository.ProjectedSnapshotRequests !=
                        checked(repository.SelectedChangeCount * 2L) ||
                     repository.AnalysisChunkCount != ChunkCount(
                        repository.SelectedChangeCount.Value,
                        ChangeAuthorPeriodManifestLimits.AnalysisChunkSize)) ||
                !repositoryComplete &&
                    (!repository.CandidateCountIsLowerBound ||
                     repository.SelectedChangeCount is not null ||
                     repository.SharedContributorChangeCount is not null ||
                     repository.ProjectedSnapshotRequests is not null ||
                     repository.AnalysisChunkCount is not null))
            {
                errors.Add($"Preflight repository '{repository.RepositoryId}' has inconsistent scope completion.");
            }
        }

        if (complete &&
            (report.Totals.SelectedChangeCount != report.Repositories.Sum(repository =>
                (long)repository.SelectedChangeCount!.Value) ||
             report.Totals.SharedContributorChangeCount != report.Repositories.Sum(repository =>
                (long)repository.SharedContributorChangeCount!.Value) ||
             report.Totals.ProjectedSnapshotRequests != report.Repositories.Sum(repository =>
                repository.ProjectedSnapshotRequests!.Value)))
        {
            errors.Add("Complete preflight totals do not match repository selection counts.");
        }

        if (!complete && (report.Totals.SelectedChangeCount is not null ||
            report.Totals.SharedContributorChangeCount is not null ||
            report.Totals.ProjectedSnapshotRequests is not null))
        {
            errors.Add("Blocked preflight totals must not imply a complete selected scope.");
        }
    }

    private static void ValidatePreflightResources(
        ChangePortfolioPreflightReport report,
        bool complete,
        List<string> errors)
    {
        ChangePortfolioPreflightResources resources = report.Resources;
        if (resources.ChargedCandidateLedgerBytes != report.Repositories.Sum(repository =>
                repository.ChargedCandidateLedgerBytes) ||
            resources.SelectionChunkCount != report.Repositories.Sum(repository =>
                repository.SelectionChunkCount) ||
            resources.AnalysisChunkCount != (complete
                ? report.Repositories.Sum(repository => repository.AnalysisChunkCount!.Value)
                : null) ||
            resources.MaximumCandidateLedgerBytesPerRepository !=
                ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository ||
            resources.SelectionChunkSize != ChangeAuthorPeriodManifestLimits.SelectionChunkSize ||
            resources.AnalysisChunkSize != ChangeAuthorPeriodManifestLimits.AnalysisChunkSize ||
            resources.MaximumConcurrentRepositories != 2 ||
            resources.MaximumConcurrentChangesPerRepository != 4 ||
            resources.MaximumConcurrentCpuWorkItems is < 1 or > 24 ||
            resources.MaximumConcurrentGitTreeReads is < 1 or > 8 ||
            resources.MaximumPendingFileInspections is < 2 or > 8 ||
            resources.MaximumBufferedFileBytes != 1024 * 1024 ||
            resources.EmergencyMaximumCandidatesPerRepository !=
                ChangeAuthorPeriodManifestLimits.EmergencyMaximumIdentityCandidatesPerRepository ||
            resources.EmergencyMaximumSelectedChanges !=
                ChangeAuthorPeriodManifestLimits.EmergencyMaximumSelectedCommits ||
            resources.MaximumCheckpointBytesPerRepository !=
                ChangePortfolioLimits.MaximumCheckpointBytesPerRepository ||
            resources.MaximumRenderedOutputBytes != ChangePortfolioLimits.MaximumRenderedOutputBytes)
        {
            errors.Add("Preflight resource totals or declared bounds are inconsistent.");
        }
    }

    private static int ChunkCount(int count, int chunkSize) => count == 0
        ? 0
        : ((count - 1) / chunkSize) + 1;
}
