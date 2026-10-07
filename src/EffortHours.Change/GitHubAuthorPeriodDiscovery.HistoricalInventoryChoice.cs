using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    public static async Task<IReadOnlyList<DiscoveredRepository>?> DiscoverHistoricalPullHeadsOptimizedAsync(
        IExternalCommandRunner commands, string directory, IReadOnlyList<GitHubDiscoveryRepository> repositories,
        string login, IReadOnlyList<string> aliases, DateTimeOffset since, DateTimeOffset until,
        ChangePortfolioDateField dateField, ChangePortfolioMergePolicy mergePolicy, ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters, CancellationToken token)
    {
        bool account = await PreferHistoricalAccountInventoryAsync(commands, directory, repositories, login, counters, token).ConfigureAwait(false);
        if (account)
        {
            var result = await DiscoverHistoricalAccountPullHeadsAsync(commands, directory, repositories, login, aliases,
                since, until, dateField, mergePolicy, coauthorPolicy, counters, token).ConfigureAwait(false);
            if (result is not null) return result;
            counters.AddFallback("open-pr", "account-connection-unavailable", repositories.Count);
        }
        counters.HistoricalInventoryStrategy = "scoped-connections";
        return await DiscoverHistoricalPullHeadsInScopeAsync(commands, directory, repositories, login, aliases,
            since, until, dateField, mergePolicy, coauthorPolicy, counters, token).ConfigureAwait(false);
    }

    private static async Task<bool> PreferHistoricalAccountInventoryAsync(IExternalCommandRunner commands,
        string directory, IReadOnlyList<GitHubDiscoveryRepository> repositories, string login,
        ProviderQueryCounters counters, CancellationToken token)
    {
        // Counts choose a complete traversal only; they never admit or omit a candidate.
        var groups = repositories.Chunk(12).ToArray();
        using CancellationTokenSource children = CancellationTokenSource.CreateLinkedTokenSource(token);
        Exception? failure = null;
        Task<(int Account, int Repositories)?>[] tasks = [.. groups.Select(async group =>
        {
            await HistoricalPullGate.WaitAsync(children.Token).ConfigureAwait(false);
            try
            {
                StringBuilder query = new("query($login:String!");
                for (int index = 0; index < group.Length; index++)
                    query.Append(CultureInfo.InvariantCulture, $",$owner{index}:String!,$name{index}:String!");
                query.Append("){user(login:$login){pullRequests(first:1,states:[OPEN,CLOSED,MERGED]){totalCount}}");
                for (int index = 0; index < group.Length; index++)
                    query.Append(CultureInfo.InvariantCulture, $"r{index}:repository(owner:$owner{index},name:$name{index}){{pullRequests(first:1,states:[OPEN,CLOSED,MERGED]){{totalCount}}}}");
                query.Append('}');
                List<string> args = ["api", "graphql", "-f", "query=" + query, "-F", "login=" + login];
                for (int index = 0; index < group.Length; index++)
                {
                    string[] identity = group[index].Identity.Split('/');
                    args.AddRange(["-F", $"owner{index}={identity[0]}", "-F", $"name{index}={identity[1]}"]);
                }
                string? json = await RunApiAsync(commands, directory, args, counters, false, false, children.Token,
                    capabilityFallback: true, failurePhase: GitHubProviderFailure.HistoricalInventoryPhase,
                    maximumResponseCharacters: HistoricalPullInventory.MaximumPageCharacters).ConfigureAwait(false);
                if (json is null) return null;
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("errors", out _)) return null;
                    JsonElement data = document.RootElement.GetProperty("data");
                    int count = data.GetProperty("user").GetProperty("pullRequests").GetProperty("totalCount").GetInt32();
                    if (count < 0) return null;
                    int pages = 0;
                    for (int index = 0; index < group.Length; index++)
                    {
                        int repositoryCount = data.GetProperty("r" + index).GetProperty("pullRequests").GetProperty("totalCount").GetInt32();
                        if (repositoryCount < 0) return null;
                        pages = checked(pages + Math.Max(1, (int)((repositoryCount + 99L) / 100)));
                    }
                    return ((int Account, int Repositories)?)(count, pages);
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or OverflowException)
                { return null; }
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Interlocked.CompareExchange(ref failure, exception, null);
                await children.CancelAsync().ConfigureAwait(false);
                throw;
            }
            finally { HistoricalPullGate.Release(); }
        })];
        (int Account, int Repositories)?[] counts;
        try { counts = await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch when (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); throw; }
        if (counts.Length == 0 || counts.Any(value => value is null) || counts.Select(value => value!.Value.Account).Distinct().Count() != 1)
            return false;
        long accountPages = Math.Max(1, (counts[0]!.Value.Account + 99L) / 100);
        return accountPages < counts.Sum(value => (long)value!.Value.Repositories);
    }
}
