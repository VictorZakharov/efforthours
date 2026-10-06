using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static readonly SemaphoreSlim HistoricalPullGate = new(4, 4);

    private static async Task<IReadOnlyList<DiscoveredRepository>> DiscoverHistoricalPullsBatchedAsync(
        IExternalCommandRunner commands, string directory, IReadOnlyList<GitHubDiscoveryRepository> repositories,
        AccountPullRequest[] pulls, IReadOnlyList<string> aliases, DateTimeOffset since, DateTimeOffset until,
        ChangePortfolioDateField dateField, ChangePortfolioMergePolicy mergePolicy, ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters, CancellationToken token)
    {
        ConcurrentBag<ResolvedPullHead> selected = [];
        List<AccountPullRequest> misses = [];
        counters.PlanHistoricalPulls(pulls.Length);
        foreach (AccountPullRequest pull in pulls)
        {
            token.ThrowIfCancellationRequested();
            GitHubPullMetadata? cached = pull is { ObjectId: not null, BaseObjectId: not null, CommitCount: not null } && counters.PullMetadataCache is { } cache
                ? await cache.ReadAsync(pull.RepositoryIdentity, pull.Number, pull.ObjectId, pull.BaseObjectId, pull.CommitCount.Value, token).ConfigureAwait(false) : null;
            if (cached is null) misses.Add(pull);
            else { counters.HistoricalPullCacheHit(); Admit(pull, cached); }
        }
        using CancellationTokenSource children = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = children.Token;
        Exception? rootFailure = null;
        Task[] tasks = [.. misses.GroupBy(pull => pull.RepositoryIdentity, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Chunk(12)).Select(async batch =>
            {
                await HistoricalPullGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    counters.HistoricalPullBatch();
                    List<string> arguments = ["api", "graphql", "-f", "query=" + HistoricalPullBatchQuery(batch.Length)];
                    for (int index = 0; index < batch.Length; index++)
                    {
                        string[] identity = batch[index].RepositoryIdentity.Split('/');
                        arguments.AddRange(["-F", $"owner{index}={identity[0]}", "-F", $"name{index}={identity[1]}", "-F", $"number{index}={batch[index].Number}"]);
                    }
                    string? json = await RunApiAsync(commands, directory, arguments, counters, false, false, token,
                        capabilityFallback: true, failurePhase: GitHubProviderFailure.OpenPullRequestPhase).ConfigureAwait(false);
                    using JsonDocument? document = ParseHistoricalPullBatchDocument(json);
                    for (int index = 0; index < batch.Length; index++)
                    {
                        token.ThrowIfCancellationRequested();
                        AccountPullRequest pull = batch[index];
                        GitHubPullMetadata? metadata = ParseHistoricalPullBatch(document?.RootElement, index, pull);
                        if (metadata is null)
                        {
                            counters.HistoricalPullFallback();
                            metadata = await ResolveHistoricalPullRestAsync(commands, directory, pull, counters, token).ConfigureAwait(false);
                        }
                        if (counters.PullMetadataCache is { } cache)
                            await cache.WriteAsync(pull.RepositoryIdentity, pull.Number, metadata, token).ConfigureAwait(false);
                        Admit(pull, metadata);
                    }
                }
                catch (Exception exception)
                {
                    if (exception is not OperationCanceledException) Interlocked.CompareExchange(ref rootFailure, exception, null);
                    await children.CancelAsync().ConfigureAwait(false);
                    throw;
                }
                finally { HistoricalPullGate.Release(); }
            })];
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch when (rootFailure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(rootFailure).Throw(); throw; }
        return [.. selected.GroupBy(pull => pull.RepositoryIdentity, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            GitHubDiscoveryRepository repository = repositories.Single(value => value.Identity.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
            DiscoveredHead[] heads = [.. group.OrderBy(pull => pull.Number).Select(pull => new DiscoveredHead(
                OpaqueId("open", repository.StableId + ":" + pull.Number), pull.ObjectId, $"refs/pull/{pull.Number}/head", pull.Open))
                .DistinctBy(head => head.ObjectId, StringComparer.Ordinal)];
            if (heads.Length > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository)
                throw new InvalidOperationException("Historical PR discovery exceeded the per-repository head bound.");
            return new DiscoveredRepository(OpaqueId("repository", repository.StableId), group.Key, heads,
                pulls.Count(pull => pull.RepositoryIdentity.Equals(group.Key, StringComparison.OrdinalIgnoreCase)));
        }).OrderBy(repository => repository.RepositoryId, StringComparer.Ordinal)];

        void Admit(AccountPullRequest pull, GitHubPullMetadata metadata)
        {
            GitAuthorPeriodPortfolioOptions options = new()
            {
                Aliases = aliases,
                SinceInclusive = since,
                UntilExclusive = until,
                DateField = dateField,
                MergePolicy = mergePolicy,
                CoauthorPolicy = coauthorPolicy
            };
            bool matching = false;
            foreach (GitHubPullCommitEvidence evidence in metadata.Commits)
            {
                GitCommitMetadata commit = evidence.Commit;
                counters.ObserveIdentity(evidence.Login, commit);
                matching |= AuthorPeriodCommitSelector.Select([commit], options, aliases).Commits.Count > 0 ||
                    aliases.Contains(evidence.Login, StringComparer.OrdinalIgnoreCase) && SelectedTimestamp(commit, dateField) >= since &&
                    SelectedTimestamp(commit, dateField) < until && (commit.ParentObjectIds.Count <= 1 || mergePolicy == ChangePortfolioMergePolicy.FirstParent);
            }
            counters.CompleteHistoricalPull(matching);
            if (matching) selected.Add(new(pull.RepositoryIdentity, pull.Number, metadata.Head, pull.Open));
        }
    }

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
                throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, new InvalidOperationException("Retained PR changed during discovery."));
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
        string detail = await RunRequiredApiAsync(commands, directory, ["api", $"repos/{pull.RepositoryIdentity}/pulls/{pull.Number}"], counters, false, token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(detail);
        JsonElement root = document.RootElement;
        string head = RequireObjectId(root.GetProperty("head").GetProperty("sha").GetString(), "retained PR head");
        string baseHead = root.TryGetProperty("base", out JsonElement baseValue) ? RequireObjectId(baseValue.GetProperty("sha").GetString(), "retained PR base") : string.Empty;
        int count = root.GetProperty("commits").GetInt32();
        if (count is <= 0 or > 250 || pull.ObjectId is not null && pull.ObjectId != head ||
            pull.BaseObjectId is not null && pull.BaseObjectId != baseHead || pull.CommitCount is not null && pull.CommitCount != count)
            throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, new InvalidOperationException("Incomplete or changed retained PR metadata."));
        string json = await RunRequiredApiAsync(commands, directory, ["api", "--paginate", "--slurp",
            $"repos/{pull.RepositoryIdentity}/pulls/{pull.Number}/commits?per_page=100"], counters, true, token).ConfigureAwait(false);
        using JsonDocument commitsDocument = JsonDocument.Parse(json);
        GitHubPullCommitEvidence[] commits = [.. Pages(commitsDocument.RootElement).Select(value => new GitHubPullCommitEvidence(ParseCommit(value), ProviderAuthorLogin(value)))];
        if (commits.Length != count || commits[^1].Commit.ObjectId != head || commits.Select(value => value.Commit.ObjectId).Distinct().Count() != count)
            throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, new InvalidOperationException("Incomplete retained PR commits."));
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
