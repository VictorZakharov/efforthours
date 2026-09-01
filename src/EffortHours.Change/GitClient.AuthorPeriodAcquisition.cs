namespace EffortHours.Change;

public sealed partial class GitClient
{
    public async Task FetchAuthorPeriodHeadObjectsAsync(
        string repositoryPath,
        string fetchSource,
        IReadOnlyList<string> sourceRefs,
        CancellationToken cancellationToken = default) => await FetchManagedObjectsAsync(
            repositoryPath,
            fetchSource,
            sourceRefs,
            cancellationToken).ConfigureAwait(false);

    public async Task FetchManagedObjectsAsync(
        string repositoryPath,
        string fetchSource,
        IReadOnlyList<string> sourceRefs,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fetchSource);
        ArgumentNullException.ThrowIfNull(sourceRefs);
        if (sourceRefs.Count is < 1 or > 32 || sourceRefs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Managed acquisition requires between 1 and 32 provider source refs or commits.",
                nameof(sourceRefs));
        }

        List<string> arguments =
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
            .. sourceRefs.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        ];
        try
        {
            await _commands.RunAsync(
                "git",
                repositoryPath,
                arguments,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalCommandException exception)
        {
            throw new InvalidOperationException(
                "Git could not acquire the selected immutable objects without updating local refs. " +
                "Confirm that the authenticated gh account can read the provider repository and " +
                "that the selected refs or commits still exist.",
                exception);
        }
    }
}
