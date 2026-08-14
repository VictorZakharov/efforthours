using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class AuthorPeriodManifestSelectorTests
{
    private static readonly DateTimeOffset Since =
        new(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OneCommitRetainsEveryContributorMatchWithoutMultiplication()
    {
        GitCommitMetadata commit = new()
        {
            ObjectId = new string('a', 40),
            ParentObjectIds = [new string('0', 40)],
            Author = new GitCommitIdentity("Contributor A", "a@example.test"),
            AuthorTimestamp = Since.AddHours(1),
            Committer = new GitCommitIdentity("Integrator", "integrator@example.test"),
            CommitterTimestamp = Since.AddHours(2),
            Coauthors = [new GitCommitIdentity("Contributor B", "b@example.test")],
        };
        ChangeAuthorPeriodManifestContributor contributorB = new()
        {
            Id = "contributor-b",
            Aliases = ["b@example.test", "Contributor B"],
        };
        ChangeAuthorPeriodManifestContributor contributorA = new()
        {
            Id = "contributor-a",
            Aliases = ["a@example.test", "Contributor A"],
        };

        AuthorPeriodManifestSelectionResult result = AuthorPeriodManifestCommitSelector.Select(
            [commit],
            Selection(),
            [contributorB, contributorA]);

        SelectedManifestAuthorCommit selected = Assert.Single(result.Commits);
        Assert.Equal(commit.ObjectId, selected.Metadata.ObjectId);
        Assert.Collection(
            selected.ContributorMatches,
            match =>
            {
                Assert.Equal("contributor-a", match.ContributorId);
                Assert.Equal(ChangePortfolioContributorMatchKind.DirectAuthor, match.Kind);
            },
            match =>
            {
                Assert.Equal("contributor-b", match.ContributorId);
                Assert.Equal(ChangePortfolioContributorMatchKind.Coauthor, match.Kind);
            });
        Assert.Contains(selected.AmbiguityReasons, reason =>
            reason.Contains("counted once", StringComparison.Ordinal));
    }

    [Fact]
    public void ContributorAndAliasOrderDoesNotChangeSemanticSelection()
    {
        GitCommitMetadata commit = new()
        {
            ObjectId = new string('b', 40),
            ParentObjectIds = [],
            Author = new GitCommitIdentity("Contributor A", "a@example.test"),
            AuthorTimestamp = Since.AddHours(1),
            Committer = new GitCommitIdentity("Integrator", "integrator@example.test"),
            CommitterTimestamp = Since.AddHours(1),
            Coauthors = [],
        };
        ChangeAuthorPeriodManifestContributor first = new()
        {
            Id = "contributor-a",
            Aliases = ["Contributor A", "a@example.test"],
        };
        ChangeAuthorPeriodManifestContributor second = new()
        {
            Id = "contributor-z",
            Aliases = ["nobody@example.test"],
        };

        SelectedManifestAuthorCommit original = Assert.Single(
            AuthorPeriodManifestCommitSelector.Select([commit], Selection(), [first, second]).Commits);
        SelectedManifestAuthorCommit reordered = Assert.Single(
            AuthorPeriodManifestCommitSelector.Select(
                [commit],
                Selection(),
                [second, first with { Aliases = [.. first.Aliases.Reverse()] }]).Commits);

        Assert.Equal(
            original.ContributorMatches.Select(match => (match.ContributorId, match.Kind)),
            reordered.ContributorMatches.Select(match => (match.ContributorId, match.Kind)));
        Assert.Equal(original.SelectedTimestamp, reordered.SelectedTimestamp);
    }

    private static ChangeAuthorPeriodManifestSelection Selection() => new()
    {
        SinceInclusive = Since,
        UntilExclusive = Since.AddDays(1),
        TimeZone = "UTC",
        DateField = ChangePortfolioDateField.Author,
        MergePolicy = ChangePortfolioMergePolicy.Exclude,
        CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
    };
}
