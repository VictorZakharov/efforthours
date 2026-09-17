using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
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
