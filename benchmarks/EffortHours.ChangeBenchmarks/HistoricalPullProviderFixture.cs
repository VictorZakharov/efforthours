using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal sealed class HistoricalPullProviderFixture(int population = 258, int latencyMilliseconds = 0) : IExternalCommandRunner
{
    private int _active;
    private int _peak;
    public int Peak => _peak;
    public int RepositoryCount { get; init; } = 1;
    public static string Repository(int index) => index == 0 ? "owner/project" : "owner/project-" + index.ToString(CultureInfo.InvariantCulture);
    public string BaseHead { get; set; } = Id(10000);
    public int ScopedPaddingCharacters { get; init; }
    public bool InvalidScopedTotal { get; set; }
    public bool RepeatedScopedCursor { get; set; }
    public bool ChangedDuringBatch { get; set; }
    public bool IncompleteBatch { get; set; }
    public bool IncompleteParents { get; set; }
    public bool PartialBatchError { get; set; }
    public bool FailBatch { get; set; }
    public ConcurrentQueue<IReadOnlyList<string>> Calls { get; } = new();
    public static string Id(int number) => number.ToString("x40", CultureInfo.InvariantCulture);
    public static readonly DateTimeOffset Since = new(2026, 1, 19, 5, 0, 0, TimeSpan.Zero);

    public async Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken, bool requireSuccess = true)
    {
        Calls.Enqueue([.. arguments]);
        int active = Interlocked.Increment(ref _active);
        InterlockedExtensions.Max(ref _peak, active);
        try
        {
            if (latencyMilliseconds > 0) await Task.Delay(latencyMilliseconds, cancellationToken).ConfigureAwait(false);
            string query = arguments.FirstOrDefault(value => value.StartsWith("query=", StringComparison.Ordinal)) ?? string.Empty;
            object response;
            if (query.Contains("pullRequests(first:", StringComparison.Ordinal))
            {
                object[] nodes = [.. Enumerable.Range(1, population).Select(number => (object)new
                {
                    number, state = "MERGED", author = new { login = "selected" }, repository = new { nameWithOwner = Repository((number - 1) % RepositoryCount) },
                    headRefOid = Id(number), baseRefOid = BaseHead, commits = new { totalCount = 1 },
                }), new { number = 9999, state = "CLOSED", author = new { login = "selected" },
                    repository = new { nameWithOwner = "other/private" }, headRefOid = Id(9999), baseRefOid = BaseHead, commits = new { totalCount = 1 } }];
                if (query.Contains("repository(owner:$owner,name:$name)", StringComparison.Ordinal))
                {
                    string owner = arguments.Single(value => value.StartsWith("owner=", StringComparison.Ordinal))[6..];
                    string name = arguments.Single(value => value.StartsWith("name=", StringComparison.Ordinal))[5..];
                    int repositoryIndex = Enumerable.Range(0, RepositoryCount).Single(index => Repository(index) == owner + "/" + name);
                    object[] scoped = [.. nodes.Take(population).Where((_, index) => index % RepositoryCount == repositoryIndex)];
                    string? cursor = arguments.FirstOrDefault(value => value.StartsWith("endCursor=", StringComparison.Ordinal));
                    int pageIndex = cursor is null ? 0 : int.Parse(cursor["endCursor=synthetic-".Length..], CultureInfo.InvariantCulture) + 1;
                    response = new
                    {
                        data = new
                        {
                            repository = new
                            {
                                pullRequests = new
                                {
                                    totalCount = InvalidScopedTotal ? 1001 : scoped.Length,
                                    nodes = scoped.Skip(pageIndex * 100).Take(100).ToArray(),
                                    pageInfo = new
                                    {
                                        hasNextPage = (pageIndex + 1) * 100 < scoped.Length,
                                        endCursor = RepeatedScopedCursor ? "synthetic-0" : "synthetic-" + pageIndex
                                    }
                                }
                            }
                        },
                        padding = new string('x', ScopedPaddingCharacters)
                    };
                }
                else
                    response = nodes.Chunk(100).Select((page, index) => new
                    {
                        data = new
                        {
                            user = new
                            {
                                pullRequests = new
                                {
                                    totalCount = nodes.Length,
                                    nodes = page,
                                    pageInfo = new { hasNextPage = (index + 1) * 100 < nodes.Length, endCursor = "synthetic-" + index },
                                }
                            }
                        }
                    }).ToArray();
            }
            else if (query.Contains("pullRequest(number:", StringComparison.Ordinal))
            {
                if (FailBatch) return new(1, "", "HTTP 500 synthetic failure");
                Dictionary<string, object> data = [];
                for (int index = 0; ; index++)
                {
                    string? numberText = arguments.FirstOrDefault(value => value.StartsWith($"number{index}=", StringComparison.Ordinal));
                    if (numberText is null) break;
                    int number = int.Parse(numberText.Split('=')[1], CultureInfo.InvariantCulture);
                    data.Add("r" + index, new
                    {
                        pullRequest = new
                        {
                            headRefOid = ChangedDuringBatch ? Id(8888) : Id(number),
                            baseRefOid = BaseHead,
                            commits = new { totalCount = 1, pageInfo = new { hasNextPage = IncompleteBatch }, nodes = new[] { new { commit = GraphCommit(number) } } }
                        }
                    });
                }
                response = PartialBatchError
                    ? (object)new { data, errors = new[] { new { path = new[] { "r0", "pullRequest", "commits" } } } }
                    : new { data };
            }
            else
            {
                string endpoint = arguments.Single(value => value.StartsWith("repos/", StringComparison.Ordinal));
                int number = int.Parse(endpoint.Split('/')[4].Split('?')[0], CultureInfo.InvariantCulture);
                response = endpoint.Contains("/commits", StringComparison.Ordinal)
                    ? new[] { new[] { RestCommit(number) } }
                    : (object)new { commits = 1, head = new { sha = Id(number) }, @base = new { sha = BaseHead } };
            }
            return new(0, JsonSerializer.Serialize(response), "") { ProcessStartupElapsed = TimeSpan.FromMilliseconds(1) };
        }
        finally { Interlocked.Decrement(ref _active); }
    }

    private object GraphCommit(int number) => new
    {
        oid = Id(number),
        parents = new { totalCount = 1, nodes = (IncompleteParents && number == 1 ? Array.Empty<string>() : [BaseHead]).Select(oid => new { oid }).ToArray() },
        author = new { name = "Selected", email = "selected@example.invalid", user = new { login = "selected" } },
        authoredDate = number == 7 ? "2026-01-19T12:00:00Z" : "2025-01-01T12:00:00Z",
        committer = new { name = "Integrator", email = "other@example.invalid" },
        committedDate = "2026-03-13T12:00:00Z",
        message = "synthetic",
    };
    private object RestCommit(int number) => new
    {
        sha = Id(number),
        parents = new[] { new { sha = BaseHead } },
        author = new { login = "selected" },
        commit = new
        {
            author = new { name = "Selected", email = "selected@example.invalid", date = number == 7 ? "2026-01-19T12:00:00Z" : "2025-01-01T12:00:00Z" },
            committer = new { name = "Integrator", email = "other@example.invalid", date = "2026-03-13T12:00:00Z" },
            message = "synthetic"
        },
    };

    public static Task<IReadOnlyList<DiscoveredRepository>?> DiscoverAsync(IExternalCommandRunner runner, ProviderQueryCounters counters, CancellationToken token = default) =>
        GitHubAuthorPeriodDiscoveryJson.DiscoverUserOpenPullHeadsAccountWideAsync(runner, "in-memory-fixture",
            [new GitHubDiscoveryRepository("42", "owner/project", "main")], "selected", ["selected@example.invalid"], Since, Since.AddDays(5),
            ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include, counters, token, true);

    public static Task<IReadOnlyList<DiscoveredRepository>?> DiscoverScopeAsync(HistoricalPullProviderFixture runner,
        ProviderQueryCounters counters, bool annual, int includedRepositories, CancellationToken token = default) =>
        GitHubAuthorPeriodDiscoveryJson.DiscoverUserOpenPullHeadsAccountWideAsync(runner, "in-memory-fixture",
            [.. Enumerable.Range(0, includedRepositories).Select(index => new GitHubDiscoveryRepository(
                (42 + index).ToString(CultureInfo.InvariantCulture), Repository(index), "main"))],
            "selected", ["selected@example.invalid"], annual ? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) : Since,
            annual ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) : Since.AddDays(5),
            ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include, counters, token, true);

    public static Task<IReadOnlyList<DiscoveredRepository>?> DiscoverRestrictedAsync(HistoricalPullProviderFixture runner,
        ProviderQueryCounters counters, int includedRepositories = 1, CancellationToken token = default) =>
        GitHubAuthorPeriodDiscoveryJson.DiscoverHistoricalPullHeadsInScopeAsync(runner, "in-memory-fixture",
            [.. Enumerable.Range(0, includedRepositories).Select(index => new GitHubDiscoveryRepository(
                (42 + index).ToString(CultureInfo.InvariantCulture), Repository(index), "main"))],
            "selected", ["selected@example.invalid"], Since, Since.AddDays(5), ChangePortfolioDateField.Author,
            ChangePortfolioMergePolicy.Exclude, ChangePortfolioCoauthorPolicy.Include, counters, token);

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            int prior;
            do { prior = Volatile.Read(ref location); if (prior >= value) return; }
            while (Interlocked.CompareExchange(ref location, value, prior) != prior);
        }
    }
}

internal sealed class MemoryPullMetadataCache : IGitHubPullMetadataCache
{
    private readonly ConcurrentDictionary<(string Repository, int Number, string Head, string Base, int Count), GitHubPullMetadata> _entries = new();
    public Task<GitHubPullMetadata?> ReadAsync(string repository, int number, string head, string baseHead, int count, CancellationToken token) =>
        Task.FromResult(_entries.GetValueOrDefault((repository, number, head, baseHead, count)));
    public Task WriteAsync(string repository, int number, GitHubPullMetadata metadata, CancellationToken token)
    {
        _entries[(repository, number, metadata.Head, metadata.Base, metadata.Commits.Count)] = metadata;
        return Task.CompletedTask;
    }
}
