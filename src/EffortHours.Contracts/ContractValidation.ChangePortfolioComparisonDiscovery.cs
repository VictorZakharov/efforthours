using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateHostDiscovery(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        bool hasProviderMetadata = report.AsOf is not null;
        if (new[]
            {
                report.Discovery is not null,
                report.ScopeProfile is not null,
                report.ScopeSummary is not null,
            }.Any(value => value != hasProviderMetadata))
        {
            errors.Add(
                "Comparison asOf, host discovery, scope profile, and scope summary metadata must be present together.");
            return;
        }

        if (report.AsOf is null)
        {
            return;
        }

        DateTimeOffset? until = report.Selection.AuthorPeriodManifest?.UntilExclusive;
        if (report.AsOf.Value.Offset != TimeSpan.Zero ||
            until is null ||
            report.AsOf < until ||
            report.NativePeriod is null && report.AsOf != until)
        {
            errors.Add(
                "Comparison asOf must be UTC and equal or follow the selected interval end.");
        }

        ChangePortfolioHostDiscovery discovery = report.Discovery!;
        int selectedRepositoryCount = report.Selection.AuthorPeriodManifest!.Repositories.Count;
        int selectedHeadCount = report.Selection.AuthorPeriodManifest.Repositories.Sum(
            repository => repository.Heads.Count);
        if (discovery.Protocol != ChangePortfolioComparisonPolicies.GitHubManagedCacheDiscoveryV1 ||
            discovery.Provider != "github" ||
            discovery.Scope != "owner-provider-discovery")
        {
            errors.Add("Host discovery uses an unsupported provider, scope, or protocol.");
        }

        ValidateDigest(discovery.ScopeDigest, "discovery.scopeDigest", errors);
        RequireText(discovery.IdentitySources, "discovery.identitySources", errors);
        int[] counts =
        [
            discovery.ProviderRepositoryCount,
            discovery.ConsideredRepositoryCount,
            discovery.ActiveRepositoryCount,
            discovery.DefaultHeadCount,
            discovery.OpenPullRequestHeadCount,
            discovery.OpenPullRequestCount,
            discovery.ProviderQueryCount,
            discovery.ProviderPageCount,
            discovery.ProviderProcessCount,
            discovery.LocalObjectCount,
            discovery.AcquiredObjectCount,
        ];
        if (counts.Any(count => count < 0) ||
            discovery.AcquiredBytes < 0 ||
            discovery.ProviderProcessStartupMilliseconds < 0m ||
            discovery.ProviderProcessCount > discovery.ProviderQueryCount ||
            discovery.Complete && discovery.ProviderProcessCount != discovery.ProviderQueryCount ||
            discovery.ConsideredRepositoryCount > discovery.ProviderRepositoryCount ||
            discovery.ActiveRepositoryCount > discovery.ConsideredRepositoryCount ||
            discovery.ActiveRepositoryCount != selectedRepositoryCount ||
            discovery.DefaultHeadCount > discovery.ActiveRepositoryCount ||
            discovery.DefaultHeadCount + discovery.OpenPullRequestHeadCount != selectedHeadCount ||
            discovery.OpenPullRequestHeadCount > discovery.OpenPullRequestCount ||
            discovery.DefaultHeadCount + discovery.OpenPullRequestHeadCount >
                discovery.LocalObjectCount + discovery.AcquiredObjectCount ||
            discovery.ElapsedMilliseconds < 0m)
        {
            errors.Add("Host discovery counts are inconsistent.");
        }

        ChangePortfolioScopeProfile profile = report.ScopeProfile!;
        if (profile.Id != "engineering" ||
            string.IsNullOrWhiteSpace(profile.Version) ||
            profile.Version.Length > 128 ||
            profile.Source is not ("bundled" or "bundled+user-extend" or
                "bundled+user-replace" or "unavailable") ||
            profile.Source == "unavailable" && report.Status != ChangePortfolioComparisonStatus.Incomplete ||
            profile.ImportantExclusions.Count == 0 ||
            profile.ImportantExclusions.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("The provider-assisted engineering scope profile is invalid.");
        }

        ValidateDigest(profile.Digest, "scopeProfile.digest", errors);
        ChangePortfolioScopeSummary summary = report.ScopeSummary!;
        if (summary.IdentitySelectedCommitCount < 0 ||
            summary.AdmittedCommitCount < 0 ||
            summary.ScopeEmptyCommitCount < 0 ||
            summary.AdmittedCommitCount + summary.ScopeEmptyCommitCount !=
                summary.IdentitySelectedCommitCount)
        {
            errors.Add("The provider-assisted scope summary counts are inconsistent.");
        }

        if (report.Status == ChangePortfolioComparisonStatus.Complete && !discovery.Complete)
        {
            errors.Add("A complete comparison requires complete host discovery.");
        }
    }
}
