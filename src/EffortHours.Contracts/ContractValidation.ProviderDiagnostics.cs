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

        if (value.HistoricalPullRequests is { } plan)
        {
            invalid |= plan.CandidateCount < 0 || plan.CacheHitCount < 0 || plan.BatchCount < 0 || plan.FallbackCount < 0 ||
                plan.CompletedCount < 0 || plan.SelectedCount < 0 || plan.PendingCount < 0 ||
                plan.CompletedCount + (long)plan.PendingCount != plan.CandidateCount || plan.CacheHitCount > plan.CompletedCount ||
                plan.SelectedCount > plan.CompletedCount || plan.FallbackCount > plan.CandidateCount - plan.CacheHitCount ||
                plan.BatchCount > value.OpenPullRequestQueryCount || discovery.Complete && plan.PendingCount != 0;
        }
        if (value.LastRequest is { } request)
        {
            invalid |= string.IsNullOrWhiteSpace(request.Phase) || request.Operation is not ("pull-metadata-batch" or "pull-inventory" or
                "pull-commits" or "pull-detail" or "authentication" or "owner-inventory" or "default-head" or "candidate-discovery") ||
                request.State is not ("complete" or "incomplete") || request.PageCount < 0 || request.ElapsedMilliseconds < 0;
            if (request.RepositoryDigest is not null) ValidateDigest(request.RepositoryDigest, "provider.request.repositoryDigest", errors);
        }
        if (value.RepositoryObservations is { } observations)
        {
            invalid |= observations.Count > 768 || observations.Sum(item => (long)item.QueryCount) > discovery.ProviderQueryCount ||
                observations.Sum(item => (long)item.PageCount) > discovery.ProviderPageCount ||
                observations.Select(item => (item.RepositoryDigest, item.Phase)).Distinct().Count() != observations.Count;
            foreach (ChangePortfolioProviderRepositoryObservation observation in observations)
            {
                ValidateDigest(observation.RepositoryDigest, "provider.repositoryDigest", errors);
                invalid |= string.IsNullOrWhiteSpace(observation.Phase) || observation.State is not ("complete" or "incomplete") ||
                    observation.QueryCount < 1 || observation.PageCount < 0 || observation.ElapsedMilliseconds < 0m ||
                    observation.ElapsedKind is not (null or "cumulative-request") || observation.WallElapsedMilliseconds < 0m ||
                    observation.WallElapsedMilliseconds > discovery.ElapsedMilliseconds;
            }
        }

        if (invalid)
        {
            errors.Add("Host provider diagnostics are invalid or inconsistent.");
        }
    }
}
