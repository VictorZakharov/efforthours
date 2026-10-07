using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static async Task<IReadOnlyList<DiscoveredRepository>?> DiscoverHistoricalAccountPullHeadsAsync(
        IExternalCommandRunner commands, string directory, IReadOnlyList<GitHubDiscoveryRepository> repositories,
        string login, IReadOnlyList<string> aliases, DateTimeOffset since, DateTimeOffset until,
        ChangePortfolioDateField dateField, ChangePortfolioMergePolicy mergePolicy, ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters, CancellationToken token)
    {
        counters.AddAccountQuery();
        counters.HistoricalInventoryStrategy = "account-connection";
        Dictionary<string, HistoricalPullInventory> inventories = repositories.ToDictionary(value => value.Identity,
            value => new HistoricalPullInventory(value.Identity), StringComparer.OrdinalIgnoreCase);
        HashSet<(string Repository, int Number)> seen = [];
        HashSet<string> cursors = new(StringComparer.Ordinal);
        int? total = null;
        int observed = 0, charged = 0;
        string? cursor = null;
        await HistoricalPullGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            while (true)
            {
                List<string> args = ["api", "graphql", "-f", "query=" + HistoricalAccountInventoryQuery, "-F", "login=" + login];
                if (cursor is not null) args.AddRange(["-F", "endCursor=" + cursor]);
                string? json = await RunApiAsync(commands, directory, args, counters, false, false, token,
                    capabilityFallback: true, failurePhase: GitHubProviderFailure.HistoricalInventoryPhase,
                    maximumResponseCharacters: HistoricalPullInventory.MaximumPageCharacters).ConfigureAwait(false);
                if (json is null) return null;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("errors", out _)) return null;
                    JsonElement connection = document.RootElement.GetProperty("data").GetProperty("user").GetProperty("pullRequests");
                    int count = connection.GetProperty("totalCount").GetInt32();
                    if (count < 0 || total is not null && total != count) return null;
                    total = count;
                    JsonElement nodes = connection.GetProperty("nodes");
                    if (nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() > 100) return null;
                    foreach (JsonElement node in nodes.EnumerateArray())
                    {
                        int number = node.GetProperty("number").GetInt32();
                        string identity = RequireRepositoryIdentity(node.GetProperty("repository").GetProperty("nameWithOwner").GetString());
                        if (number <= 0 || !seen.Add((identity.ToLowerInvariant(), number))) return null;
                        observed++;
                        charged = checked(charged + 64 + 2 * identity.Length);
                        if (charged > 16 * 1024 * 1024)
                            throw GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.HistoricalInventoryPhase,
                                "Historical account inventory-ledger-byte bound exceeded: limit 16777216. No candidates were truncated.",
                                "inspect-pr-discovery-or-use-pinned-manifest");
                        // Out-of-scope PRs need no detail, commit evidence, cache entry or acquisition.
                        if (!inventories.TryGetValue(identity, out HistoricalPullInventory? inventory)) continue;
                        JsonElement author = node.GetProperty("author");
                        if (author.ValueKind == JsonValueKind.Null ||
                            !string.Equals(author.GetProperty("login").GetString(), login, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!inventory.Observe(number)) return null;
                        string state = node.GetProperty("state").GetString() ?? "";
                        if (state is not ("OPEN" or "CLOSED" or "MERGED")) return null;
                        inventory.Add(new(identity, number, state == "OPEN",
                            RequireObjectId(node.GetProperty("headRefOid").GetString(), "historical head"),
                            node.GetProperty("commits").GetProperty("totalCount").GetInt32(),
                            RequireObjectId(node.GetProperty("baseRefOid").GetString(), "historical base")));
                    }
                    JsonElement info = connection.GetProperty("pageInfo");
                    if (!info.GetProperty("hasNextPage").GetBoolean())
                    {
                        if (total != observed) return null;
                        break;
                    }
                    if (nodes.GetArrayLength() == 0 || observed >= total) return null;
                    cursor = info.GetProperty("endCursor").GetString();
                    if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 1024 || !cursors.Add(cursor)) return null;
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException && exception is not GitHubProviderException)
                { return null; }
            }
        }
        finally { HistoricalPullGate.Release(); }
        AccountPullRequest[] pulls = [.. inventories.Values.SelectMany(inventory => inventory.Complete())
            .OrderBy(pull => pull.RepositoryIdentity, StringComparer.Ordinal).ThenBy(pull => pull.Number)];
        counters.AddOpenPullRequests(pulls.Count(pull => pull.Open));
        counters.AddHistoricalPullRequests(pulls.Count(pull => !pull.Open));
        counters.AddPullCandidateRepositories(pulls.Select(pull => pull.RepositoryIdentity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        return await DiscoverHistoricalPullsBatchedAsync(commands, directory, repositories, pulls, aliases, since, until,
            dateField, mergePolicy, coauthorPolicy, counters, token).ConfigureAwait(false);
    }

    private const string HistoricalAccountInventoryQuery =
        "query($endCursor:String,$login:String!){user(login:$login){pullRequests(first:100,after:$endCursor,states:[OPEN,CLOSED,MERGED])" +
        "{totalCount nodes{number state author{login} repository{nameWithOwner} headRefOid baseRefOid commits{totalCount}}" +
        "pageInfo{hasNextPage endCursor}}}}";
}
