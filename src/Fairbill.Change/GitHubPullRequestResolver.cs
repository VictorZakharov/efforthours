using System.Text.Json;
using Fairbill.Contracts.V1;

namespace Fairbill.Change;

public sealed record ResolvedPullRequest
{
    public required PullRequestReference Reference { get; init; }

    public required string BaseObjectId { get; init; }

    public required string HeadObjectId { get; init; }
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
            "number,url,baseRefOid,headRefOid",
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
            return new ResolvedPullRequest
            {
                BaseObjectId = baseObjectId,
                HeadObjectId = headObjectId,
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
                "gh returned an incomplete pull-request identity. Expected number, baseRefOid, and headRefOid.",
                exception);
        }
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
