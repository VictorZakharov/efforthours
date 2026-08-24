using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    public static async Task<IReadOnlyList<DiscoveredRepository>?>
        DiscoverViewerOpenPullHeadsAccountWideAsync(
            IExternalCommandRunner commands,
            string workingDirectory,
            IReadOnlyList<GitHubDiscoveryRepository> repositories,
            string authenticatedLogin,
            IReadOnlyList<string> aliases,
            DateTimeOffset since,
            DateTimeOffset until,
            ChangePortfolioDateField dateField,
            ChangePortfolioMergePolicy mergePolicy,
            ChangePortfolioCoauthorPolicy coauthorPolicy,
            ProviderQueryCounters counters,
            CancellationToken cancellationToken)
    {
        string? json = await RunApiAsync(
            commands,
            workingDirectory,
            [
                "api",
                "--paginate",
                "--slurp",
                "-f",
                "query=" + ViewerPullRequestsQuery,
                "-F",
                "login=" + authenticatedLogin,
            ],
            counters,
            paginated: true,
            optional: false,
            cancellationToken,
            capabilityFallback: true,
            failurePhase: GitHubProviderFailure.OpenPullRequestPhase).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        AccountPullRequest[]? pulls = ParseCompleteAccountPulls(json, authenticatedLogin);
        if (pulls is null)
        {
            return null;
        }

        Dictionary<string, GitHubDiscoveryRepository> byIdentity = repositories.ToDictionary(
            repository => repository.Identity,
            StringComparer.OrdinalIgnoreCase);
        AccountPullRequest[] considered = [.. pulls
            .Where(pull => byIdentity.ContainsKey(pull.RepositoryIdentity))
            .OrderBy(pull => pull.RepositoryIdentity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pull => pull.Number)];
        counters.AddOpenPullRequests(considered.Length);
        using SemaphoreSlim gate = new(4, 4);
        Task<ResolvedPullHead?>[] tasks = [.. considered.Select(async pull =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                PullDetail detail = await ResolvePullDetailAsync(
                    commands,
                    workingDirectory,
                    pull.RepositoryIdentity,
                    pull.Number,
                    counters,
                    cancellationToken).ConfigureAwait(false);
                bool selected = await PullContainsMatchingCommitAsync(
                    commands,
                    workingDirectory,
                    pull.RepositoryIdentity,
                    pull.Number,
                    detail.CommitCount,
                    aliases,
                    since,
                    until,
                    dateField,
                    mergePolicy,
                    coauthorPolicy,
                    counters,
                    cancellationToken).ConfigureAwait(false);
                return selected
                    ? new ResolvedPullHead(
                        pull.RepositoryIdentity,
                        pull.Number,
                        detail.ObjectId)
                    : null;
            }
            finally
            {
                gate.Release();
            }
        })];
        ResolvedPullHead[] selected = [.. (await Task.WhenAll(tasks).ConfigureAwait(false))
            .Where(value => value is not null)
            .Select(value => value!)];
        List<DiscoveredRepository> discovered = [];
        foreach (IGrouping<string, ResolvedPullHead> group in selected
            .GroupBy(value => value.RepositoryIdentity, StringComparer.OrdinalIgnoreCase))
        {
            GitHubDiscoveryRepository repository = byIdentity[group.Key];
            DiscoveredHead[] heads = [.. group
                .OrderBy(value => value.Number)
                .Select(value => new DiscoveredHead(
                    OpaqueId("open", repository.StableId + ":" + value.Number),
                    value.ObjectId,
                    $"refs/pull/{value.Number}/head"))
                .DistinctBy(value => value.ObjectId, StringComparer.Ordinal)];
            if (heads.Length > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository)
            {
                throw new InvalidOperationException(
                    "Account-level open-pull-request discovery exceeded the per-repository head bound.");
            }

            discovered.Add(new DiscoveredRepository(
                OpaqueId("repository", repository.StableId),
                repository.Identity,
                heads,
                considered.Count(value =>
                    value.RepositoryIdentity.Equals(group.Key, StringComparison.OrdinalIgnoreCase))));
        }

        return [.. discovered.OrderBy(value => value.RepositoryId, StringComparer.Ordinal)];
    }

    private static AccountPullRequest[]? ParseCompleteAccountPulls(
        string json,
        string authenticatedLogin)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return null;
            }

            int? total = null;
            int observed = 0;
            List<AccountPullRequest> pulls = [];
            foreach (JsonElement page in root.EnumerateArray())
            {
                JsonElement connection = page.GetProperty("data").GetProperty("user")
                    .GetProperty("pullRequests");
                int pageTotal = connection.GetProperty("totalCount").GetInt32();
                total ??= pageTotal;
                if (pageTotal != total || pageTotal < 0)
                {
                    return null;
                }

                foreach (JsonElement item in connection.GetProperty("nodes").EnumerateArray())
                {
                    observed++;
                    JsonElement author = item.GetProperty("author");
                    if (author.ValueKind == JsonValueKind.Null ||
                        !string.Equals(
                            author.GetProperty("login").GetString(),
                            authenticatedLogin,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int number = item.GetProperty("number").GetInt32();
                    string identity = RequireRepositoryIdentity(
                        item.GetProperty("repository").GetProperty("nameWithOwner").GetString());
                    if (number <= 0)
                    {
                        return null;
                    }

                    pulls.Add(new AccountPullRequest(identity, number));
                }
            }

            return total == observed && total <= 1_000
                ? [.. pulls.Distinct()]
                : null;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<PullDetail> ResolvePullDetailAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        string repositoryIdentity,
        int pullNumber,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string json = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            ["api", $"repos/{repositoryIdentity}/pulls/{pullNumber}"],
            counters,
            paginated: false,
            cancellationToken).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            int count = document.RootElement.GetProperty("commits").GetInt32();
            string objectId = RequireObjectId(
                document.RootElement.GetProperty("head").GetProperty("sha").GetString(),
                "open pull-request head");
            return count >= 0
                ? new PullDetail(count, objectId)
                : throw new InvalidOperationException();
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw GitHubProviderFailure.Malformed(
                GitHubProviderFailure.OpenPullRequestPhase,
                exception);
        }
    }

    private const string ViewerPullRequestsQuery =
        "query($endCursor:String,$login:String!){user(login:$login)" +
        "{pullRequests(first:100,after:$endCursor,states:OPEN)" +
        "{totalCount nodes{number author{login} repository{nameWithOwner}}" +
        "pageInfo{hasNextPage endCursor}}}}";

    private sealed record AccountPullRequest(string RepositoryIdentity, int Number);

    private sealed record ResolvedPullHead(
        string RepositoryIdentity,
        int Number,
        string ObjectId);

    private sealed record PullDetail(int CommitCount, string ObjectId);
}
