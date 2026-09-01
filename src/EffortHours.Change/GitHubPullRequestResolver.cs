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
                cancellationToken,
                requireSuccess: false).ConfigureAwait(false);
        }
        catch (ExternalCommandException exception)
        {
            throw GitHubProviderFailure.FromStart(
                exception,
                GitHubProviderFailure.PullRequestResolutionPhase);
        }

        if (result.ExitCode != 0)
        {
            throw GitHubProviderFailure.FromResult(
                result,
                GitHubProviderFailure.PullRequestResolutionPhase);
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
            GitHubPullRequestLocator locator = GitHubPullRequestLocatorParser.Parse(
                url ?? string.Empty,
                repository);
            if (locator.Number != number)
            {
                throw new InvalidOperationException(
                    "gh returned inconsistent pull-request number and URL values.");
            }

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
                FetchSource = "https://github.com/" + locator.RepositoryIdentity + ".git",
                ChangedFileCount = changedFileCount,
                Reference = new PullRequestReference
                {
                    Input = input,
                    Number = number,
                    Repository = locator.RepositoryIdentity,
                    Url = string.IsNullOrWhiteSpace(url) ? null : url,
                },
            };
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw GitHubProviderFailure.Malformed(
                GitHubProviderFailure.PullRequestResolutionPhase,
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
