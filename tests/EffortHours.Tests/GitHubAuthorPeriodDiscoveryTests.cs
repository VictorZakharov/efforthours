using System.Globalization;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHubAuthorPeriodDiscoveryTests
{
    [Fact]
    public async Task AuthenticatedPersonalOwnerUsesThePrivateRepositoryInventoryEndpoint()
    {
        QueueRunner runner = new(JsonSerializer.Serialize(new[] { Array.Empty<object>() }));

        IReadOnlyList<GitHubDiscoveryRepository> repositories =
            await GitHubAuthorPeriodDiscoveryJson.ListRepositoriesAsync(
                runner,
                "virtual-workspace",
                "owner",
                "user",
                "OWNER",
                new ProviderQueryCounters(),
                CancellationToken.None);

        Assert.Empty(repositories);
        Assert.Contains("user/repos?per_page=100&affiliation=owner", Assert.Single(runner.Calls));
    }

    [Theory]
    [InlineData("2026-03-08T12:00:00Z", "2026-03-08T05:00:00+00:00", "2026-03-08T12:00:00+00:00")]
    [InlineData("2026-11-01T12:00:00Z", "2026-11-01T04:00:00+00:00", "2026-11-01T12:00:00+00:00")]
    public void LocalDayUsesNamedZoneBoundariesAcrossDaylightSavingTransitions(
        string asOfText,
        string expectedSinceText,
        string expectedUntilText)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");

        (DateTimeOffset since, DateTimeOffset until) = GitHubAuthorPeriodDiscovery.LocalDay(
            DateTimeOffset.Parse(asOfText, CultureInfo.InvariantCulture),
            zone);

        Assert.Equal(DateTimeOffset.Parse(expectedSinceText, CultureInfo.InvariantCulture), since);
        Assert.Equal(DateTimeOffset.Parse(expectedUntilText, CultureInfo.InvariantCulture), until);
    }

    [Theory]
    [InlineData("https://github.com/Owner/Repository.git", "owner/repository")]
    [InlineData("git@github.com:Owner/Repository.git", "owner/repository")]
    [InlineData("ssh://git@github.com/Owner/Repository", "owner/repository")]
    [InlineData("https://example.test/Owner/Repository.git", null)]
    public void WorkspaceRemoteIdentityIsCanonicalAndProviderScoped(
        string remote,
        string? expected) => Assert.Equal(expected, WorkspaceGitHubRepositoryCatalog.TryNormalizeRemote(remote));

    [Fact]
    public async Task OpenPullDiscoveryKeepsOnlyHeadsWithMatchingIntervalCommits()
    {
        string defaultHead = new('a', 40);
        string matchingHead = new('b', 40);
        string unrelatedHead = new('c', 40);
        string parent = new('d', 40);
        QueueRunner runner = new(
            CommitPage(defaultHead, parent, "Target", "target@example.test", "2026-08-21T12:00:00Z"),
            JsonSerializer.Serialize(new[] { new[]
            {
                new { number = 1, user = new { login = "target" }, head = new { sha = matchingHead } },
                new { number = 2, user = new { login = "target" }, head = new { sha = unrelatedHead } },
                new { number = 3, user = new { login = "collaborator" }, head = new { sha = new string('e', 40) } },
            } }),
            JsonSerializer.Serialize(new { commits = 1 }),
            CommitPage(matchingHead, parent, "Target", "target@example.test", "2026-08-21T12:00:00Z"),
            JsonSerializer.Serialize(new { commits = 1 }),
            CommitPage(unrelatedHead, parent, "Other", "other@example.test", "2026-08-21T12:00:00Z"));
        GitHubDiscoveryRepository provider = new("42", "owner/repository", "main");
        ProviderQueryCounters counters = new();

        DiscoveredRepository discovered = Assert.IsType<DiscoveredRepository>(
            await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
            runner,
            "virtual-workspace",
            provider,
            ["target@example.test"],
            "target",
            new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero),
            ChangePortfolioDateField.Author,
            ChangePortfolioMergePolicy.Exclude,
            ChangePortfolioCoauthorPolicy.Include,
            includeOpenPullRequests: true,
            counters,
            CancellationToken.None));

        Assert.Equal(2, discovered.Heads.Count);
        Assert.Equal(defaultHead, discovered.Heads[0].ObjectId);
        Assert.Equal(matchingHead, discovered.Heads[1].ObjectId);
        Assert.Equal(6, counters.QueryCount);
        Assert.Equal(6, counters.PageCount);
        Assert.Equal(2, counters.OpenPullRequestCount);
    }

    [Fact]
    public async Task OpenPullDiscoveryRejectsAnIncompletePaginatedCommitInventory()
    {
        string defaultHead = new('a', 40);
        string pullHead = new('b', 40);
        string parent = new('d', 40);
        QueueRunner runner = new(
            CommitPage(defaultHead, parent, "Target", "target@example.test", "2026-08-21T12:00:00Z"),
            JsonSerializer.Serialize(new[] { new[]
            {
                new { number = 1, user = new { login = "target" }, head = new { sha = pullHead } },
            } }),
            JsonSerializer.Serialize(new { commits = 2 }),
            CommitPage(pullHead, parent, "Target", "target@example.test", "2026-08-21T12:00:00Z"));
        GitHubDiscoveryRepository provider = new("42", "owner/repository", "main");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
                runner,
                "virtual-workspace",
                provider,
                ["target@example.test"],
                "target",
                new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero),
                ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include,
                includeOpenPullRequests: true,
                new ProviderQueryCounters(),
                CancellationToken.None));

        Assert.Contains("incomplete", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyRepositoryIsInactiveInsteadOfFailingOwnerDiscovery()
    {
        EmptyRepositoryRunner runner = new();
        ProviderQueryCounters counters = new();

        DiscoveredRepository? discovered =
            await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
                runner,
                "virtual-workspace",
                new GitHubDiscoveryRepository("42", "owner/empty", "main"),
                ["target@example.test"],
                "target",
                new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero),
                ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include,
                includeOpenPullRequests: false,
                counters,
                CancellationToken.None);

        Assert.Null(discovered);
        Assert.Equal(1, counters.QueryCount);
        Assert.Equal(1, counters.PageCount);
        Assert.False(runner.RequireSuccess);
    }

    private static string CommitPage(
        string objectId,
        string parent,
        string name,
        string email,
        string timestamp) => JsonSerializer.Serialize(new[] { new[]
        {
            new
            {
                sha = objectId,
                parents = new[] { new { sha = parent } },
                author = new { login = name.ToLowerInvariant() },
                commit = new
                {
                    author = new { name, email, date = timestamp },
                    committer = new { name, email, date = timestamp },
                    message = "Synthetic commit",
                },
            },
        } });

    private sealed class QueueRunner(params string[] outputs) : IExternalCommandRunner
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
            Assert.Equal("gh", executable);
            Calls.Add([.. arguments]);
            return Task.FromResult(new ExternalCommandResult(0, _outputs.Dequeue(), string.Empty));
        }
    }

    private sealed class EmptyRepositoryRunner : IExternalCommandRunner
    {
        public bool RequireSuccess { get; private set; } = true;

        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("gh", executable);
            RequireSuccess = requireSuccess;
            return Task.FromResult(new ExternalCommandResult(
                1,
                "{\"message\":\"Git Repository is empty.\",\"status\":\"409\"}",
                "gh: Git Repository is empty. (HTTP 409)"));
        }
    }
}
