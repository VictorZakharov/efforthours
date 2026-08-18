using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ResolvedPullRequest
{
    public required PullRequestReference Reference { get; init; }

    public required string BaseObjectId { get; init; }

    public required string HeadObjectId { get; init; }

    public string? BaseRefName { get; init; }

    public string? FetchSource { get; init; }

    public int? ChangedFileCount { get; init; }
}

public interface IPullRequestResolver
{
    public Task<ResolvedPullRequest> ResolveAsync(
        string repositoryPath,
        string input,
        string? repository,
        CancellationToken cancellationToken = default);
}

public sealed class GitHubPullRequestResolver : IPullRequestResolver
{
    private readonly IExternalCommandRunner _commands;

    public GitHubPullRequestResolver()
        : this(new ExternalCommandRunner())
    {
    }

    internal GitHubPullRequestResolver(IExternalCommandRunner commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public async Task<ResolvedPullRequest> ResolveAsync(
        string repositoryPath,
        string input,
        string? repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        List<string> arguments =
        [
            "pr",
            "view",
            input,
            "--json",
            "number,url,baseRefName,baseRefOid,headRefOid,changedFiles",
        ];
        if (!string.IsNullOrWhiteSpace(repository))
        {
            arguments.Add("--repo");
            arguments.Add(repository);
        }

        ExternalCommandResult result;
        try
        {
            result = await _commands.RunAsync(
                "gh",
                repositoryPath,
                arguments,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalCommandException exception)
        {
            throw new InvalidOperationException(
                "Could not resolve the pull request through optional gh CLI support. " +
                "Confirm that gh is installed, authenticated, and allowed to access the selected repository. " +
                exception.Message,
                exception);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            JsonElement root = document.RootElement;
            int number = root.GetProperty("number").GetInt32();
            string baseObjectId = RequireObjectId(root, "baseRefOid");
            string headObjectId = RequireObjectId(root, "headRefOid");
            string? url = root.TryGetProperty("url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null;
            string baseRefName = RequireText(root, "baseRefName");
            int changedFileCount = root.GetProperty("changedFiles").GetInt32();
            if (changedFileCount < 0)
            {
                throw new InvalidOperationException("gh returned a negative changedFiles value.");
            }

            return new ResolvedPullRequest
            {
                BaseObjectId = baseObjectId,
                HeadObjectId = headObjectId,
                BaseRefName = baseRefName,
                FetchSource = FetchSource(url, number),
                ChangedFileCount = changedFileCount,
                Reference = new PullRequestReference
                {
                    Input = input,
                    Number = number,
                    Repository = repository,
                    Url = string.IsNullOrWhiteSpace(url) ? null : url,
                },
            };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "gh returned an incomplete pull-request identity. Expected number, URL, base ref, " +
                "base/head object IDs, and changed-file count.",
                exception);
        }
    }

    private static string RequireText(JsonElement root, string name)
    {
        string value = root.GetProperty(name).GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"gh returned an invalid {name} value.");
        }

        return value;
    }

    private static string FetchSource(string? pullRequestUrl, int number)
    {
        if (!Uri.TryCreate(pullRequestUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("gh returned an invalid pull-request URL.");
        }

        string suffix = $"/pull/{number}";
        string path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith(suffix, StringComparison.Ordinal) || path.Length == suffix.Length)
        {
            throw new InvalidOperationException("gh returned a pull-request URL with an unexpected path.");
        }

        UriBuilder source = new(uri)
        {
            Path = path[..^suffix.Length] + ".git",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return source.Uri.AbsoluteUri;
    }

    private static string RequireObjectId(JsonElement root, string name)
    {
        string value = root.GetProperty(name).GetString()?.ToLowerInvariant() ?? string.Empty;
        if (value.Length is not (40 or 64) || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException($"gh returned an invalid {name} value.");
        }

        return value;
    }
}
