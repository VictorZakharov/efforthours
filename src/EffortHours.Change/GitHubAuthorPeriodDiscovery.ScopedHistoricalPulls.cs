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
            List<JsonElement> pages = [];
            HashSet<string> cursors = new(StringComparer.Ordinal);
            string? cursor = null;
            int responseCharacters = 0;
            for (int pageIndex = 0; pageIndex < 10; pageIndex++)
            {
                token.ThrowIfCancellationRequested();
                List<string> args = ["api", "graphql", "-f", "query=" + ScopedHistoricalPullQuery,
                    "-F", "owner=" + parts[0], "-F", "name=" + parts[1]];
                if (cursor is not null) args.AddRange(["-F", "endCursor=" + cursor]);
                string? json = await RunApiAsync(commands, directory, args, counters, false, false, token,
                    capabilityFallback: true, failurePhase: GitHubProviderFailure.OpenPullRequestPhase).ConfigureAwait(false);
                if (json is null) return null;
                if (json.Length > MaximumResponseCharacters - responseCharacters)
                    throw GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.OpenPullRequestPhase,
                        "Scoped historical inventory exceeded its 16-Mi-character response bound; no evidence was truncated.",
                        "inspect-pr-discovery-or-use-pinned-manifest");
                responseCharacters += json.Length;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("errors", out _)) return null;
                    JsonElement connection = document.RootElement.GetProperty("data").GetProperty("repository").GetProperty("pullRequests");
                    if (connection.GetProperty("totalCount").GetInt32() > 1000) return null;
                    JsonElement info = connection.GetProperty("pageInfo");
                    pages.Add(document.RootElement.Clone());
                    if (!info.GetProperty("hasNextPage").GetBoolean())
                    {
                        AccountPullRequest[]? parsed = ParseCompleteAccountPulls(JsonSerializer.Serialize(pages), login, historical: true, repositoryConnection: true);
                        return parsed is not null && parsed.All(pull => pull.RepositoryIdentity.Equals(identity, StringComparison.OrdinalIgnoreCase)) ? parsed : null;
                    }
                    cursor = info.GetProperty("endCursor").GetString();
                    if (string.IsNullOrWhiteSpace(cursor) || !cursors.Add(cursor)) return null;
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
                { return null; }
            }
            return null;
        }
    }

    private const string ScopedHistoricalPullQuery =
        "query($endCursor:String,$owner:String!,$name:String!){repository(owner:$owner,name:$name)" +
        "{pullRequests(first:100,after:$endCursor,states:[OPEN,CLOSED,MERGED])" +
        "{totalCount nodes{state headRefOid baseRefOid commits{totalCount} number author{login} repository{nameWithOwner}}" +
        "pageInfo{hasNextPage endCursor}}}}";
}
