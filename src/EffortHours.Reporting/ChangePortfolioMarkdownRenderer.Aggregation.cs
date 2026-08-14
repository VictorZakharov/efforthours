using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public static partial class ChangePortfolioMarkdownRenderer
{
    private static void AppendAggregation(
        StringBuilder markdown,
        ChangePortfolioAggregation aggregation)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Contributor match view");
        markdown.AppendLine();
        markdown.AppendLine("| Contributor | Selected | Direct | Co-author | Single match | Shared match | Zero row |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (ChangePortfolioContributorSummary contributor in aggregation.Contributors)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(contributor.ContributorId)).Append("` | ")
                .Append(contributor.SelectedCommitCount).Append(" | ")
                .Append(contributor.DirectAuthorMatchCount).Append(" | ")
                .Append(contributor.CoauthorMatchCount).Append(" | ")
                .Append(contributor.SingleContributorSelectedCommitCount).Append(" | ")
                .Append(contributor.SharedContributorSelectedCommitCount).Append(" | ")
                .Append(contributor.NoSelectedCommits ? "yes" : "no").AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine(
            "Contributor counts are an associated-match view. EHE is additive only through the exclusive groups below; shared groups are never copied into individual rows.");
        markdown.AppendLine();
        markdown.AppendLine("## Contributor match-set allocation");
        markdown.AppendLine();
        markdown.AppendLine("| Group | Contributors | Kind | Commits | Direct | Co-author | Low | Expected | High | Repositories |");
        markdown.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (ChangePortfolioContributorGroup group in aggregation.ContributorGroups)
        {
            markdown.Append("| `").Append(ReportFormatting.Escape(Short(group.Id))).Append("` | ")
                .Append(ReportFormatting.Escape(string.Join(", ", group.ContributorIds))).Append(" | ")
                .Append(ReportFormatting.Kebab(group.Kind)).Append(" | ")
                .Append(group.SelectedCommitCount).Append(" | ")
                .Append(group.DirectAuthorMatchCount).Append(" | ")
                .Append(group.CoauthorMatchCount).Append(" | ")
                .Append(ReportFormatting.Hours(group.NormalizedEffort.Low)).Append(" | ")
                .Append(ReportFormatting.Hours(group.NormalizedEffort.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(group.NormalizedEffort.High)).Append(" | ")
                .Append(group.RepositoryAllocations.Count).AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("Contributor groups sum exactly to the authoritative portfolio low/expected/high range.");
        markdown.AppendLine();
        markdown.AppendLine("| Group/repository | Items | Isolated expected | Normalized expected | Expected delta | Adjustments | Uncertainty |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (ChangePortfolioContributorGroup group in aggregation.ContributorGroups)
        {
            foreach (ChangePortfolioContributorRepositoryAllocation allocation in group.RepositoryAllocations)
            {
                markdown.Append("| `").Append(ReportFormatting.Escape(Short(group.Id))).Append("` / ")
                    .Append(ReportFormatting.Escape(allocation.RepositoryId)).Append(" | ")
                    .Append(allocation.ItemIds.Count).Append(" | ")
                    .Append(ReportFormatting.Hours(allocation.IsolatedEffort.Expected)).Append(" | ")
                    .Append(ReportFormatting.Hours(allocation.NormalizedEffort.Expected)).Append(" | ")
                    .Append(ReportFormatting.Hours(allocation.ReconciliationDelta.Expected)).Append(" | ")
                    .Append(allocation.AdjustmentIds.Count).Append(" | ")
                    .Append(allocation.UncertaintyReasons.Count).AppendLine(" |");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Repository and head coverage");
        markdown.AppendLine();
        markdown.AppendLine("| Repository | Commits | Direct | Co-author | Shared contributors | Shared heads | Low | Expected | High | Zero row |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (ChangePortfolioRepositorySummary repository in aggregation.Repositories)
        {
            markdown.Append("| ").Append(ReportFormatting.Escape(repository.RepositoryId)).Append(" | ")
                .Append(repository.SelectedCommitCount).Append(" | ")
                .Append(repository.DirectAuthorMatchCount).Append(" | ")
                .Append(repository.CoauthorMatchCount).Append(" | ")
                .Append(repository.SharedContributorSelectedCommitCount).Append(" | ")
                .Append(repository.SharedHeadSelectedCommitCount).Append(" | ")
                .Append(ReportFormatting.Hours(repository.NormalizedEffort.Low)).Append(" | ")
                .Append(ReportFormatting.Hours(repository.NormalizedEffort.Expected)).Append(" | ")
                .Append(ReportFormatting.Hours(repository.NormalizedEffort.High)).Append(" | ")
                .Append(repository.NoSelectedCommits ? "yes" : "no").AppendLine(" |");
        }

        markdown.AppendLine();
        markdown.AppendLine("| Repository/head | Reachable commits | Unique commits | Shared commits | No unique work |");
        markdown.AppendLine("| --- | ---: | ---: | ---: | --- |");
        foreach (ChangePortfolioRepositorySummary repository in aggregation.Repositories)
        {
            foreach (ChangePortfolioHeadSummary head in repository.Heads)
            {
                markdown.Append("| `").Append(ReportFormatting.Escape(repository.RepositoryId)).Append('/')
                    .Append(ReportFormatting.Escape(head.HeadId)).Append("` | ")
                    .Append(head.ReachableSelectedCommitCount).Append(" | ")
                    .Append(head.UniqueSelectedCommitCount).Append(" | ")
                    .Append(head.SharedSelectedCommitCount).Append(" | ")
                    .Append(head.NoUniqueSelectedCommits ? "yes" : "no").AppendLine(" |");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine("## Head-reachability allocation");
        markdown.AppendLine();
        markdown.AppendLine("| Repository | Heads | Kind | Commits | Low | Expected | High | Expected delta | Adjustments | Uncertainty |");
        markdown.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (ChangePortfolioRepositorySummary repository in aggregation.Repositories)
        {
            foreach (ChangePortfolioHeadGroup group in repository.HeadGroups)
            {
                markdown.Append("| ").Append(ReportFormatting.Escape(repository.RepositoryId)).Append(" | ")
                    .Append(ReportFormatting.Escape(string.Join(", ", group.HeadIds))).Append(" | ")
                    .Append(ReportFormatting.Kebab(group.Kind)).Append(" | ")
                    .Append(group.SelectedCommitCount).Append(" | ")
                    .Append(ReportFormatting.Hours(group.NormalizedEffort.Low)).Append(" | ")
                    .Append(ReportFormatting.Hours(group.NormalizedEffort.Expected)).Append(" | ")
                    .Append(ReportFormatting.Hours(group.NormalizedEffort.High)).Append(" | ")
                    .Append(ReportFormatting.Hours(group.ReconciliationDelta.Expected)).Append(" | ")
                    .Append(group.AdjustmentIds.Count).Append(" | ")
                    .Append(group.UncertaintyReasons.Count).AppendLine(" |");
            }
        }

        markdown.AppendLine();
        markdown.AppendLine(
            "Head groups sum exactly within each repository. Contributor groups and head groups are alternative views of the same normalized EHE and must not be added together.");
    }
}
