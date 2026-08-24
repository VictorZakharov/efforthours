using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private const int MaximumGraphQlRepositoryBatch = 12;

    public static async Task<IReadOnlyList<DiscoveredRepository>?>
        DiscoverDefaultHeadsBatchedAsync(
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
        List<DiscoveredRepository> discovered = [];
        foreach (GitHubDiscoveryRepository[] batch in repositories.Chunk(MaximumGraphQlRepositoryBatch))
        {
            IReadOnlyList<DiscoveredRepository>? values =
                await ResolveDefaultHeadBatchAsync(
                    commands,
                    workingDirectory,
                    batch,
                    aliases,
                    since,
                    until,
                    dateField,
                    mergePolicy,
                    coauthorPolicy,
                    counters,
                    cancellationToken).ConfigureAwait(false);
            if (values is null)
            {
                return null;
            }

            discovered.AddRange(values);
        }

        return [.. discovered.OrderBy(value => value.RepositoryId, StringComparer.Ordinal)];
    }

    private static async Task<IReadOnlyList<DiscoveredRepository>?> ResolveDefaultHeadBatchAsync(
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
            "api",
            "graphql",
            "-f",
            "query=" + DefaultHeadBatchQuery(repositories.Length),
            "-F",
            "since=" + since.ToString("O", CultureInfo.InvariantCulture),
            "-F",
            "until=" + until.ToString("O", CultureInfo.InvariantCulture),
        ];
        for (int index = 0; index < repositories.Length; index++)
        {
            string[] identity = repositories[index].Identity.Split('/');
            arguments.Add("-F");
            arguments.Add($"owner{index}={identity[0]}");
            arguments.Add("-F");
            arguments.Add($"name{index}={identity[1]}");
        }

        string? json = await RunApiAsync(
            commands,
            workingDirectory,
            arguments,
            counters,
            paginated: false,
            optional: false,
            cancellationToken,
            capabilityFallback: true,
            failurePhase: GitHubProviderFailure.DefaultHeadPhase).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("errors", out JsonElement errors) &&
                errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                return null;
            }

            JsonElement data = root.GetProperty("data");
            List<DiscoveredRepository> discovered = [];
            for (int index = 0; index < repositories.Length; index++)
            {
                GitHubDiscoveryRepository repository = repositories[index];
                JsonElement providerRepository = data.GetProperty($"r{index}");
                if (providerRepository.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                JsonElement branch = providerRepository.GetProperty("defaultBranchRef");
                if (branch.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (!string.Equals(
                    branch.GetProperty("name").GetString(),
                    repository.DefaultBranch,
                    StringComparison.Ordinal))
                {
                    return null;
                }

                JsonElement target = branch.GetProperty("target");
                JsonElement history = target.GetProperty("history");
                if (history.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean())
                {
                    return null;
                }

                JsonElement[] commits = [.. history.GetProperty("nodes").EnumerateArray()];
                if (commits.Length == 0 || !GraphCommitsContainMatch(
                    commits,
                    aliases,
                    since,
                    until,
                    dateField,
                    mergePolicy,
                    coauthorPolicy))
                {
                    continue;
                }

                string objectId = RequireObjectId(
                    commits[0].GetProperty("oid").GetString(),
                    "default-branch head");
                discovered.Add(new DiscoveredRepository(
                    OpaqueId("repository", repository.StableId),
                    repository.Identity,
                    [new DiscoveredHead(
                        "default",
                        objectId,
                        $"refs/heads/{repository.DefaultBranch}")],
                    0));
            }

            return discovered;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or
                InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static bool GraphCommitsContainMatch(
        IReadOnlyList<JsonElement> commits,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy)
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
        foreach (JsonElement value in commits)
        {
            GitCommitMetadata commit = ParseGraphCommit(value);
            if (AuthorPeriodCommitSelector.Select([commit], options, aliases).Commits.Count > 0 ||
                GraphLoginMatches(value, aliases) &&
                SelectedTimestamp(commit, dateField) >= since &&
                SelectedTimestamp(commit, dateField) < until &&
                (commit.ParentObjectIds.Count <= 1 ||
                    mergePolicy == ChangePortfolioMergePolicy.FirstParent))
            {
                return true;
            }
        }

        return false;
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

    private static bool GraphLoginMatches(JsonElement commit, IReadOnlyList<string> aliases)
    {
        JsonElement author = commit.GetProperty("author");
        return author.TryGetProperty("user", out JsonElement user) &&
            user.ValueKind == JsonValueKind.Object &&
            user.TryGetProperty("login", out JsonElement login) &&
            aliases.Contains(login.GetString(), StringComparer.OrdinalIgnoreCase);
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
