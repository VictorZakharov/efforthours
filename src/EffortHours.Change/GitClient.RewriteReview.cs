using System.Globalization;

namespace EffortHours.Change;

public sealed partial class GitClient
{
    internal async Task<(DateTimeOffset Author, DateTimeOffset Committer)> ReadRewriteTimestampsAsync(
        string repository, string objectId, CancellationToken token)
    {
        ExternalCommandResult result = await _commands.RunAsync("git", repository,
            ["show", "-s", "--format=%aI%n%cI", objectId], token).ConfigureAwait(false);
        string[] values = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2 || !DateTimeOffset.TryParse(values[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset author) ||
            !DateTimeOffset.TryParse(values[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset committer))
            throw new InvalidOperationException("Git returned invalid immutable rewrite timestamps.");
        return (author.ToUniversalTime(), committer.ToUniversalTime());
    }
}
