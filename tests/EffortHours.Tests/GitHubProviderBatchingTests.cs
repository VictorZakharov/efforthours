using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHubProviderBatchingTests
{
    private static readonly DateTimeOffset Since =
        new(2026, 8, 24, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = Since.AddHours(12);

    [Fact]
    public async Task DefaultHeadBatchPreservesTheRestSelectedCommitAndUsesOneProcess()
    {
        string head = new('a', 40);
        string parent = new('b', 40);
        GitHubDiscoveryRepository repository = new("42", "owner/repository", "main");
        QueueRunner batchRunner = new(GraphDefaultHead(head, parent));
        ProviderQueryCounters batchCounters = new();

        IReadOnlyList<DiscoveredRepository>? batched =
            await GitHubAuthorPeriodDiscoveryJson.DiscoverDefaultHeadsBatchedAsync(
                batchRunner,
                "unrelated-folder",
                [repository],
                ["selected@example.test"],
                Since,
                Until,
                ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include,
                batchCounters,
                CancellationToken.None);

        QueueRunner restRunner = new(RestCommitPage(head, parent));
        DiscoveredRepository rest = Assert.IsType<DiscoveredRepository>(
            await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(
                restRunner,
                "unrelated-folder",
                repository,
                ["selected@example.test"],
                "selected",
                Since,
                Until,
                ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include,
                includeOpenPullRequests: false,
                new ProviderQueryCounters(),
                CancellationToken.None));

        DiscoveredRepository batch = Assert.Single(batched!);
        Assert.Equal(rest.Heads.Select(value => value.ObjectId), batch.Heads.Select(value => value.ObjectId));
        Assert.Equal(1, batchCounters.QueryCount);
        Assert.Equal(1, batchCounters.ProcessCount);
        Assert.Contains("graphql", Assert.Single(batchRunner.Calls));
    }

    [Fact]
    public async Task IncompleteDefaultHeadBatchRequiresTheCompleteRestFallback()
    {
        string head = new('a', 40);
        string parent = new('b', 40);
        string response = GraphDefaultHead(head, parent)
            .Replace("false", "true", StringComparison.Ordinal);

        IReadOnlyList<DiscoveredRepository>? result =
            await GitHubAuthorPeriodDiscoveryJson.DiscoverDefaultHeadsBatchedAsync(
                new QueueRunner(response),
                "unrelated-folder",
                [new GitHubDiscoveryRepository("42", "owner/repository", "main")],
                ["selected@example.test"],
                Since,
                Until,
                ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include,
                new ProviderQueryCounters(),
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AccountWideOpenPullInventoryAvoidsPerRepositoryListQueries()
    {
        string head = new('c', 40);
        string parent = new('d', 40);
        string connection = JsonSerializer.Serialize(new[]
        {
            new
            {
                data = new
                {
                    user = new
                    {
                        pullRequests = new
                        {
                            totalCount = 1,
                            nodes = new[]
                            {
                                new
                                {
                                    number = 7,
                                    author = new { login = "selected" },
                                    repository = new { nameWithOwner = "owner/repository" },
                                },
                            },
                            pageInfo = new { hasNextPage = false, endCursor = (string?)null },
                        },
                    },
                },
            },
        });
        QueueRunner runner = new(
            connection,
            JsonSerializer.Serialize(new { commits = 1, head = new { sha = head } }),
            RestCommitPage(head, parent));
        ProviderQueryCounters counters = new();

        IReadOnlyList<DiscoveredRepository>? result =
            await GitHubAuthorPeriodDiscoveryJson
                .DiscoverViewerOpenPullHeadsAccountWideAsync(
                    runner,
                    "unrelated-folder",
                    [new GitHubDiscoveryRepository("42", "owner/repository", "main")],
                    "selected",
                    ["selected", "selected@example.test"],
                    Since,
                    Until,
                    ChangePortfolioDateField.Author,
                    ChangePortfolioMergePolicy.Exclude,
                    ChangePortfolioCoauthorPolicy.Include,
                    counters,
                    CancellationToken.None);

        DiscoveredRepository repository = Assert.Single(result!);
        Assert.Equal(head, Assert.Single(repository.Heads).ObjectId);
        Assert.Equal(3, counters.QueryCount);
        Assert.Equal(1, counters.OpenPullRequestCount);
        Assert.DoesNotContain(
            runner.Calls,
            arguments => arguments.Any(value =>
                value.Contains("pulls?state=open", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task IncompleteAccountConnectionRequiresPerRepositoryFallback()
    {
        string connection = JsonSerializer.Serialize(new[]
        {
            new
            {
                data = new
                {
                    user = new
                    {
                        pullRequests = new
                        {
                            totalCount = 2,
                            nodes = new[]
                            {
                                new
                                {
                                    number = 7,
                                    author = new { login = "selected" },
                                    repository = new { nameWithOwner = "owner/repository" },
                                },
                            },
                            pageInfo = new { hasNextPage = false, endCursor = (string?)null },
                        },
                    },
                },
            },
        });

        IReadOnlyList<DiscoveredRepository>? result =
            await GitHubAuthorPeriodDiscoveryJson
                .DiscoverViewerOpenPullHeadsAccountWideAsync(
                    new QueueRunner(connection),
                    "unrelated-folder",
                    [new GitHubDiscoveryRepository("42", "owner/repository", "main")],
                    "selected",
                    ["selected"],
                    Since,
                    Until,
                    ChangePortfolioDateField.Author,
                    ChangePortfolioMergePolicy.Exclude,
                    ChangePortfolioCoauthorPolicy.Include,
                    new ProviderQueryCounters(),
                    CancellationToken.None);

        Assert.Null(result);
    }

    private static string GraphDefaultHead(string head, string parent) =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                r0 = new
                {
                    defaultBranchRef = new
                    {
                        name = "main",
                        target = new
                        {
                            history = new
                            {
                                nodes = new[]
                                {
                                    new
                                    {
                                        oid = head,
                                        parents = new { nodes = new[] { new { oid = parent } } },
                                        author = new
                                        {
                                            name = "Selected",
                                            email = "selected@example.test",
                                            user = new { login = "selected" },
                                        },
                                        authoredDate = "2026-08-24T12:00:00Z",
                                        committer = new
                                        {
                                            name = "Selected",
                                            email = "selected@example.test",
                                            user = new { login = "selected" },
                                        },
                                        committedDate = "2026-08-24T12:00:00Z",
                                        message = "selected today",
                                    },
                                },
                                pageInfo = new { hasNextPage = false },
                            },
                        },
                    },
                },
            },
        });

    private static string RestCommitPage(string head, string parent) =>
        JsonSerializer.Serialize(new[] { new[]
        {
            new
            {
                sha = head,
                parents = new[] { new { sha = parent } },
                author = new { login = "selected" },
                commit = new
                {
                    author = new
                    {
                        name = "Selected",
                        email = "selected@example.test",
                        date = "2026-08-24T12:00:00Z",
                    },
                    committer = new
                    {
                        name = "Selected",
                        email = "selected@example.test",
                        date = "2026-08-24T12:00:00Z",
                    },
                    message = "selected today",
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
            Calls.Add([.. arguments]);
            return Task.FromResult(new ExternalCommandResult(0, _outputs.Dequeue(), string.Empty)
            {
                ProcessStartupElapsed = TimeSpan.FromMilliseconds(1),
            });
        }
    }
}
