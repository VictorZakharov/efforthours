using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    public static async Task<IReadOnlyList<GitHubActiveContributor>>
        ListActiveContributorsAsync(
            IExternalCommandRunner commands,
            string workingDirectory,
            GitHubDiscoveryRepository repository,
            DateTimeOffset since,
            DateTimeOffset until,
            ChangePortfolioDateField dateField,
            ChangePortfolioMergePolicy mergePolicy,
            ProviderQueryCounters counters,
            CancellationToken cancellationToken)
    {
        string endpoint =
            $"repos/{repository.Identity}/commits?sha={Uri.EscapeDataString(repository.DefaultBranch!)}" +
            $"&since={Uri.EscapeDataString(since.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&until={Uri.EscapeDataString(until.ToString("O", CultureInfo.InvariantCulture))}" +
            "&per_page=100";
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
            Dictionary<string, HashSet<string>> aliases =
                new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement value in Pages(document.RootElement))
            {
                GitCommitMetadata commit = ParseCommit(value);
                DateTimeOffset selected = SelectedTimestamp(commit, dateField);
                if (selected < since || selected >= until ||
                    commit.ParentObjectIds.Count > 1 &&
                    mergePolicy == ChangePortfolioMergePolicy.Exclude ||
                    !TryHumanLogin(value, out string login))
                {
                    continue;
                }

                if (!aliases.TryGetValue(login, out HashSet<string>? values))
                {
                    values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    aliases.Add(login, values);
                }

                values.Add(login);
                AddAlias(values, commit.Author.Name);
                AddAlias(values, commit.Author.Email);
            }

            return [.. aliases
                .Select(pair => new GitHubActiveContributor(
                    pair.Key,
                    [.. pair.Value
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ThenBy(value => value, StringComparer.Ordinal)]))
                .Where(contributor => contributor.Aliases.Count <=
                    ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
                .OrderBy(contributor => contributor.Login, StringComparer.OrdinalIgnoreCase)
                .ThenBy(contributor => contributor.Login, StringComparer.Ordinal)];
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or
                InvalidOperationException or FormatException)
        {
            throw new InvalidOperationException(
                "GitHub returned incomplete active-contributor commit metadata.",
                exception);
        }
    }

    private static bool TryHumanLogin(JsonElement commit, out string login)
    {
        login = string.Empty;
        if (!commit.TryGetProperty("author", out JsonElement author) ||
            author.ValueKind != JsonValueKind.Object ||
            !author.TryGetProperty("login", out JsonElement loginValue) ||
            string.IsNullOrWhiteSpace(loginValue.GetString()))
        {
            return false;
        }

        login = loginValue.GetString()!.Trim();
        string type = author.TryGetProperty("type", out JsonElement typeValue)
            ? typeValue.GetString() ?? string.Empty
            : string.Empty;
        return !type.Equals("Bot", StringComparison.OrdinalIgnoreCase) &&
            !LooksLikeServiceAccount(login);
    }

    private static bool LooksLikeServiceAccount(string login) =>
        login.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase) ||
        login.EndsWith("-bot", StringComparison.OrdinalIgnoreCase) ||
        login.EndsWith("_bot", StringComparison.OrdinalIgnoreCase) ||
        login.EndsWith(".bot", StringComparison.OrdinalIgnoreCase) ||
        login.StartsWith("bot-", StringComparison.OrdinalIgnoreCase);

    private static void AddAlias(HashSet<string> aliases, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            aliases.Add(value.Trim());
        }
    }
}
