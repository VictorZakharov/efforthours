using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static partial class ChangePortfolioTodayMarkdownRenderer
{
    private static void AppendProviderDiagnostics(
        StringBuilder markdown,
        ChangePortfolioProviderDiagnostics? diagnostics)
    {
        if (diagnostics is null)
        {
            return;
        }

        markdown.Append("- Provider metadata: ").Append(diagnostics.MetadataCacheStatus)
            .Append("; default-head queries: ").Append(diagnostics.DefaultHeadQueryCount)
            .Append(" (").Append(diagnostics.DefaultHeadBatchCount).Append(" batches); open-PR queries: ")
            .Append(diagnostics.OpenPullRequestQueryCount).Append(" (")
            .Append(diagnostics.OpenPullRequestAccountQueryCount).AppendLine(" account inventory).");
        markdown.Append("- PR identity: ").Append(diagnostics.IdentityResolution)
            .Append("; candidate repositories: ")
            .Append(diagnostics.OpenPullRequestCandidateRepositoryCount).AppendLine(".");
        foreach (ChangePortfolioProviderFallback fallback in diagnostics.Fallbacks)
        {
            markdown.Append("- Discovery fallback: ").Append(fallback.Phase)
                .Append(" / ").Append(fallback.Reason).Append(" / ")
                .Append(fallback.RepositoryCount).AppendLine(" repositories.");
        }
    }
}
