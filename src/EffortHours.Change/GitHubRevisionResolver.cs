namespace EffortHours.Change;

internal sealed record ResolvedGitRevision(string Selector, string ObjectId);

internal interface IGitHubRevisionResolver
{
    public Task<IReadOnlyList<ResolvedGitRevision>> ResolveAsync(
        string repositoryIdentity,
        IReadOnlyList<string> selectors,
        CancellationToken cancellationToken);
}

internal sealed class GitHubRevisionResolver : IGitHubRevisionResolver
{
    private readonly IExternalCommandRunner _commands;

    public GitHubRevisionResolver()
        : this(new ExternalCommandRunner())
    {
    }

    internal GitHubRevisionResolver(IExternalCommandRunner commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public async Task<IReadOnlyList<ResolvedGitRevision>> ResolveAsync(
        string repositoryIdentity,
        IReadOnlyList<string> selectors,
        CancellationToken cancellationToken)
    {
        string repository = GitHubRepositoryIdentity.Normalize(repositoryIdentity);
        ArgumentNullException.ThrowIfNull(selectors);
        if (selectors.Count is < 1 or > 3 || selectors.Any(selector =>
                string.IsNullOrWhiteSpace(selector) || selector.Length > 512 ||
                selector.Any(char.IsControl)))
        {
            throw new ArgumentException(
                "Checkout-free Git selection requires between one and three bounded provider revisions.",
                nameof(selectors));
        }

        List<ResolvedGitRevision> resolved = [];
        foreach (string selector in selectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string endpoint = $"repos/{repository}/commits/{Uri.EscapeDataString(selector)}";
            ExternalCommandResult result;
            try
            {
                result = await _commands.RunAsync(
                    "gh",
                    Environment.CurrentDirectory,
                    ["api", endpoint, "--jq", ".sha"],
                    cancellationToken,
                    requireSuccess: false).ConfigureAwait(false);
            }
            catch (ExternalCommandException exception)
            {
                throw GitHubProviderFailure.FromStart(
                    exception,
                    GitHubProviderFailure.RevisionResolutionPhase);
            }

            if (result.ExitCode != 0)
            {
                throw GitHubProviderFailure.FromResult(
                    result,
                    GitHubProviderFailure.RevisionResolutionPhase);
            }

            string objectId = result.StandardOutput.Trim().ToLowerInvariant();
            if (objectId.Length is not (40 or 64) || objectId.Any(character => !Uri.IsHexDigit(character)))
            {
                throw GitHubProviderFailure.Malformed(
                    GitHubProviderFailure.RevisionResolutionPhase,
                    new InvalidOperationException("GitHub returned an invalid commit identity."));
            }

            resolved.Add(new ResolvedGitRevision(selector, objectId));
        }

        return resolved;
    }
}
