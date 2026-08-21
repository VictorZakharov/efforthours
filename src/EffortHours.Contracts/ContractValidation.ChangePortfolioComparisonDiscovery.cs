using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateHostDiscovery(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        if ((report.AsOf is null) != (report.Discovery is null))
        {
            errors.Add("Comparison asOf and host discovery metadata must be present together.");
            return;
        }

        if (report.AsOf is null)
        {
            return;
        }

        if (report.AsOf.Value.Offset != TimeSpan.Zero ||
            report.AsOf < report.Selection.AuthorPeriodManifest?.SinceInclusive ||
            report.AsOf >= report.Selection.AuthorPeriodManifest?.UntilExclusive)
        {
            errors.Add("Comparison asOf must be a UTC instant inside the selected interval.");
        }

        ChangePortfolioHostDiscovery discovery = report.Discovery!;
        if (discovery.Protocol != ChangePortfolioComparisonPolicies.GitHubWorkspaceDiscoveryV1 ||
            discovery.Provider != "github" ||
            discovery.Scope != "owner-workspace-intersection")
        {
            errors.Add("Host discovery uses an unsupported provider, scope, or protocol.");
        }

        ValidateDigest(discovery.ScopeDigest, "discovery.scopeDigest", errors);
        RequireText(discovery.IdentitySources, "discovery.identitySources", errors);
        int[] counts =
        [
            discovery.ProviderRepositoryCount,
            discovery.WorkspaceRepositoryCount,
            discovery.ConsideredRepositoryCount,
            discovery.ActiveRepositoryCount,
            discovery.DefaultHeadCount,
            discovery.OpenPullRequestHeadCount,
            discovery.ProviderQueryCount,
            discovery.ProviderPageCount,
            discovery.LocalObjectCount,
            discovery.AcquiredObjectCount,
        ];
        if (counts.Any(count => count < 0) ||
            discovery.ConsideredRepositoryCount > discovery.ProviderRepositoryCount ||
            discovery.ConsideredRepositoryCount > discovery.WorkspaceRepositoryCount ||
            discovery.ActiveRepositoryCount > discovery.ConsideredRepositoryCount ||
            discovery.DefaultHeadCount != discovery.ActiveRepositoryCount ||
            discovery.DefaultHeadCount + discovery.OpenPullRequestHeadCount >
                discovery.LocalObjectCount + discovery.AcquiredObjectCount ||
            discovery.ElapsedMilliseconds < 0m)
        {
            errors.Add("Host discovery counts are inconsistent.");
        }

        if (report.Status == ChangePortfolioComparisonStatus.Complete && !discovery.Complete)
        {
            errors.Add("A complete comparison requires complete host discovery.");
        }
    }
}
