using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    // The caller owns a bounded pipeline slot; headers, cache reads and misses progress together.
    private static async Task<AccountPullRequest[]> ResolveHistoricalPullHeadersBatchAsync(IExternalCommandRunner commands,
        string directory, AccountPullRequest[] pulls, ProviderQueryCounters counters, CancellationToken token)
    {
        AccountPullRequest[] result = (AccountPullRequest[])pulls.Clone();
        var unknown = pulls.Select((pull, index) => (pull, index))
            .Where(value => value.pull.CommitCount is null || value.pull.BaseObjectId is null).ToArray();
        if (unknown.Length == 0) return result;
        StringBuilder query = new("query(");
        for (int index = 0; index < unknown.Length; index++)
        {
            if (index > 0) query.Append(',');
            query.Append(CultureInfo.InvariantCulture, $"$owner{index}:String!,$name{index}:String!,$number{index}:Int!");
        }
        query.Append("){ ");
        for (int index = 0; index < unknown.Length; index++) query.Append(CultureInfo.InvariantCulture, $"r{index}:repository(owner:$owner{index},name:$name{index}){{pullRequest(number:$number{index}){{headRefOid baseRefOid commits{{totalCount}}}}}} ");
        query.Append('}');
        List<string> args = ["api", "graphql", "-f", "query=" + query];
        for (int index = 0; index < unknown.Length; index++)
        {
            string[] parts = unknown[index].pull.RepositoryIdentity.Split('/');
            args.AddRange(["-F", $"owner{index}={parts[0]}", "-F", $"name{index}={parts[1]}", "-F", $"number{index}={unknown[index].pull.Number}"]);
        }
        string? json = await RunApiAsync(commands, directory, args, counters, false, false, token,
            capabilityFallback: true, failurePhase: GitHubProviderFailure.HistoricalHeaderPhase,
            maximumResponseCharacters: HistoricalPullInventory.MaximumPageCharacters).ConfigureAwait(false);
        using JsonDocument? document = ParseHistoricalPullBatchDocument(json);
        for (int index = 0; index < unknown.Length; index++)
        {
            AccountPullRequest pull = unknown[index].pull;
            string head, baseHead;
            int count;
            if (document is not null && TryHistoricalHeader(document.RootElement, "r" + index, out JsonElement header))
            {
                head = RequireObjectId(header.GetProperty("headRefOid").GetString(), "historical head");
                baseHead = RequireObjectId(header.GetProperty("baseRefOid").GetString(), "historical base");
                count = header.GetProperty("commits").GetProperty("totalCount").GetInt32();
            }
            else
            {
                string detail = await RunRequiredApiAsync(commands, directory,
                    ["api", $"repos/{pull.RepositoryIdentity}/pulls/{pull.Number}", "--jq", "{commits,head:{sha:.head.sha},base:{sha:.base.sha}}"],
                    counters, false, token, failurePhase: GitHubProviderFailure.HistoricalHeaderPhase).ConfigureAwait(false);
                using JsonDocument rest = JsonDocument.Parse(detail);
                head = RequireObjectId(rest.RootElement.GetProperty("head").GetProperty("sha").GetString(), "historical head");
                baseHead = RequireObjectId(rest.RootElement.GetProperty("base").GetProperty("sha").GetString(), "historical base");
                count = rest.RootElement.GetProperty("commits").GetInt32();
            }
            if (head != pull.ObjectId || pull.BaseObjectId is not null && baseHead != pull.BaseObjectId ||
                pull.CommitCount is not null && count != pull.CommitCount || count is <= 0 or > 250)
                throw GitHubProviderFailure.Malformed(GitHubProviderFailure.HistoricalHeaderPhase,
                    new InvalidOperationException("Incomplete or changed historical PR header."));
            result[unknown[index].index] = pull with { BaseObjectId = baseHead, CommitCount = count };
        }
        return result;
    }

    private static bool TryHistoricalHeader(JsonElement root, string alias, out JsonElement header)
    {
        header = default;
        if (root.TryGetProperty("errors", out JsonElement errors) && errors.EnumerateArray().Any(error =>
            !error.TryGetProperty("path", out JsonElement path) || path.ValueKind != JsonValueKind.Array || path.GetArrayLength() == 0 ||
            path[0].GetString() == alias)) return false;
        return root.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(alias, out JsonElement repository) && repository.ValueKind == JsonValueKind.Object &&
            repository.TryGetProperty("pullRequest", out header) && header.ValueKind == JsonValueKind.Object &&
            header.TryGetProperty("headRefOid", out _) && header.TryGetProperty("baseRefOid", out _) &&
            header.TryGetProperty("commits", out JsonElement commits) && commits.ValueKind == JsonValueKind.Object && commits.TryGetProperty("totalCount", out _);
    }
}
