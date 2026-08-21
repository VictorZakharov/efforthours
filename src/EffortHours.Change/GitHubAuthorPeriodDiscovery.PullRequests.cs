using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static async Task<int> ResolvePullCommitCountAsync(
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
            return count >= 0
                ? count
                : throw new InvalidOperationException(
                    "GitHub returned an invalid pull-request commit count.");
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "GitHub returned incomplete open-pull-request detail metadata.",
                exception);
        }
    }

    private static async Task<bool> PullContainsMatchingCommitAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        string repositoryIdentity,
        int pullNumber,
        int expectedCommitCount,
        IReadOnlyList<string> aliases,
        DateTimeOffset since,
        DateTimeOffset until,
        ChangePortfolioDateField dateField,
        ChangePortfolioMergePolicy mergePolicy,
        ChangePortfolioCoauthorPolicy coauthorPolicy,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        string json = await RunRequiredApiAsync(
            commands,
            workingDirectory,
            [
                "api",
                "--paginate",
                "--slurp",
                $"repos/{repositoryIdentity}/pulls/{pullNumber}/commits?per_page=100",
            ],
            counters,
            paginated: true,
            cancellationToken).ConfigureAwait(false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement[] commits = [.. Pages(document.RootElement)];
            if (expectedCommitCount < 0 || commits.Length != expectedCommitCount)
            {
                throw new InvalidOperationException(
                    "GitHub did not return the complete open-pull-request commit inventory.");
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
            foreach (JsonElement commit in commits)
            {
                GitCommitMetadata metadata = ParseCommit(commit);
                if (AuthorPeriodCommitSelector.Select([metadata], options, aliases).Commits.Count > 0 ||
                    ProviderLoginMatches(commit, aliases) &&
                    SelectedTimestamp(metadata, dateField) >= since &&
                    SelectedTimestamp(metadata, dateField) < until &&
                    (metadata.ParentObjectIds.Count <= 1 ||
                        mergePolicy == ChangePortfolioMergePolicy.FirstParent))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidOperationException(
                "GitHub returned incomplete commit metadata for an open pull request.",
                exception);
        }
    }

    private static GitCommitMetadata ParseCommit(JsonElement value)
    {
        JsonElement commit = value.GetProperty("commit");
        JsonElement author = commit.GetProperty("author");
        JsonElement committer = commit.GetProperty("committer");
        return new GitCommitMetadata
        {
            ObjectId = RequireObjectId(value.GetProperty("sha").GetString(), "pull-request commit"),
            ParentObjectIds = [.. value.GetProperty("parents").EnumerateArray().Select(parent =>
                RequireObjectId(parent.GetProperty("sha").GetString(), "pull-request parent"))],
            Author = ParseIdentity(author),
            AuthorTimestamp = ParseTimestamp(author.GetProperty("date").GetString()),
            Committer = ParseIdentity(committer),
            CommitterTimestamp = ParseTimestamp(committer.GetProperty("date").GetString()),
            Coauthors = GitCommitMetadataParser.ParseCoauthors(
                commit.GetProperty("message").GetString() ?? string.Empty),
        };
    }

    private static GitCommitIdentity ParseIdentity(JsonElement value) => new(
        value.GetProperty("name").GetString()?.Trim() ?? string.Empty,
        value.GetProperty("email").GetString()?.Trim() ?? string.Empty);

    private static DateTimeOffset ParseTimestamp(string? value) => DateTimeOffset.TryParse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind,
        out DateTimeOffset timestamp)
            ? timestamp
            : throw new FormatException("GitHub returned an invalid commit timestamp.");

    private static DateTimeOffset SelectedTimestamp(
        GitCommitMetadata commit,
        ChangePortfolioDateField dateField) => dateField == ChangePortfolioDateField.Author
            ? commit.AuthorTimestamp
            : commit.CommitterTimestamp;

    private static bool ProviderLoginMatches(
        JsonElement commit,
        IReadOnlyList<string> aliases) =>
        commit.TryGetProperty("author", out JsonElement author) &&
        author.ValueKind == JsonValueKind.Object &&
        author.TryGetProperty("login", out JsonElement login) &&
        aliases.Contains(login.GetString(), StringComparer.OrdinalIgnoreCase);
}
