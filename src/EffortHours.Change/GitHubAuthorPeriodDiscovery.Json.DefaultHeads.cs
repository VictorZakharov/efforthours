using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    internal static async Task<string?> ResolveHistoricalDefaultHeadAsync(
        IExternalCommandRunner commands, string workingDirectory, GitHubDiscoveryRepository repository,
        ProviderQueryCounters counters, CancellationToken token)
    {
        string json = await RunRequiredApiAsync(commands, workingDirectory,
            ["api", $"repos/{repository.Identity}/commits?sha={Uri.EscapeDataString(repository.DefaultBranch!)}&per_page=1"],
            counters, paginated: false, token, emptyRepositoryIsEmpty: true).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().Any()) return null;
            // The production request is one page. Nested pages are accepted for adapter compatibility.
            JsonElement[] values = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 &&
                root[0].ValueKind == JsonValueKind.Array ? [.. Pages(root)] : [.. root.EnumerateArray()];
            if (values.Length == 0) return null;
            counters.ObserveIdentity(ProviderAuthorLogin(values[0]), ParseCommit(values[0]));
            return RequireObjectId(values[0].GetProperty("sha").GetString(), "default-branch head");
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw GitHubProviderFailure.Malformed(GitHubProviderFailure.DefaultHeadPhase, exception);
        }
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
        CancellationToken cancellationToken,
        bool fullHistory = false)
    {
        string endpoint = $"repos/{repositoryIdentity}/commits?sha={Uri.EscapeDataString(branch)}" +
            (fullHistory ? string.Empty :
                $"&since={Uri.EscapeDataString(since.ToString("O", CultureInfo.InvariantCulture))}" +
                $"&until={Uri.EscapeDataString(until.ToString("O", CultureInfo.InvariantCulture))}") + "&per_page=100";
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
            bool selected = false;
            foreach (JsonElement value in commits)
            {
                GitCommitMetadata commit = ParseCommit(value);
                counters.ObserveIdentity(ProviderAuthorLogin(value), commit);
                selected |= AuthorPeriodCommitSelector.Select([commit], options, aliases).Commits.Count > 0 ||
                    ProviderLoginMatches(value, aliases) &&
                    SelectedTimestamp(commit, dateField) >= since &&
                    SelectedTimestamp(commit, dateField) < until &&
                    (commit.ParentObjectIds.Count <= 1 || mergePolicy == ChangePortfolioMergePolicy.FirstParent);
            }
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

}
