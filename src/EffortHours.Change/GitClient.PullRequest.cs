namespace EffortHours.Change;

public sealed partial class GitClient
{
    public async Task FetchPullRequestObjectsAsync(
        string repositoryPath,
        string fetchSource,
        string baseRefName,
        int pullRequestNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fetchSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRefName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pullRequestNumber);

        try
        {
            await _commands.RunAsync(
                "git",
                repositoryPath,
                [
                    "-c",
                    "credential.helper=",
                    "-c",
                    "credential.helper=!gh auth git-credential",
                    "fetch",
                    "--no-tags",
                    "--no-write-fetch-head",
                    "--no-recurse-submodules",
                    fetchSource,
                    $"refs/heads/{baseRefName}",
                    $"refs/pull/{pullRequestNumber}/head",
                ],
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalCommandException exception)
        {
            throw new InvalidOperationException(
                "Git could not acquire the selected pull-request objects without updating local refs. " +
                "Confirm that the authenticated gh account can read the provider repository and that the " +
                "PR and base refs still exist. " + exception.Message,
                exception);
        }
    }
}
