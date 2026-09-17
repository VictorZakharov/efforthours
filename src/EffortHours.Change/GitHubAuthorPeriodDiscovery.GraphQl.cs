using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private const int MaximumGraphQlRepositoryBatch = 12;

    public static async Task<DefaultHeadBatchResult> DiscoverDefaultHeadsBatchedAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        IReadOnlyList<GitHubDiscoveryRepository> repositories,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        using SemaphoreSlim gate = new(4, 4);
        Task<DefaultHeadBatchResult>[] tasks = [.. repositories
            .Chunk(MaximumGraphQlRepositoryBatch).Select(async batch =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await ResolveDefaultHeadBatchAsync(
                        commands, workingDirectory, batch, aliases, since, until,
                        dateField, mergePolicy, coauthorPolicy, counters, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            })];
        DefaultHeadBatchResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return new DefaultHeadBatchResult(
            [.. results.SelectMany(result => result.Repositories)
                .OrderBy(value => value.RepositoryId, StringComparer.Ordinal)],
            [.. results.SelectMany(result => result.FallbackRepositories)]);
    }

    private static async Task<DefaultHeadBatchResult> ResolveDefaultHeadBatchAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        GitHubDiscoveryRepository[] repositories,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "api", "graphql", "-f", "query=" + DefaultHeadBatchQuery(repositories.Length),
            "-F", "since=" + since.ToString("O", CultureInfo.InvariantCulture),
            "-F", "until=" + until.ToString("O", CultureInfo.InvariantCulture),
        ];
        for (int index = 0; index < repositories.Length; index++)
        {
            string[] identity = repositories[index].Identity.Split('/');
            arguments.Add("-F");
            arguments.Add($"owner{index}={identity[0]}");
            arguments.Add("-F");
            arguments.Add($"name{index}={identity[1]}");
        }

        counters.AddDefaultBatch();
        string? json = await RunApiAsync(
            commands, workingDirectory, arguments, counters, paginated: false, optional: false,
            cancellationToken, capabilityFallback: true,
            failurePhase: GitHubProviderFailure.DefaultHeadPhase).ConfigureAwait(false);
        if (json is null)
        {
            return FallbackBatch("provider-unavailable");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("errors", out JsonElement errors) &&
                errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                return FallbackBatch("provider-errors");
            }

            JsonElement data = root.GetProperty("data");
            List<DiscoveredRepository> discovered = [];
            List<GitHubDiscoveryRepository> fallback = [];
            for (int index = 0; index < repositories.Length; index++)
            {
                GitHubDiscoveryRepository repository = repositories[index];
                string? reason = ParseDefaultRepository(
                    data, index, repository, aliases, since, until, dateField, mergePolicy,
                    coauthorPolicy, counters, out DiscoveredRepository? selected);
                if (reason is not null)
                {
                    fallback.Add(repository);
                    counters.AddFallback("default-head", reason, 1);
                }
                else if (selected is not null)
                {
                    discovered.Add(selected);
                }
            }

            return new DefaultHeadBatchResult(discovered, fallback);
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return FallbackBatch("malformed-response");
        }

        DefaultHeadBatchResult FallbackBatch(string reason)
        {
            counters.AddFallback("default-head", reason, repositories.Length);
            return new DefaultHeadBatchResult([], repositories);
        }
    }

    private static string? ParseDefaultRepository(
        JsonElement data,
        int index,
        GitHubDiscoveryRepository repository,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters,
        out DiscoveredRepository? selected)
    {
        selected = null;
        try
        {
            JsonElement providerRepository = data.GetProperty($"r{index}");
            if (providerRepository.ValueKind == JsonValueKind.Null)
            {
                return "repository-unavailable";
            }

            JsonElement branch = providerRepository.GetProperty("defaultBranchRef");
            if (branch.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (!string.Equals(branch.GetProperty("name").GetString(),
                repository.DefaultBranch, StringComparison.Ordinal))
            {
                return "branch-changed";
            }

            JsonElement history = branch.GetProperty("target").GetProperty("history");
            if (history.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean())
            {
                return "incomplete-history";
            }

            JsonElement[] commits = [.. history.GetProperty("nodes").EnumerateArray()];
            if (commits.Length > 0 && GraphCommitsContainMatch(
                commits, aliases, since, until, dateField, mergePolicy, coauthorPolicy, counters))
            {
                selected = new DiscoveredRepository(
                    OpaqueId("repository", repository.StableId),
                    repository.Identity,
                    [new DiscoveredHead("default",
                        RequireObjectId(commits[0].GetProperty("oid").GetString(), "default-branch head"),
                        $"refs/heads/{repository.DefaultBranch}")],
                    0);
            }

            return null;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return "malformed-response";
        }
    }

    private static bool GraphCommitsContainMatch(
        IReadOnlyList<JsonElement> commits,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters)
    {
        GitAuthorPeriodPortfolioOptions options = new()
        {
            Aliases = aliases,
            SinceInclusive = since,
            UntilExclusive = until,
            DateField = dateField,
            MergePolicy = mergePolicy,
            CoauthorPolicy = coauthorPolicy,
        };
        bool selected = false;
        foreach (JsonElement value in commits)
        {
            GitCommitMetadata commit = ParseGraphCommit(value);
            counters.ObserveIdentity(GraphAuthorLogin(value), commit);
            if (AuthorPeriodCommitSelector.Select([commit], options, aliases).Commits.Count > 0 ||
                GraphLoginMatches(value, aliases) &&
                SelectedTimestamp(commit, dateField) >= since &&
                SelectedTimestamp(commit, dateField) < until &&
                (commit.ParentObjectIds.Count <= 1 ||
                    mergePolicy == ChangePortfolioMergePolicy.FirstParent))
            {
                selected = true;
            }
        }

        return selected;
    }

    private static GitCommitMetadata ParseGraphCommit(JsonElement value)
    {
        JsonElement author = value.GetProperty("author");
        JsonElement committer = value.GetProperty("committer");
        return new GitCommitMetadata
        {
            ObjectId = RequireObjectId(value.GetProperty("oid").GetString(), "default-branch commit"),
            ParentObjectIds = [.. value.GetProperty("parents").GetProperty("nodes")
                .EnumerateArray()
                .Select(parent => RequireObjectId(
                    parent.GetProperty("oid").GetString(),
                    "default-branch parent"))],
            Author = ParseIdentity(author),
            AuthorTimestamp = ParseTimestamp(value.GetProperty("authoredDate").GetString()),
            Committer = ParseIdentity(committer),
            CommitterTimestamp = ParseTimestamp(value.GetProperty("committedDate").GetString()),
            Coauthors = GitCommitMetadataParser.ParseCoauthors(
                value.GetProperty("message").GetString() ?? string.Empty),
        };
    }

    private static bool GraphLoginMatches(JsonElement commit, IReadOnlyList<string> aliases) =>
        aliases.Contains(GraphAuthorLogin(commit), StringComparer.OrdinalIgnoreCase);

    private static string? GraphAuthorLogin(JsonElement commit)
    {
        JsonElement author = commit.GetProperty("author");
        return author.TryGetProperty("user", out JsonElement user) &&
            user.ValueKind == JsonValueKind.Object &&
            user.TryGetProperty("login", out JsonElement login) ? login.GetString() : null;
    }

    private static string DefaultHeadBatchQuery(int count)
    {
        StringBuilder query = new("query($since:GitTimestamp!,$until:GitTimestamp!");
        for (int index = 0; index < count; index++)
        {
            query.Append(",$owner").Append(index).Append(":String!,$name")
                .Append(index).Append(":String!");
        }

        query.Append("){");
        for (int index = 0; index < count; index++)
        {
            query.Append('r').Append(index).Append(":repository(owner:$owner")
                .Append(index).Append(",name:$name").Append(index)
                .Append("){defaultBranchRef{name target{...on Commit{history(first:100,since:$since,until:$until)")
                .Append("{nodes{oid parents(first:2){nodes{oid}} author{name email user{login}} authoredDate ")
                .Append("committer{name email user{login}} committedDate message}pageInfo{hasNextPage}}}}}}");
        }

        return query.Append('}').ToString();
    }
}
