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
                    ("open-pr", "identity-not-single-login" or "account-connection-unavailable" or "scoped-connection-unavailable");
        }

        if (value.HistoricalPullRequests is { } plan)
        {
            invalid |= plan.CandidateCount < 0 || plan.CacheHitCount < 0 || plan.BatchCount < 0 || plan.FallbackCount < 0 ||
                plan.CompletedCount < 0 || plan.SelectedCount < 0 || plan.PendingCount < 0 ||
                plan.CompletedCount + (long)plan.PendingCount != plan.CandidateCount || plan.CacheHitCount > plan.CompletedCount ||
                plan.SelectedCount > plan.CompletedCount || plan.FallbackCount > plan.CandidateCount - plan.CacheHitCount ||
                plan.BatchCount > value.OpenPullRequestQueryCount || discovery.Complete && plan.PendingCount != 0;
        }
        if (value.HistoricalPullRequests is { } detailed &&
            (detailed.InventoryStrategy is not null || detailed.InventoryComplete is not null || detailed.MetadataComplete is not null ||
             detailed.InventoryQueryCount is not null || detailed.HeaderQueryCount is not null || detailed.MetadataQueryCount is not null ||
             detailed.HeaderBatchCount is not null || detailed.HeaderFallbackCount is not null || detailed.CacheWriteCount is not null || detailed.ResumeState is not null))
        {
            invalid |= detailed.InventoryComplete is null || detailed.MetadataComplete is null || detailed.InventoryQueryCount is null or < 0 ||
                detailed.HeaderQueryCount is null or < 0 || detailed.MetadataQueryCount is null or < 0 ||
                detailed.HeaderBatchCount is null or < 0 || detailed.HeaderFallbackCount is null or < 0 || detailed.CacheWriteCount is null or < 0 ||
                detailed.InventoryQueryCount + (long?)detailed.HeaderQueryCount + detailed.MetadataQueryCount > value.OpenPullRequestQueryCount ||
                detailed.BatchCount > detailed.MetadataQueryCount || detailed.HeaderBatchCount > detailed.HeaderQueryCount || detailed.HeaderFallbackCount > detailed.HeaderQueryCount ||
                detailed.CacheWriteCount > detailed.CompletedCount - detailed.CacheHitCount ||
                detailed.InventoryStrategy is not ("not-observed" or "account-connection" or "scoped-connections" or "rest-pages") ||
                detailed.ResumeState is not ("completed-metadata-reusable" or "no-completed-metadata") ||
                detailed.MetadataComplete == true && (detailed.InventoryComplete != true || detailed.PendingCount != 0) ||
                discovery.Complete && (detailed.InventoryComplete != true || detailed.MetadataComplete != true) ||
                detailed.ResumeState == "completed-metadata-reusable" != (detailed.CacheHitCount + detailed.CacheWriteCount > 0);
        }
        if (value.LastRequest is { } request)
        {
            invalid |= string.IsNullOrWhiteSpace(request.Phase) || request.Operation is not ("pull-metadata-batch" or "pull-header-batch" or "pull-inventory-probe" or "pull-inventory" or
                "pull-commits" or "pull-detail" or "authentication" or "owner-inventory" or "default-head" or "candidate-discovery") ||
                request.State is not ("complete" or "incomplete") || request.PageCount < 0 || request.ElapsedMilliseconds < 0;
            invalid |= request.Api is not (null or "rest" or "graphql") ||
                request.Outcome is not (null or "success" or "fallback" or "process-exit" or "api-failure" or "http-failure" or "transport-failure" or
                    "transport-timeout" or "response-malformed" or "output-bound" or "start-failure" or "cancelled") ||
                request.HttpStatus is < 400 or > 599 || request.TimeoutOwner is not
                    (null or "provider-transport" or "discovery-deadline" or "caller" or "sibling-failure" or "unknown") ||
                request.Outcome == "success" && (request.State != "complete" || request.ExitCode != 0 || request.TimeoutOwner is not null || request.HttpStatus is not null) ||
                request.Outcome == "http-failure" && (request.HttpStatus is null || request.State != "incomplete") ||
                request.Outcome == "transport-timeout" && request.TimeoutOwner != "provider-transport" ||
                request.Outcome == "cancelled" && request.TimeoutOwner is null;
            if (request.RepositoryDigest is not null) ValidateDigest(request.RepositoryDigest, "provider.request.repositoryDigest", errors);
        }
        if (value.RepositoryObservations is { } observations)
        {
            invalid |= observations.Count > 1024 || observations.Sum(item => (long)item.QueryCount) > discovery.ProviderQueryCount ||
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
