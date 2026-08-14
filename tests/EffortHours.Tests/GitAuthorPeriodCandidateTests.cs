using EffortHours.Change;

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

        IReadOnlyList<GitCommitMetadata> candidates =
            await client.ListAuthorPeriodCandidatesAsync(
                "virtual-repository",
                new string('c', 40),
                ["Target", "target@example.test"],
                includeCoauthors: true,
                maximumCount: 10_001);

        Assert.Equal([directId, coauthorId], [.. candidates.Select(candidate => candidate.ObjectId)]);
        Assert.Collection(
            runner.Calls,
            direct =>
            {
                Assert.Contains("--fixed-strings", direct);
                Assert.Contains("--regexp-ignore-case", direct);
                Assert.Contains("--max-count=10001", direct);
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
    public void CandidateLedgerRetainsTheTenThousandRecordSafetyBoundary()
    {
        GitPortfolioPlanner.EnsureCandidateLimit(10_000, 10_000);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GitPortfolioPlanner.EnsureCandidateLimit(10_001, 10_000));

        Assert.Contains("identity-prefiltered commit candidates", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unbounded identity ledger", exception.Message, StringComparison.Ordinal);
    }

    private static string Metadata(
        string objectId,
        string authorName,
        string authorEmail,
        string coauthors) => string.Join(
            '\0',
            objectId,
            string.Empty,
            authorName,
            authorEmail,
            "2026-08-14T09:00:00+00:00",
            authorName,
            authorEmail,
            "2026-08-14T09:00:00+00:00",
            coauthors) + '\0';

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
