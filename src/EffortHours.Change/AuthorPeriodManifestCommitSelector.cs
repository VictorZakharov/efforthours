using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record SelectedManifestAuthorCommit(
    GitCommitMetadata Metadata,
    DateTimeOffset SelectedTimestamp,
    IReadOnlyList<ChangePortfolioContributorMatch> ContributorMatches,
    IReadOnlyList<string> AmbiguityReasons);

internal sealed record AuthorPeriodManifestSelectionResult(
    IReadOnlyList<SelectedManifestAuthorCommit> Commits,
    IReadOnlyList<Diagnostic> Diagnostics);

internal static class AuthorPeriodManifestCommitSelector
{
    public static AuthorPeriodManifestSelectionResult Select(
        IReadOnlyList<GitCommitMetadata> history,
        ChangeAuthorPeriodManifestSelection selection,
        IReadOnlyList<ChangeAuthorPeriodManifestContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(contributors);
        ChangeAuthorPeriodManifestContributor[] orderedContributors =
            [.. contributors.OrderBy(contributor => contributor.Id, StringComparer.Ordinal)];
        string[] aliases = [.. orderedContributors
            .SelectMany(contributor => contributor.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(alias => alias, StringComparer.Ordinal)];
        GitAuthorPeriodPortfolioOptions options = new()
        {
            Aliases = aliases,
            SinceInclusive = selection.SinceInclusive,
            UntilExclusive = selection.UntilExclusive,
            TimeZone = selection.TimeZone,
            DateField = selection.DateField,
            MergePolicy = selection.MergePolicy,
            CoauthorPolicy = selection.CoauthorPolicy,
        };
        AuthorPeriodSelectionResult selected = AuthorPeriodCommitSelector.Select(
            history,
            options,
            aliases);
        List<SelectedManifestAuthorCommit> commits = [];
        foreach (SelectedAuthorCommit commit in selected.Commits)
        {
            ChangePortfolioContributorMatch[] matches = [.. orderedContributors
                .Select(contributor => Match(commit.Metadata, contributor, selection.CoauthorPolicy))
                .Where(match => match is not null)
                .Select(match => match!)];
            if (matches.Length == 0)
            {
                throw new InvalidOperationException(
                    "Manifest author-period selection produced a commit without a contributor match.");
            }

            List<string> ambiguity = [.. commit.AmbiguityReasons];
            if (matches.Length > 1)
            {
                ambiguity.Add(
                    "The immutable commit matches multiple requested contributors; repository-attributed effort is counted once and shared-credit proportions remain unspecified.");
            }

            commits.Add(new SelectedManifestAuthorCommit(
                commit.Metadata,
                commit.SelectedTimestamp,
                matches,
                ambiguity));
        }

        return new AuthorPeriodManifestSelectionResult(commits, selected.Diagnostics);
    }

    private static ChangePortfolioContributorMatch? Match(
        GitCommitMetadata commit,
        ChangeAuthorPeriodManifestContributor contributor,
        ChangePortfolioCoauthorPolicy coauthorPolicy)
    {
        if (AuthorIdentityMatcher.Matches(commit.Author, contributor.Aliases))
        {
            return new ChangePortfolioContributorMatch
            {
                ContributorId = contributor.Id,
                Kind = ChangePortfolioContributorMatchKind.DirectAuthor,
            };
        }

        return coauthorPolicy == ChangePortfolioCoauthorPolicy.Include &&
            commit.Coauthors.Any(identity => AuthorIdentityMatcher.Matches(identity, contributor.Aliases))
                ? new ChangePortfolioContributorMatch
                {
                    ContributorId = contributor.Id,
                    Kind = ChangePortfolioContributorMatchKind.Coauthor,
                }
                : null;
    }
}

internal static class AuthorIdentityMatcher
{
    public static bool Matches(GitCommitIdentity identity, IReadOnlyList<string> aliases)
    {
        string display = $"{identity.Name} <{identity.Email}>";
        return aliases.Any(alias =>
            string.Equals(alias, identity.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(alias, identity.Email, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(alias, display, StringComparison.OrdinalIgnoreCase));
    }
}
