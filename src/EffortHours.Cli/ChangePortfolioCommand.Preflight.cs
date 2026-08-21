using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<int> ExecutePreflightAsync(
        ChangePortfolioCommandOptions options,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        GitAuthorPeriodManifestScopePlan scope = await _measureAuthorPeriodManifest(
            options.AuthorPeriodManifestPath!,
            executionTelemetry,
            cancellationToken).ConfigureAwait(false);
        ChangePortfolioPreflightReport report = BuildPreflightReport(scope, options.Profile);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Generated author-period preflight is invalid: " + string.Join(" ", errors));
        }

        string output;
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.Rendering))
        {
            string json = options.Compact
                ? ContractJson.SerializeCompact(report)
                : ContractJson.Serialize(report);
            SchemaValidationResult schema = ContractSchemaValidator.Validate(
                SchemaNames.ChangePortfolioPreflightReport,
                json);
            if (!schema.IsValid)
            {
                throw new InvalidOperationException(
                    "Generated author-period preflight violates its JSON Schema: " +
                    string.Join(" ", schema.Errors));
            }

            output = options.Format == "markdown"
                ? ChangePortfolioPreflightMarkdownRenderer.Render(report)
                : json;
        }

        int write = await WriteOutputAsync(
            output,
            options.OutputPath,
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (write != CliExitCodes.Success)
        {
            return write;
        }

        await WriteExecutionTimingsAsync(executionTelemetry, standardError).ConfigureAwait(false);
        return report.Status == ChangePortfolioPreflightStatus.Blocked
            ? CliExitCodes.InvalidInput
            : CliExitCodes.Success;
    }

    private static ChangePortfolioPreflightReport BuildPreflightReport(
        GitAuthorPeriodManifestScopePlan scope,
        EstimationProfile profile)
    {
        ChangePortfolioPreflightRepository[] repositories =
        [
            .. scope.Repositories
                .OrderBy(repository => repository.RepositoryId, StringComparer.Ordinal)
                .Select(repository => new ChangePortfolioPreflightRepository
                {
                    RepositoryId = repository.RepositoryId,
                    HeadCount = repository.HeadCount,
                    CandidateCount = repository.CandidateCount,
                    CandidateCountIsLowerBound = repository.CandidateCountIsLowerBound,
                    SelectedChangeCount = repository.SelectedChangeCount,
                    SharedContributorChangeCount = repository.SharedContributorChangeCount,
                    ProjectedSnapshotRequests = repository.ProjectedSnapshotRequests,
                    SelectionChunkCount = repository.SelectionChunkCount,
                    AnalysisChunkCount = repository.AnalysisChunkCount,
                    ChargedCandidateLedgerBytes = repository.ChargedCandidateLedgerBytes,
                    MaximumCandidateLedgerBytes = repository.MaximumCandidateLedgerBytes,
                    BlockingResource = repository.BlockingResource,
                    Contributors = [.. repository.Contributors.Select(contributor =>
                        new ChangePortfolioPreflightContributor
                        {
                            ContributorId = contributor.ContributorId,
                            CandidateCount = contributor.CandidateCount,
                            DirectAuthorCandidateCount = contributor.DirectAuthorCandidateCount,
                            CoauthorCandidateCount = contributor.CoauthorCandidateCount,
                            SelectedChangeCount = contributor.SelectedChangeCount,
                        })],
                }),
        ];
        long? selected = scope.CompleteScope
            ? repositories.Sum(repository => (long)repository.SelectedChangeCount!.Value)
            : null;
        bool globalCountBlocked = selected > ChangeAuthorPeriodManifestLimits.MaximumSelectedCommits;
        ChangePortfolioPreflightStatus status = !scope.CompleteScope || globalCountBlocked
            ? ChangePortfolioPreflightStatus.Blocked
            : selected > ChangePortfolioPreflightPolicies.DetailedReportReviewThreshold
                ? ChangePortfolioPreflightStatus.SummaryRecommended
                : ChangePortfolioPreflightStatus.Ready;
        ChangePortfolioPreflightRecommendation recommendation = Recommendation(status, selected);
        return new ChangePortfolioPreflightReport
        {
            CliVersion = CliVersion(),
            Profile = profile,
            Selection = scope.Selection,
            Status = status,
            Totals = new ChangePortfolioPreflightTotals
            {
                RepositoryCount = repositories.Length,
                HeadCount = repositories.Sum(repository => repository.HeadCount),
                ContributorCount = scope.Manifest.Contributors.Count,
                CandidateCount = repositories.Sum(repository => (long)repository.CandidateCount),
                CandidateCountIsLowerBound = repositories.Any(
                    repository => repository.CandidateCountIsLowerBound),
                SelectedChangeCount = selected,
                SharedContributorChangeCount = scope.CompleteScope
                    ? repositories.Sum(repository =>
                        (long)repository.SharedContributorChangeCount!.Value)
                    : null,
                ProjectedSnapshotRequests = scope.CompleteScope
                    ? repositories.Sum(repository => repository.ProjectedSnapshotRequests!.Value)
                    : null,
            },
            Repositories = repositories,
            Resources = new ChangePortfolioPreflightResources
            {
                BlockingResource = globalCountBlocked
                    ? "emergency-selected-change-count"
                    : repositories
                        .Select(repository => repository.BlockingResource)
                        .FirstOrDefault(resource => resource is not null),
                CandidateLedgerChargePolicy =
                    ChangePortfolioPreflightPolicies.CandidateLedgerChargeV1,
                ChargedCandidateLedgerBytes = repositories.Sum(
                    repository => repository.ChargedCandidateLedgerBytes),
                MaximumCandidateLedgerBytesPerRepository =
                    ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository,
                SelectionChunkCount = repositories.Sum(repository => repository.SelectionChunkCount),
                SelectionChunkSize = ChangeAuthorPeriodManifestLimits.SelectionChunkSize,
                AnalysisChunkCount = scope.CompleteScope
                    ? repositories.Sum(repository => repository.AnalysisChunkCount!.Value)
                    : null,
                AnalysisChunkSize = ChangeEstimator.PortfolioDeltaPrimeChunkSize,
                MaximumConcurrentRepositories = ChangeEstimator.MaximumConcurrentPortfolioRepositories,
                MaximumConcurrentChangesPerRepository =
                    ChangeEstimator.MaximumConcurrentPortfolioChangesPerRepository,
                MaximumConcurrentCpuWorkItems =
                    RepositoryAnalysisConcurrency.MaximumCpuWorkItems,
                MaximumConcurrentGitTreeReads =
                    RepositoryAnalysisConcurrency.MaximumGitTreeReads,
                MaximumPendingFileInspections =
                    RepositoryAnalysisConcurrency.MaximumPendingFileInspections,
                MaximumBufferedFileBytes =
                    RepositoryAnalysisConcurrency.MaximumBufferedFileBytes,
                EmergencyMaximumCandidatesPerRepository =
                    ChangeAuthorPeriodManifestLimits.EmergencyMaximumIdentityCandidatesPerRepository,
                EmergencyMaximumSelectedChanges =
                    ChangeAuthorPeriodManifestLimits.MaximumSelectedCommits,
                MaximumCheckpointBytesPerRepository =
                    ChangePortfolioLimits.MaximumCheckpointBytesPerRepository,
                MaximumRenderedOutputBytes = ChangePortfolioLimits.MaximumRenderedOutputBytes,
            },
            OutputPolicy = new ChangePortfolioPreflightOutputPolicy
            {
                DetailedReportReviewThreshold =
                    ChangePortfolioPreflightPolicies.DetailedReportReviewThreshold,
                FullCalculationIncludesEverySelectedChange = true,
                SummaryMayOmitDetailRows = true,
                DetailContract = "Summary views may omit per-change rows only after every selected " +
                    "change participates in one global calculation and reconciliation; omission must be explicit.",
            },
            Recommendation = recommendation,
            Verification = new ChangePortfolioPreflightVerification
            {
                ManifestDigest = scope.Selection.AuthorPeriodManifest!.ManifestDigest,
                CompleteScope = scope.CompleteScope,
                CalculationPerformed = false,
                RepositoryPathsExcluded = true,
                RawAliasesExcluded = true,
            },
        };
    }

    private static ChangePortfolioPreflightRecommendation Recommendation(
        ChangePortfolioPreflightStatus status,
        long? selected)
    {
        if (status == ChangePortfolioPreflightStatus.Blocked)
        {
            return new ChangePortfolioPreflightRecommendation
            {
                Action = ChangePortfolioPreflightAction.ResolveResourceBudget,
                Reason = "This build cannot retain or render the measured scope inside one or more " +
                    "declared resource budgets; the count is never silently truncated.",
                Steps =
                [
                    "Keep the requested interval as one semantic calculation; do not add separately reconciled fragments.",
                    "Narrow contributor, repository, or head scope only if the omitted input is outside the intended portfolio.",
                    "Otherwise use a build with a larger explicitly bounded resource envelope or extend the implementation before estimating EHE.",
                ],
            };
        }

        if (selected == 0)
        {
            return new ChangePortfolioPreflightRecommendation
            {
                Action = ChangePortfolioPreflightAction.ReviewEmptySelection,
                Reason = "No non-merge change matched the exact manifest identities and interval.",
                Steps =
                [
                    "Do not launch snapshot or static analysis for this empty selection.",
                    "If zero was unexpected, review the manifest aliases, pinned heads, date field, timezone, and inclusive/exclusive boundaries.",
                ],
            };
        }

        if (status == ChangePortfolioPreflightStatus.SummaryRecommended)
        {
            return new ChangePortfolioPreflightRecommendation
            {
                Action = ChangePortfolioPreflightAction.RunCheckpointedSummary,
                Reason = $"The exact {selected} change selection fits calculation budgets but exceeds " +
                    $"the {ChangePortfolioPreflightPolicies.DetailedReportReviewThreshold}-row " +
                    "human-review threshold.",
                Steps =
                [
                    "Run one time-bucketed manifest calculation with a stable --checkpoint directory.",
                    "Use --format markdown with --report-view trend or findings for a final summary; every selected change still participates in global reconciliation.",
                    "Use JSON only when a downstream tool requires the complete detailed contract.",
                    "Treat the measured counts as scope evidence, not a wall-time forecast; tree, diff, and analyzer shape can still dominate.",
                    "Do not split the interval into independent runs or add rounded report totals.",
                ],
            };
        }

        return new ChangePortfolioPreflightRecommendation
        {
            Action = ChangePortfolioPreflightAction.RunNormally,
            Reason = $"The exact {selected} change selection fits the declared resource and " +
                "presentation envelopes.",
            Steps =
            [
                "Run the same manifest without --preflight so selection and analysis use the measured immutable scope.",
                "Use a stable --checkpoint directory for a time-bucketed run when interruption or repeated rendering is likely.",
                "Treat the measured counts as scope evidence, not a wall-time forecast; tree, diff, and analyzer shape can still dominate.",
            ],
        };
    }

}
