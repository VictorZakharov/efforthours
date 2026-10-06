using System.Text.RegularExpressions;

namespace EffortHours.Change;

internal static partial class GitHubProviderRequestFailure
{
    public static (string Outcome, int? HttpStatus, string? TimeoutOwner) Classify(ExternalCommandResult result)
    {
        if (result.ExitCode == 0) return ("success", null, null);
        string detail = result.StandardError + "\n" + result.StandardOutput;
        Match match = HttpStatusPattern().Match(detail);
        int? status = match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
        if (status is not null) return ("http-failure", status, null);
        if (Contains(detail, "GraphQL:")) return ("api-failure", null, null);
        if (Contains(detail, "timed out", "timeout", "deadline exceeded"))
            return ("transport-timeout", status, "provider-transport");
        if (Contains(detail, "could not resolve host", "unable to connect", "connection refused", "network is unreachable",
            "tls handshake", "connection reset", "unexpected eof")) return ("transport-failure", null, null);
        return ("process-exit", null, null);
    }

    private static bool Contains(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"\bHTTP(?:/\d(?:\.\d)?)?\s+(?:status\s+)?([45]\d\d)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpStatusPattern();
}
