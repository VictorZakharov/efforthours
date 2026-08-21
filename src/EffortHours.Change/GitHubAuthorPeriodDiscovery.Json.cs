using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record GitHubDiscoveryRepository(
    string StableId,
    string Identity,
    string? DefaultBranch);

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
                repositories.Add(new GitHubDiscoveryRepository(
                    stableId,
                    identity,
                    string.IsNullOrWhiteSpace(defaultBranch) ? null : defaultBranch));
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

    public static async Task<DiscoveredRepository> DiscoverHeadsAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        MappedRepository repository,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        bool includeOpenPullRequests,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string identity = repository.Provider.Identity;
        string branch = repository.Provider.DefaultBranch!;
        string defaultJson = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            ["api", $"repos/{identity}/commits/{Uri.EscapeDataString(branch)}"],
            counters,
            paginated: false,
            cancellationToken).ConfigureAwait(false);
        string defaultObject = ParseObject(defaultJson, "sha", "default-branch head");
        List<DiscoveredHead> heads =
        [
            new DiscoveredHead("default", defaultObject, $"refs/heads/{branch}"),
        ];
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
                        OpaqueId("open", repository.Provider.StableId + ":" + number),
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
                $"Repository '{OpaqueId("repository", repository.Provider.StableId)}' has {heads.Count} " +
                $"discovered heads; v1 supports at most " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository} per repository.");
        }

        return new DiscoveredRepository(
            OpaqueId("repository", repository.Provider.StableId),
            repository.LocalPath,
            "https://github.com/" + identity + ".git",
            heads);
    }

    private static async Task<string?> RunApiAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        ProviderQueryCounters counters,
        bool paginated,
        bool optional,
        CancellationToken cancellationToken)
    {
        counters.AddQuery();
        ExternalCommandResult result;
        try
        {
            result = await commands.RunAsync(
                "gh",
                workingDirectory,
                arguments,
                cancellationToken,
                requireSuccess: !optional).ConfigureAwait(false);
        }
        catch (ExternalCommandException exception)
        {
            throw new InvalidOperationException(
                "GitHub discovery failed. Confirm that gh is installed, authenticated, and authorized " +
                "for the requested owner scope.",
                exception);
        }

        if (optional && result.ExitCode != 0)
        {
            return null;
        }

        if (result.StandardOutput.Length > MaximumResponseCharacters)
        {
            throw new InvalidOperationException(
                "GitHub discovery response exceeded the bounded adapter input size.");
        }

        if (paginated)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException();
                }

                counters.AddPages(document.RootElement.GetArrayLength());
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "GitHub returned malformed paginated JSON.",
                    exception);
            }
        }
        else
        {
            counters.AddPages(1);
        }

        return result.StandardOutput;
    }

    private static async Task<string> RunRequiredApiAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        ProviderQueryCounters counters,
        bool paginated,
        CancellationToken cancellationToken) =>
        await RunApiAsync(
            commands,
            workingDirectory,
            arguments,
            counters,
            paginated,
            optional: false,
            cancellationToken).ConfigureAwait(false) ??
        throw new InvalidOperationException("GitHub discovery returned no response.");

    private static IEnumerable<JsonElement> Pages(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Paginated GitHub output must be an array of pages.");
        }

        foreach (JsonElement page in root.EnumerateArray())
        {
            if (page.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Each paginated GitHub page must be an array.");
            }

            foreach (JsonElement item in page.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static string ParseObject(string json, string property, string subject)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return RequireObjectId(document.RootElement.GetProperty(property).GetString(), subject);
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException($"GitHub returned an incomplete {subject}.", exception);
        }
    }

    private static string RequireObjectId(string? value, string subject)
    {
        string objectId = value?.ToLowerInvariant() ?? string.Empty;
        if (objectId.Length is not (40 or 64) || objectId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException($"GitHub returned an invalid {subject} object ID.");
        }

        return objectId;
    }

    private static string RequireRepositoryIdentity(string? value)
    {
        string identity = value?.Trim() ?? string.Empty;
        string[] parts = identity.Split('/');
        if (parts.Length != 2 || parts.Any(part => string.IsNullOrWhiteSpace(part)) ||
            parts.Any(part => part.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            throw new InvalidOperationException("GitHub returned an invalid repository identity.");
        }

        return identity;
    }

    private static string OpaqueId(string prefix, string value)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
        return prefix + "-" + digest[..20];
    }
}
