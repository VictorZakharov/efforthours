using System.Globalization;
using System.Text.Json;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    internal static async Task ResolveNoreplyAliasesAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        GitHubPullAuthorIdentity identity,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        foreach (string alias in identity.UnresolvedAliases())
        {
            if (!TryParseNoreply(alias, out string login, out long accountId))
            {
                continue;
            }

            string? json = await RunApiAsync(commands, workingDirectory,
                ["api", $"users/{login}"], counters, paginated: false, optional: false,
                cancellationToken, capabilityFallback: true,
                failurePhase: GitHubProviderFailure.OpenPullRequestPhase).ConfigureAwait(false);
            if (json is null)
            {
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement user = document.RootElement;
                string? currentLogin = user.GetProperty("login").GetString();
                if (user.GetProperty("id").GetInt64() == accountId &&
                    login.Equals(currentLogin, StringComparison.OrdinalIgnoreCase))
                {
                    identity.Associate(alias, currentLogin!);
                }
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or
                InvalidOperationException or FormatException)
            {
                throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, exception);
            }
        }
    }

    internal static bool TryParseNoreply(string alias, out string login, out long accountId)
    {
        login = string.Empty;
        accountId = 0;
        const string suffix = "@users.noreply.github.com";
        if (!alias.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string local = alias[..^suffix.Length];
        int separator = local.IndexOf('+');
        if (separator <= 0 || !long.TryParse(local.AsSpan(0, separator), NumberStyles.None,
            CultureInfo.InvariantCulture, out accountId) || accountId <= 0)
        {
            return false;
        }

        login = local[(separator + 1)..];
        return GitHubPullAuthorIdentity.IsLogin(login);
    }
}
