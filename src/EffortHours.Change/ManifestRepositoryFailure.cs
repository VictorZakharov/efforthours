namespace EffortHours.Change;

internal static class ManifestRepositoryFailure
{
    public static InvalidOperationException Create(string repositoryId, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentNullException.ThrowIfNull(exception);
        string message = exception is ExternalCommandException commandFailure
            ? DescribeGitFailure(repositoryId, commandFailure.Message)
            : $"Repository '{repositoryId}' is not a readable local Git repository.";
        return new InvalidOperationException(message, exception);
    }

    private static string DescribeGitFailure(string repositoryId, string detail)
    {
        if (Contains(detail, "detected dubious ownership") ||
            Contains(detail, "safe.directory") ||
            Contains(detail, "unsafe repository"))
        {
            return $"Repository '{repositoryId}' was rejected by Git's dubious-ownership safety check. " +
                "Configure only this repository as a process-local safe.directory and retry.";
        }

        if (Contains(detail, "not a git repository"))
        {
            return $"Repository '{repositoryId}' is not a Git worktree.";
        }

        if (Contains(detail, "unsupported repository format") ||
            (Contains(detail, "repository version") && Contains(detail, "not supported")))
        {
            return $"Repository '{repositoryId}' uses a repository format unsupported by the installed Git executable.";
        }

        return $"Repository '{repositoryId}' is not a readable local Git repository.";
    }

    private static bool Contains(string value, string fragment) =>
        value.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
