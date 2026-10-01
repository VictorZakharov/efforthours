namespace EffortHours.Change;

public sealed partial class GitClient
{
    public async Task ValidateArchivePolicyAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ExternalCommandResult info = await _commands.RunAsync("git", repositoryPath,
            ["rev-parse", "--git-path", "info/attributes"], cancellationToken).ConfigureAwait(false);
        string path = info.StandardOutput.Trim();
        if (!Path.IsPathRooted(path)) path = Path.Combine(repositoryPath, path);
        if (File.Exists(path) && new FileInfo(path).Length != 0)
            throw new InvalidDataException("Repository-local info/attributes are outside immutable archive provenance; remove that override from the execution copy.");
    }

    private async Task<IReadOnlyList<string>> ArchiveArgumentsAsync(string repositoryPath, string objectId, CancellationToken token)
    {
        await ValidateArchivePolicyAsync(repositoryPath, token).ConfigureAwait(false);
        // Archive conversion must never invoke an untrusted clean/smudge/process filter.
        // Query names only, then override every effective driver command and required flag.
        ExternalCommandResult filters = await _commands.RunAsync("git", repositoryPath,
            ["config", "--null", "--name-only", "--get-regexp", "^filter\\..*\\.(smudge|clean|process|required)$"],
            token, requireSuccess: false).ConfigureAwait(false);
        if (filters.ExitCode is not (0 or 1)) throw new InvalidDataException("Could not validate archive filter configuration.");
        List<string> arguments = ["-c", "core.attributesFile=", "-c", "core.autocrlf=false", "-c", "core.eol=lf"];
        string[] keys = filters.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (keys.Length > 1024) throw new InvalidDataException("Archive filter configuration exceeds its bounded key budget.");
        foreach (string key in keys)
        {
            arguments.Add("-c");
            arguments.Add(key + (key.EndsWith(".required", StringComparison.OrdinalIgnoreCase) ? "=false" : "="));
        }
        arguments.AddRange(["archive", "--format=tar", objectId]);
        return arguments;
    }
}
