using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static JsonDocument? ParseHistoricalPullBatchDocument(string? json)
    {
        try { return json is null ? null : JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
    }

    private static GitHubPullMetadata? ParseHistoricalPullBatch(JsonElement? element, int index, AccountPullRequest expected)
    {
        if (element is not { } root) return null;
        try
        {
            // Per-alias errors can affect one PR; independently validate every complete sibling.
            if (root.TryGetProperty("errors", out JsonElement errors) && errors.EnumerateArray().Any(error =>
                !error.TryGetProperty("path", out JsonElement path) || path.ValueKind != JsonValueKind.Array || path.GetArrayLength() == 0 ||
                path[0].GetString() == "r" + index)) return null;
            JsonElement pull = root.GetProperty("data").GetProperty("r" + index).GetProperty("pullRequest");
            string head = RequireObjectId(pull.GetProperty("headRefOid").GetString(), "retained PR head");
            string baseHead = RequireObjectId(pull.GetProperty("baseRefOid").GetString(), "retained PR base");
            JsonElement connection = pull.GetProperty("commits");
            int count = connection.GetProperty("totalCount").GetInt32();
            if (expected.ObjectId is not null && head != expected.ObjectId || expected.BaseObjectId is not null && baseHead != expected.BaseObjectId ||
                expected.CommitCount is not null && count != expected.CommitCount)
                throw GitHubProviderFailure.Malformed(GitHubProviderFailure.HistoricalMetadataPhase, new InvalidOperationException("Retained PR changed during discovery."));
            if (count is <= 0 or > 250 || connection.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()) return null;
            List<GitHubPullCommitEvidence> commits = [];
            foreach (JsonElement node in connection.GetProperty("nodes").EnumerateArray())
            {
                JsonElement value = node.GetProperty("commit");
                JsonElement parents = value.GetProperty("parents");
                int parentCount = parents.GetProperty("totalCount").GetInt32();
                if (parentCount is < 0 or > 2 || parents.GetProperty("nodes").GetArrayLength() != parentCount) return null;
                commits.Add(new(ParseGraphCommit(value), GraphAuthorLogin(value)));
            }
            if (commits.Count != count || commits[^1].Commit.ObjectId != head || commits.Select(value => value.Commit.ObjectId).Distinct().Count() != count) return null;
            return new(head, baseHead, commits);
        }
        catch (GitHubProviderException) { throw; }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }

    private static async Task<GitHubPullMetadata> ResolveHistoricalPullRestAsync(IExternalCommandRunner commands, string directory,
        AccountPullRequest pull, ProviderQueryCounters counters, CancellationToken token)
    {
        string detail = await RunRequiredApiAsync(commands, directory, ["api", $"repos/{pull.RepositoryIdentity}/pulls/{pull.Number}"], counters, false, token, failurePhase: GitHubProviderFailure.HistoricalMetadataPhase).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(detail);
        JsonElement root = document.RootElement;
        string head = RequireObjectId(root.GetProperty("head").GetProperty("sha").GetString(), "retained PR head");
        string baseHead = root.TryGetProperty("base", out JsonElement baseValue) ? RequireObjectId(baseValue.GetProperty("sha").GetString(), "retained PR base") : string.Empty;
        int count = root.GetProperty("commits").GetInt32();
        if (count is <= 0 or > 250 || pull.ObjectId is not null && pull.ObjectId != head ||
            pull.BaseObjectId is not null && pull.BaseObjectId != baseHead || pull.CommitCount is not null && pull.CommitCount != count)
            throw GitHubProviderFailure.Malformed(GitHubProviderFailure.HistoricalMetadataPhase, new InvalidOperationException("Incomplete or changed retained PR metadata."));
        string json = await RunRequiredApiAsync(commands, directory, ["api", "--paginate", "--slurp",
            $"repos/{pull.RepositoryIdentity}/pulls/{pull.Number}/commits?per_page=100"], counters, true, token, failurePhase: GitHubProviderFailure.HistoricalMetadataPhase).ConfigureAwait(false);
        using JsonDocument commitsDocument = JsonDocument.Parse(json);
        GitHubPullCommitEvidence[] commits = [.. Pages(commitsDocument.RootElement).Select(value => new GitHubPullCommitEvidence(ParseCommit(value), ProviderAuthorLogin(value)))];
        if (commits.Length != count || commits[^1].Commit.ObjectId != head || commits.Select(value => value.Commit.ObjectId).Distinct().Count() != count)
            throw GitHubProviderFailure.Malformed(GitHubProviderFailure.HistoricalMetadataPhase, new InvalidOperationException("Incomplete retained PR commits."));
        return new(head, baseHead, commits);
    }

    private static string HistoricalPullBatchQuery(int count)
    {
        StringBuilder query = new("query(");
        for (int index = 0; index < count; index++)
        {
            if (index > 0) query.Append(',');
            query.Append(CultureInfo.InvariantCulture, $"$owner{index}:String!,$name{index}:String!,$number{index}:Int!");
        }
        query.Append("){");
        for (int index = 0; index < count; index++) query.Append($"r{index}:repository(owner:$owner{index},name:$name{index})" +
            $"{{pullRequest(number:$number{index}){{headRefOid baseRefOid commits(first:100){{totalCount pageInfo{{hasNextPage}} nodes{{commit{{" +
            "oid parents(first:2){totalCount nodes{oid}} author{name email user{login}} authoredDate " +
            "committer{name email user{login}} committedDate message}}}}}");
        return query.Append('}').ToString();
    }
}
