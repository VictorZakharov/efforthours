using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class GitHubProviderException : InvalidOperationException
{
    public GitHubProviderException(EffortHoursAgentAction action, string message, Exception? inner = null)
        : base(message, inner)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public EffortHoursAgentAction Action { get; }
}

internal static class GitHubProviderFailure
{
    public const string AuthenticationPhase = "provider-authentication";
    public const string OwnerInventoryPhase = "owner-inventory";
    public const string CandidateDiscoveryPhase = "candidate-discovery";
    public const string DefaultHeadPhase = "default-head-discovery";
    public const string OpenPullRequestPhase = "open-pr-discovery";
    public const string PullRequestResolutionPhase = "pull-request-resolution";
    public const string RevisionResolutionPhase = "git-revision-resolution";
    public const string ManagedCachePhase = "managed-cache-acquisition";

    public static GitHubProviderException FromStart(
        ExternalCommandException exception,
        string phase) => Create(
            "github-cli-executable-missing",
            phase,
            "install-github-cli",
            "The GitHub CLI executable could not be started.",
            exception);

    public static GitHubProviderException FromResult(
        ExternalCommandResult result,
        string phase)
    {
        string detail = result.StandardError + "\n" + result.StandardOutput;
        if (Contains(detail, "access is denied", "permission denied", "eacces", "unauthorizedaccessexception"))
        {
            return Create(
                "github-cli-config-access-denied",
                AuthenticationPhase,
                "retry-exact-command-with-permission",
                "The GitHub CLI could not access its authenticated user configuration.",
                approvalPrefix: ["eh", "change", "today"],
                retryLimit: 1);
        }

        if (Contains(detail, "not logged into", "gh auth login", "authentication required", "http 401"))
        {
            return Create(
                "github-cli-unauthenticated",
                AuthenticationPhase,
                "authenticate-github-cli",
                "The GitHub CLI is not authenticated.");
        }

        if (Contains(detail, "rate limit", "http 429", "secondary rate"))
        {
            return Create(
                "github-provider-rate-limited",
                phase,
                "wait-for-provider-rate-limit",
                "GitHub rate limiting prevented complete discovery.");
        }

        if (Contains(detail, "http 403", "http 404", "forbidden", "not found"))
        {
            if (phase == PullRequestResolutionPhase)
            {
                return Create(
                    "github-pull-request-forbidden-or-not-found",
                    phase,
                    "verify-pull-request-and-access",
                    "The requested GitHub pull request was not found or is not accessible.");
            }

            if (phase == RevisionResolutionPhase)
            {
                return Create(
                    "github-revision-forbidden-or-not-found",
                    phase,
                    "verify-revision-and-access",
                    "The requested GitHub revision was not found or is not accessible.");
            }

            return Create(
                "github-owner-forbidden-or-not-found",
                OwnerInventoryPhase,
                "verify-owner-and-access",
                "The requested GitHub owner was not found or is not accessible.");
        }

        if (Contains(
            detail,
            "could not resolve host",
            "unable to connect",
            "connection refused",
            "network is unreachable",
            "tls handshake",
            "timed out"))
        {
            return Create(
                "github-network-unavailable",
                phase,
                "restore-network-and-retry",
                "GitHub could not be reached.");
        }

        return Create(
            "github-provider-request-failed",
            phase,
            "inspect-github-cli-health",
            "GitHub provider discovery did not complete.");
    }

    public static GitHubProviderException Malformed(string phase, Exception inner) => Create(
        "github-provider-response-malformed",
        phase,
        "retry-after-valid-provider-response",
        "GitHub returned a malformed or incomplete response.",
        inner);

    public static GitHubProviderException ManagedCacheAccessDenied(Exception inner) => Create(
        "managed-cache-access-denied",
        ManagedCachePhase,
        "grant-managed-cache-access",
        "EffortHours could not access its managed repository cache.",
        inner);

    private static GitHubProviderException Create(
        string code,
        string phase,
        string suggestedAction,
        string message,
        Exception? inner = null,
        IReadOnlyList<string>? approvalPrefix = null,
        int retryLimit = 0) => new(
            new EffortHoursAgentAction
            {
                FailureCode = code,
                Phase = phase,
                SuggestedAction = suggestedAction,
                SuggestedApprovalPrefix = approvalPrefix ?? [],
                RetryLimit = retryLimit,
            },
            message,
            inner);

    private static bool Contains(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
