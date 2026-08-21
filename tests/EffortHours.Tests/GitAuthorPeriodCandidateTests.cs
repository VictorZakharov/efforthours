using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitAuthorPeriodCandidateTests
{
    [Fact]
    public async Task CandidateQueryPrefiltersDirectAuthorsAndCoauthorsInsideGit()
    {
        string directId = new('a', 40);
        string coauthorId = new('b', 40);
        RecordingRunner runner = new(
            Metadata(directId, "Target", "target@example.test", string.Empty),
            Metadata(
                coauthorId,
                "Other",
                "other@example.test",
                "Target <target@example.test>"));
        GitClient client = new(
            runner,
            (_, _, _) => throw new NotSupportedException());

        GitAuthorPeriodCandidateResult result =
            await client.ListAuthorPeriodCandidatesAsync(
                "virtual-repository",
                Query(
                    [new string('c', 40)],
                    [new GitAuthorPeriodIdentityGroup(
                        "contributor-a",
                        ["Target", "target@example.test"])],
                    maximumCandidates: 10_000,
                    includeCoauthors: true));

        Assert.Equal([directId, coauthorId], [.. result.Candidates.Select(candidate => candidate.ObjectId)]);
        GitAuthorPeriodCandidateGroupCount group = Assert.Single(result.GroupCounts);
        Assert.Equal(1, group.DirectAuthorCount);
        Assert.Equal(1, group.CoauthorCount);
        Assert.Equal(2, group.TotalCount);
        Assert.Collection(
            runner.Calls,
            direct =>
            {
                Assert.Contains("--fixed-strings", direct);
                Assert.Contains("--regexp-ignore-case", direct);
                Assert.DoesNotContain(direct, argument =>
                    argument.StartsWith("--max-count=", StringComparison.Ordinal));
                Assert.Contains("--author=Target", direct);
                Assert.Contains("--author=target@example.test", direct);
                Assert.DoesNotContain(direct, argument => argument.StartsWith("--grep=", StringComparison.Ordinal));
            },
            coauthor =>
            {
                Assert.Contains("--grep=Target", coauthor);
                Assert.Contains("--grep=target@example.test", coauthor);
                Assert.DoesNotContain(coauthor, argument => argument.StartsWith("--author=", StringComparison.Ordinal));
            });
    }

    [Fact]
    public async Task OutOfWindowLifetimeMatchesDoNotConsumeTheCandidateLimit()
    {
        RecordingRunner runner = new(string.Concat(
            Metadata(new string('a', 40), "Target", "target@example.test", string.Empty, "2020-01-01T00:00:00+00:00"),
            Metadata(new string('b', 40), "Target", "target@example.test", string.Empty),
            Metadata(new string('c', 40), "Target", "target@example.test", string.Empty)));
        GitClient client = new(runner, (_, _, _) => throw new NotSupportedException());

        GitAuthorPeriodCandidateResult result = await client.ListAuthorPeriodCandidatesAsync(
            "virtual-repository",
            Query(
                [new string('d', 40)],
                [new GitAuthorPeriodIdentityGroup("contributor-a", ["Target"])],
                maximumCandidates: 2));

        Assert.Equal(2, result.Candidates.Count);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.ObjectId == new string('a', 40));
        Assert.Equal(2, Assert.Single(result.GroupCounts).TotalCount);
    }

    [Fact]
    public async Task OverLimitDiagnosticReportsObservedCountAndPublicContributorBreakdown()
    {
        RecordingRunner runner = new(string.Concat(
            Metadata(new string('a', 40), "Target", "target@example.test", string.Empty),
            Metadata(new string('b', 40), "Target", "target@example.test", string.Empty),
            Metadata(new string('c', 40), "Target", "target@example.test", string.Empty)));
        GitClient client = new(runner, (_, _, _) => throw new NotSupportedException());

        GitAuthorPeriodCandidateLimitException exception = await Assert.ThrowsAsync<
            GitAuthorPeriodCandidateLimitException>(() => client.ListAuthorPeriodCandidatesAsync(
                "virtual-repository",
                Query(
                    [new string('d', 40)],
                    [new GitAuthorPeriodIdentityGroup(
                        "contributor-a",
                        ["Target", "target@example.test"])],
                    maximumCandidates: 2)));

        Assert.Equal(3, exception.ObservedCount);
        Assert.False(exception.CountIsLowerBound);
        Assert.Equal(2, exception.MaximumCount);
        GitAuthorPeriodCandidateGroupCount count = Assert.Single(exception.GroupCounts);
        Assert.Equal("contributor-a", count.Id);
        Assert.Equal(3, count.DirectAuthorCount);
        Assert.Equal(3, count.TotalCount);
        Assert.Contains("found 3 exact identity candidates", exception.Message, StringComparison.Ordinal);
        Assert.Contains("limit is 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("contributor-a=3", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("target@example.test", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CandidateBreakdownUsesDirectAuthorPrecedenceForTheSameContributor()
    {
        string objectId = new('a', 40);
        string metadata = Metadata(
            objectId,
            "Target",
            "target@example.test",
            "Target <target@example.test>");
        RecordingRunner runner = new(metadata, metadata);
        GitClient client = new(runner, (_, _, _) => throw new NotSupportedException());

        GitAuthorPeriodCandidateResult result = await client.ListAuthorPeriodCandidatesAsync(
            "virtual-repository",
            Query(
                [new string('d', 40)],
                [new GitAuthorPeriodIdentityGroup("contributor-a", ["Target"])],
                maximumCandidates: 2,
                includeCoauthors: true));

        _ = Assert.Single(result.Candidates);
        GitAuthorPeriodCandidateGroupCount count = Assert.Single(result.GroupCounts);
        Assert.Equal(1, count.DirectAuthorCount);
        Assert.Equal(0, count.CoauthorCount);
        Assert.Equal(1, count.TotalCount);
    }

    [Fact]
    public async Task CandidateQueryPassesAllHeadsToOneRepositoryScopedUnion()
    {
        string firstHead = new('c', 40);
        string secondHead = new('b', 40);
        RecordingRunner runner = new(
            Metadata(new string('a', 40), "Target", "target@example.test", string.Empty));
        GitClient client = new(
            runner,
            (_, _, _) => throw new NotSupportedException());

        GitAuthorPeriodCandidateResult result = await client.ListAuthorPeriodCandidatesAsync(
            "virtual-repository",
            Query(
                [firstHead, secondHead],
                [new GitAuthorPeriodIdentityGroup("contributor-a", ["Target"])],
                maximumCandidates: 100));

        _ = Assert.Single(result.Candidates);
        IReadOnlyList<string> call = Assert.Single(runner.Calls);
        Assert.Equal(secondHead, call[^2]);
        Assert.Equal(firstHead, call[^1]);
        Assert.Equal(1, call.Count(argument => argument == firstHead));
        Assert.Equal(1, call.Count(argument => argument == secondHead));
    }

    private static string Metadata(
        string objectId,
        string authorName,
        string authorEmail,
        string coauthors,
        string timestamp = "2026-08-14T09:00:00+00:00") => string.Join(
            '\0',
            objectId,
            string.Empty,
            authorName,
            authorEmail,
            timestamp,
            authorName,
            authorEmail,
            timestamp,
            coauthors) + '\0';

    private static GitAuthorPeriodCandidateQuery Query(
        IReadOnlyList<string> heads,
        IReadOnlyList<GitAuthorPeriodIdentityGroup> groups,
        int maximumCandidates,
        bool includeCoauthors = false) => new()
        {
            HeadObjectIds = heads,
            IdentityGroups = groups,
            SinceInclusive = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            UntilExclusive = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            DateField = ChangePortfolioDateField.Author,
            IncludeCoauthors = includeCoauthors,
            MaximumCandidates = maximumCandidates,
        };

    private sealed class RecordingRunner(params string[] outputs) : IExternalCommandRunner
    {
        private readonly Queue<string> _outputs = new(outputs);

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("git", executable);
            Assert.Equal("virtual-repository", workingDirectory);
            Assert.True(requireSuccess);
            Calls.Add([.. arguments]);
            return Task.FromResult(new ExternalCommandResult(0, _outputs.Dequeue(), string.Empty));
        }
    }
}
