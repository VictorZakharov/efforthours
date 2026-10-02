namespace EffortHours.Change;

public sealed partial class GitClient
{
    public async Task<string?> ReadConfiguredEmailAsync(string repositoryPath, CancellationToken token)
    {
        ExternalCommandResult result = await _commands.RunAsync("git", repositoryPath,
            ["config", "--get", "user.email"], token, requireSuccess: false).ConfigureAwait(false);
        string email = result.StandardOutput.Trim();
        return result.ExitCode == 0 && email.Length is > 0 and <= 320 && !email.Contains('\n') ? email : null;
    }
}
