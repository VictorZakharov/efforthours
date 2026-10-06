using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static async Task<AccountPullRequest[]> ResolveHistoricalPullHeadersAsync(IExternalCommandRunner commands,
        string directory, AccountPullRequest[] pulls, ProviderQueryCounters counters, CancellationToken token)
    {
        AccountPullRequest[] result = (AccountPullRequest[])pulls.Clone();
        var batches = pulls.Select((pull, index) => (pull, index)).Where(value => value.pull.CommitCount is null || value.pull.BaseObjectId is null)
            .GroupBy(value => value.pull.RepositoryIdentity, StringComparer.OrdinalIgnoreCase).SelectMany(group => group.Chunk(12));
        using CancellationTokenSource children = CancellationTokenSource.CreateLinkedTokenSource(token);
        Exception? root = null;
        Task[] tasks = [.. batches.Select(async batch =>
        {
            await HistoricalPullGate.WaitAsync(children.Token).ConfigureAwait(false);
            try
            {
                StringBuilder query = new("query(");
                for (int index = 0; index < batch.Length; index++) query.Append(CultureInfo.InvariantCulture, $"$owner{index}:String!,$name{index}:String!,$number{index}:Int!,");
                query.Append("){ ");
                for (int index = 0; index < batch.Length; index++) query.Append(CultureInfo.InvariantCulture, $"r{index}:repository(owner:$owner{index},name:$name{index}){{pullRequest(number:$number{index}){{headRefOid baseRefOid commits{{totalCount}}}}}} ");
                query.Append('}');
                List<string> args = ["api", "graphql", "-f", "query=" + query];
                for (int index = 0; index < batch.Length; index++)
                {
                    string[] parts = batch[index].pull.RepositoryIdentity.Split('/');
                    args.AddRange(["-F", $"owner{index}={parts[0]}", "-F", $"name{index}={parts[1]}", "-F", $"number{index}={batch[index].pull.Number}"]);
                }
                string? json = await RunApiAsync(commands, directory, args, counters, false, false, children.Token,
                    capabilityFallback: true, failurePhase: GitHubProviderFailure.OpenPullRequestPhase,
                    maximumResponseCharacters: HistoricalPullInventory.MaximumPageCharacters).ConfigureAwait(false);
                using JsonDocument? document = ParseHistoricalPullBatchDocument(json);
                for (int index = 0; index < batch.Length; index++)
                {
                    AccountPullRequest pull = batch[index].pull;
                    JsonElement header = default;
                    bool complete = document is not null && TryHistoricalHeader(document.RootElement, "r" + index, out header);
                    if (!complete)
                    {
                        string detail = await RunApiAsync(commands, directory,
                            ["api", $"repos/{pull.RepositoryIdentity}/pulls/{pull.Number}", "--jq", "{commits,head:{sha:.head.sha},base:{sha:.base.sha}}"],
                            counters, false, false, children.Token, failurePhase: GitHubProviderFailure.OpenPullRequestPhase).ConfigureAwait(false)
                            ?? throw new InvalidOperationException("Missing historical PR identity.");
                        using JsonDocument rest = JsonDocument.Parse(detail);
                        string head = RequireObjectId(rest.RootElement.GetProperty("head").GetProperty("sha").GetString(), "historical head");
                        if (head != pull.ObjectId) throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, new JsonException());
                        result[batch[index].index] = pull with { BaseObjectId = RequireObjectId(rest.RootElement.GetProperty("base").GetProperty("sha").GetString(), "historical base"), CommitCount = rest.RootElement.GetProperty("commits").GetInt32() };
                        continue;
                    }
                    string frozenHead = RequireObjectId(header.GetProperty("headRefOid").GetString(), "historical head");
                    if (frozenHead != pull.ObjectId) throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, new JsonException());
                    result[batch[index].index] = pull with { BaseObjectId = RequireObjectId(header.GetProperty("baseRefOid").GetString(), "historical base"), CommitCount = header.GetProperty("commits").GetProperty("totalCount").GetInt32() };
                }
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException) Interlocked.CompareExchange(ref root, exception, null);
                await children.CancelAsync().ConfigureAwait(false);
                throw;
            }
            finally { HistoricalPullGate.Release(); }
        })];
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch when (root is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(root).Throw(); throw; }
        return result;
    }

    private static bool TryHistoricalHeader(JsonElement root, string alias, out JsonElement header)
    {
        header = default;
        return !root.TryGetProperty("errors", out _) && root.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(alias, out JsonElement repository) && repository.ValueKind == JsonValueKind.Object &&
            repository.TryGetProperty("pullRequest", out header) && header.ValueKind == JsonValueKind.Object &&
            header.TryGetProperty("headRefOid", out _) && header.TryGetProperty("baseRefOid", out _) &&
            header.TryGetProperty("commits", out JsonElement commits) && commits.ValueKind == JsonValueKind.Object && commits.TryGetProperty("totalCount", out _);
    }
}
