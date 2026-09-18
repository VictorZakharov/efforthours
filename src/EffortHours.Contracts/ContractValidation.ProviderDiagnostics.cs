using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateProviderDiagnostics(
        ChangePortfolioHostDiscovery discovery,
        List<string> errors)
    {
        if (discovery.ProviderDiagnostics is not { } value)
        {
            return; // Older v1 reports do not carry these operational observations.
        }

        bool invalid = value.MetadataCacheStatus is not ("not-observed" or "missing" or
            "invalid-size" or "invalid-content" or "unsupported-protocol" or
            "identity-mismatch" or "expired" or "hit" or "hit-owner-only") ||
            value.IdentityResolution is not ("not-observed" or "direct-login" or "explicit-login" or
                "provider-linked-aliases" or "multiple-logins" or "team") ||
            value.OpenPullRequestCandidateRepositoryCount < 0 ||
            value.OpenPullRequestCandidateRepositoryCount > discovery.ConsideredRepositoryCount ||
            value.DefaultHeadBatchCount < 0 ||
            value.DefaultHeadQueryCount < value.DefaultHeadBatchCount ||
            value.OpenPullRequestAccountQueryCount is < 0 or > 1 ||
            value.OpenPullRequestQueryCount < value.OpenPullRequestAccountQueryCount ||
            (long)value.DefaultHeadQueryCount + value.OpenPullRequestQueryCount > discovery.ProviderQueryCount ||
            discovery.ProviderMetadataCacheHit != (value.MetadataCacheStatus is "hit" or "hit-owner-only") ||
            value.Fallbacks.Count > 8 ||
            value.Fallbacks.Select(item => (item.Phase, item.Reason)).Distinct().Count() != value.Fallbacks.Count;

        foreach (ChangePortfolioProviderFallback item in value.Fallbacks)
        {
            invalid |= item.RepositoryCount < 0 ||
                item.RepositoryCount > discovery.ConsideredRepositoryCount ||
                (item.Phase, item.Reason) is not
                    ("default-head", "provider-unavailable" or "provider-errors" or "malformed-response" or
                        "repository-unavailable" or "branch-changed" or "incomplete-history") and not
                    ("open-pr", "identity-not-single-login" or "account-connection-unavailable");
        }

        if (invalid)
        {
            errors.Add("Host provider diagnostics are invalid or inconsistent.");
        }
    }
}
