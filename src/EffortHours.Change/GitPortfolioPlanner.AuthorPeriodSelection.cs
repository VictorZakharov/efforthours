using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitPortfolioPlanner
{
    private static GitAuthorPeriodIdentityGroup[] CreateIdentityGroups(
        ChangeAuthorPeriodManifest manifest) =>
        [
            .. manifest.Contributors
                .OrderBy(contributor => contributor.Id, StringComparer.Ordinal)
                .Select(contributor => new GitAuthorPeriodIdentityGroup(
                    contributor.Id,
                    CanonicalAliases(contributor.Aliases))),
        ];

    private async Task<PreparedManifestSelection> SelectManifestRepositoryAsync(
        PreparedManifestRepository repository,
        ChangeAuthorPeriodManifest manifest,
        IReadOnlyList<GitAuthorPeriodIdentityGroup> identityGroups,
        ChangePortfolioExecutionTelemetry executionTelemetry,
        CancellationToken cancellationToken)
    {
        GitAuthorPeriodCandidateResult candidateResult;
        try
        {
            using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.HistoryUnion))
            {
                candidateResult = await _git.ListAuthorPeriodCandidatesAsync(
                    repository.RootPath,
                    new GitAuthorPeriodCandidateQuery
                    {
                        HeadObjectIds = [.. repository.Manifest.Heads.Select(head => head.ObjectId)],
                        IdentityGroups = identityGroups,
                        SinceInclusive = manifest.Selection.SinceInclusive,
                        UntilExclusive = manifest.Selection.UntilExclusive,
                        DateField = manifest.Selection.DateField,
                        IncludeCoauthors = manifest.Selection.CoauthorPolicy ==
                            ChangePortfolioCoauthorPolicy.Include,
                        MaximumLedgerBytes = _options.MaximumCandidateLedgerBytes,
                        EmergencyMaximumCandidates = _options.MaximumHistoryCommits,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (ExternalCommandException exception)
        {
            throw new InvalidOperationException(
                $"Git could not traverse author-period candidates for repository '{repository.Manifest.Id}'.",
                exception);
        }

        AuthorPeriodManifestSelectionResult selected;
        using (executionTelemetry.Measure(ChangePortfolioExecutionPhases.Selection))
        {
            selected = AuthorPeriodManifestCommitSelector.Select(
                candidateResult.Candidates,
                manifest.Selection,
                manifest.Contributors);
        }

        return new PreparedManifestSelection(candidateResult, selected);
    }

    private sealed record PreparedManifestSelection(
        GitAuthorPeriodCandidateResult CandidateResult,
        AuthorPeriodManifestSelectionResult Selected);
}
