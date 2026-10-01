namespace EffortHours.Change;

internal sealed partial class GitHubRepositoryCache
{
    /// <summary>Completes selected history only in the private cache, under its acquisition lock.</summary>
    internal async Task CompleteHistoryAsync(string identity, string head, bool fetchMissing, CancellationToken token)
    {
        string path = RepositoryPath(identity);
        if (!fetchMissing) return;
        await using FileStream cacheLock = await AcquireLockAsync(path, token).ConfigureAwait(false);
        ExternalCommandResult result = await _commands.RunAsync("git", path,
            ["rev-parse", "--is-shallow-repository"], token).ConfigureAwait(false);
        bool shallow = result.StandardOutput.Trim() == "true";
        if (!shallow)
        {
            try
            {
                _ = await _git.ReadSnapshotHistoryAsync(path, head, token).ConfigureAwait(false);
                return;
            }
            catch (ExternalCommandException) { /* Explicit acquisition may repair missing ancestry. */ }
        }
        await _commands.RunAsync("git", path,
            ["-c", "credential.helper=", "-c", "credential.helper=!gh auth git-credential",
                "fetch", "--no-tags", "--no-write-fetch-head", "--no-recurse-submodules",
                .. shallow ? UnshallowArguments : [], _fetchSource(identity), head], token).ConfigureAwait(false);
        (_, bool stillShallow) = await _git.ReadSnapshotHistoryAsync(path, head, token).ConfigureAwait(false);
        if (stillShallow) throw new SnapshotPlanningException("shallow-history", "Provider acquisition did not complete selected history; no measurement was published.");
    }

    private static readonly string[] UnshallowArguments = ["--unshallow"];
}
