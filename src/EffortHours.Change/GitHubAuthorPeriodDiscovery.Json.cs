using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record GitHubDiscoveryRepository(
    string StableId,
    string Identity,
    string? DefaultBranch,
    bool Archived = false,
    bool Mirror = false);

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private const int MaximumResponseCharacters = 16 * 1024 * 1024;

    public static async Task<string> ResolveOwnerTypeAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        string owner,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string json = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            ["api", $"users/{Uri.EscapeDataString(owner)}"],
            counters,
            paginated: false,
            cancellationToken).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            string type = document.RootElement.GetProperty("type").GetString() ?? string.Empty;
            return type.Equals("Organization", StringComparison.OrdinalIgnoreCase)
                ? "organization"
                : "user";
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "GitHub returned an incomplete owner identity.",
                exception);
        }
    }

    public static async Task<IReadOnlyList<GitHubDiscoveryRepository>> ListRepositoriesAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        string owner,
        string ownerType,
        string authenticatedLogin,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string escaped = Uri.EscapeDataString(owner);
        string endpoint = ownerType == "organization"
            ? $"orgs/{escaped}/repos?per_page=100&type=all"
            : owner.Equals(authenticatedLogin, StringComparison.OrdinalIgnoreCase)
                ? "user/repos?per_page=100&affiliation=owner"
                : $"users/{escaped}/repos?per_page=100&type=owner";
        string json = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            ["api", "--paginate", "--slurp", endpoint],
            counters,
            paginated: true,
            cancellationToken).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            List<GitHubDiscoveryRepository> repositories = [];
            foreach (JsonElement item in Pages(document.RootElement))
            {
                string stableId = item.GetProperty("id").ToString();
                string identity = RequireRepositoryIdentity(
                    item.GetProperty("full_name").GetString());
                string? defaultBranch = item.TryGetProperty("default_branch", out JsonElement branch)
                    ? branch.GetString()
                    : null;
                bool archived = item.TryGetProperty("archived", out JsonElement archivedValue) &&
                    archivedValue.ValueKind == JsonValueKind.True;
                bool mirror = item.TryGetProperty("mirror_url", out JsonElement mirrorValue) &&
                    mirrorValue.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(mirrorValue.GetString());
                repositories.Add(new GitHubDiscoveryRepository(
                    stableId,
                    identity,
                    string.IsNullOrWhiteSpace(defaultBranch) ? null : defaultBranch,
                    archived,
                    mirror));
            }

            GitHubDiscoveryRepository[] canonical = [.. repositories
                .DistinctBy(repository => repository.StableId, StringComparer.Ordinal)
                .OrderBy(repository => repository.StableId, StringComparer.Ordinal)];
            if (canonical.Length > 1_000)
            {
                throw new InvalidOperationException(
                    "GitHub owner discovery exceeded the bounded 1,000-repository adapter scope.");
            }

            return canonical;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "GitHub returned an incomplete paginated repository inventory.",
                exception);
        }
    }

    public static async Task<string> ResolveViewerAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string json = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            ["api", "user"],
            counters,
            paginated: false,
            cancellationToken).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            string login = document.RootElement.GetProperty("login").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(login))
            {
                throw new InvalidOperationException("The active GitHub login is empty.");
            }

            return login;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "GitHub could not resolve the active authenticated account.",
                exception);
        }
    }

    public static async Task<IReadOnlyList<string>> ResolveVerifiedEmailsAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string? json = await RunApiAsync(
            commands,
            workingDirectory,
            ["api", "--paginate", "--slurp", "user/emails?per_page=100"],
            counters,
            paginated: true,
            optional: true,
            cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return [.. Pages(document.RootElement)
                .Where(item => item.TryGetProperty("verified", out JsonElement verified) &&
                    verified.ValueKind == JsonValueKind.True)
                .Select(item => item.GetProperty("email").GetString())
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "GitHub returned malformed verified-email metadata.",
                exception);
        }
    }

    public static async Task<DiscoveredRepository?> DiscoverHeadsAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        GitHubDiscoveryRepository repository,
        IReadOnlyList<string> aliases,
        string authenticatedLogin,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        bool includeOpenPullRequests,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string identity = repository.Identity;
        string branch = repository.DefaultBranch!;
        List<DiscoveredHead> heads = [];
        string? defaultObject = await ResolveMatchingDefaultHeadAsync(
            commands,
            workingDirectory,
            identity,
            branch,
            aliases,
            since,
            until,
            dateField,
            mergePolicy,
            coauthorPolicy,
            counters,
            cancellationToken).ConfigureAwait(false);
        if (defaultObject is not null)
        {
            heads.Add(new DiscoveredHead("default", defaultObject, $"refs/heads/{branch}"));
        }

        int authoredOpenPullRequests = 0;
        if (includeOpenPullRequests)
        {
            string pullsJson = await RunRequiredApiAsync(
                commands,
                workingDirectory,
                ["api", "--paginate", "--slurp", $"repos/{identity}/pulls?state=open&per_page=100"],
                counters,
                paginated: true,
                cancellationToken).ConfigureAwait(false);
            try
            {
                using JsonDocument document = JsonDocument.Parse(pullsJson);
                foreach (JsonElement pull in Pages(document.RootElement)
                    .OrderBy(item => item.GetProperty("number").GetInt32()))
                {
                    if (!PullAuthorMatches(pull, authenticatedLogin, aliases))
                    {
                        continue;
                    }

                    authoredOpenPullRequests++;
                    int number = pull.GetProperty("number").GetInt32();
                    if (number <= 0)
                    {
                        throw new InvalidOperationException("GitHub returned an invalid pull-request number.");
                    }

                    int expectedCommitCount = await ResolvePullCommitCountAsync(
                        commands,
                        workingDirectory,
                        identity,
                        number,
                        counters,
                        cancellationToken).ConfigureAwait(false);
                    if (!await PullContainsMatchingCommitAsync(
                        commands,
                        workingDirectory,
                        identity,
                        number,
                        expectedCommitCount,
                        aliases,
                        since,
                        until,
                        dateField,
                        mergePolicy,
                        coauthorPolicy,
                        counters,
                        cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    string objectId = RequireObjectId(
                        pull.GetProperty("head").GetProperty("sha").GetString(),
                        "open pull-request head");
                    if (heads.Any(head => head.ObjectId == objectId))
                    {
                        continue;
                    }

                    heads.Add(new DiscoveredHead(
                        OpaqueId("open", repository.StableId + ":" + number),
                        objectId,
                        $"refs/pull/{number}/head"));
                }
            }
            catch (Exception exception) when (
                exception is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "GitHub returned an incomplete paginated open-pull-request inventory.",
                    exception);
            }
        }

        if (heads.Count > ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository)
        {
            throw new InvalidOperationException(
                $"Repository '{OpaqueId("repository", repository.StableId)}' has {heads.Count} " +
                $"discovered heads; v1 supports at most " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository} per repository.");
        }

        counters.AddOpenPullRequests(authoredOpenPullRequests);

        return heads.Count == 0
            ? null
            : new DiscoveredRepository(
                OpaqueId("repository", repository.StableId),
                repository.Identity,
                heads,
                authoredOpenPullRequests);
    }

    private static async Task<string?> ResolveMatchingDefaultHeadAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        string repositoryIdentity,
        string branch,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string endpoint = $"repos/{repositoryIdentity}/commits?sha={Uri.EscapeDataString(branch)}" +
            $"&since={Uri.EscapeDataString(since.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&until={Uri.EscapeDataString(until.ToString("O", CultureInfo.InvariantCulture))}&per_page=100";
        string json = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            ["api", "--paginate", "--slurp", endpoint],
            counters,
            paginated: true,
            cancellationToken,
            emptyRepositoryIsEmpty: true).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement[] commits = [.. Pages(document.RootElement)];
            if (commits.Length == 0)
            {
                return null;
            }

            GitAuthorPeriodPortfolioOptions options = new()
            {
                Aliases = aliases,
                SinceInclusive = since,
                UntilExclusive = until,
                DateField = dateField,
                MergePolicy = mergePolicy,
                CoauthorPolicy = coauthorPolicy,
            };
            bool selected = commits.Any(commit =>
                AuthorPeriodCommitSelector.Select([ParseCommit(commit)], options, aliases)
                    .Commits.Count > 0);
            return selected
                ? RequireObjectId(commits[0].GetProperty("sha").GetString(), "default-branch head")
                : null;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidOperationException(
                "GitHub returned incomplete default-branch commit metadata.",
                exception);
        }
    }

    private static bool PullAuthorMatches(
        JsonElement pull,
        string authenticatedLogin,
        IReadOnlyList<string> aliases) =>
        pull.TryGetProperty("user", out JsonElement user) &&
        user.ValueKind == JsonValueKind.Object &&
        user.TryGetProperty("login", out JsonElement login) &&
        (string.Equals(login.GetString(), authenticatedLogin, StringComparison.OrdinalIgnoreCase) ||
            aliases.Contains(login.GetString(), StringComparer.OrdinalIgnoreCase));

}
