using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    public static async Task<IReadOnlyList<DiscoveredRepository>?> DiscoverHistoricalPullHeadsInScopeAsync(
        IExternalCommandRunner commands, string directory, IReadOnlyList<GitHubDiscoveryRepository> repositories,
        string login, IReadOnlyList<string> aliases, DateTimeOffset since, DateTimeOffset until,
        ChangePortfolioDateField dateField, ChangePortfolioMergePolicy mergePolicy, ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters, CancellationToken token)
    {
        using CancellationTokenSource children = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = children.Token;
        Exception? rootFailure = null;
        Task<AccountPullRequest[]?>[] tasks = [.. repositories.Select(async repository =>
        {
            await HistoricalPullGate.WaitAsync(token).ConfigureAwait(false);
            try { return await Read(repository.Identity).ConfigureAwait(false); }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Interlocked.CompareExchange(ref rootFailure, exception, null);
                await children.CancelAsync().ConfigureAwait(false);
                throw;
            }
            finally { HistoricalPullGate.Release(); }
        })];
        AccountPullRequest[]?[] inventories;
        try { inventories = await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch when (rootFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(rootFailure).Throw(); throw; }
        if (inventories.Any(inventory => inventory is null)) return null;
        AccountPullRequest[] pulls = [.. inventories.SelectMany(inventory => inventory!).OrderBy(pull => pull.RepositoryIdentity, StringComparer.Ordinal)
            .ThenBy(pull => pull.Number)];
        counters.AddOpenPullRequests(pulls.Count(pull => pull.Open));
        counters.AddHistoricalPullRequests(pulls.Count(pull => !pull.Open));
        counters.AddPullCandidateRepositories(pulls.Select(pull => pull.RepositoryIdentity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        return await DiscoverHistoricalPullsBatchedAsync(commands, directory, repositories, pulls, aliases, since, until,
            dateField, mergePolicy, coauthorPolicy, counters, token).ConfigureAwait(false);

        async Task<AccountPullRequest[]?> Read(string identity)
        {
            string[] parts = identity.Split('/');
            HistoricalPullInventory inventory = new(identity);
            HashSet<string> cursors = new(StringComparer.Ordinal);
            string? cursor = null;
            int? total = null;
            int observed = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                List<string> args = ["api", "graphql", "-f", "query=" + ScopedHistoricalPullQuery,
                    "-F", "owner=" + parts[0], "-F", "name=" + parts[1]];
                if (cursor is not null) args.AddRange(["-F", "endCursor=" + cursor]);
                string? json = await RunApiAsync(commands, directory, args, counters, false, false, token,
                    capabilityFallback: true, failurePhase: GitHubProviderFailure.OpenPullRequestPhase,
                    maximumResponseCharacters: HistoricalPullInventory.MaximumPageCharacters).ConfigureAwait(false);
                if (json is null) return null;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("errors", out _)) return null;
                    JsonElement connection = document.RootElement.GetProperty("data").GetProperty("repository").GetProperty("pullRequests");
                    int count = connection.GetProperty("totalCount").GetInt32();
                    if (count < 0 || total is not null && total != count) return null;
                    total = count;
                    JsonElement nodes = connection.GetProperty("nodes");
                    if (nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() > 100) return null;
                    foreach (JsonElement node in nodes.EnumerateArray())
                    {
                        if (!inventory.Observe(node.GetProperty("number").GetInt32())) return null;
                        observed++;
                        if (!string.Equals(node.GetProperty("author").GetProperty("login").GetString(), login, StringComparison.OrdinalIgnoreCase)) continue;
                        string repository = RequireRepositoryIdentity(node.GetProperty("repository").GetProperty("nameWithOwner").GetString());
                        if (!repository.Equals(identity, StringComparison.OrdinalIgnoreCase)) return null;
                        string state = node.GetProperty("state").GetString() ?? "";
                        if (state is not ("OPEN" or "CLOSED" or "MERGED")) return null;
                        inventory.Add(new(repository, node.GetProperty("number").GetInt32(), state == "OPEN",
                            RequireObjectId(node.GetProperty("headRefOid").GetString(), "historical head"),
                            node.GetProperty("commits").GetProperty("totalCount").GetInt32(),
                            RequireObjectId(node.GetProperty("baseRefOid").GetString(), "historical base")));
                    }
                    JsonElement info = connection.GetProperty("pageInfo");
                    if (!info.GetProperty("hasNextPage").GetBoolean()) return total == observed ? inventory.Complete() : null;
                    if (nodes.GetArrayLength() == 0 || observed >= total) return null;
                    cursor = info.GetProperty("endCursor").GetString();
                    if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 1024 || !cursors.Add(cursor)) return null;
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException && exception is not GitHubProviderException)
                { return null; }
            }
        }
    }

    private const string ScopedHistoricalPullQuery =
        "query($endCursor:String,$owner:String!,$name:String!){repository(owner:$owner,name:$name)" +
        "{pullRequests(first:100,after:$endCursor,states:[OPEN,CLOSED,MERGED])" +
        "{totalCount nodes{state headRefOid baseRefOid commits{totalCount} number author{login} repository{nameWithOwner}}" +
        "pageInfo{hasNextPage endCursor}}}}";
}
