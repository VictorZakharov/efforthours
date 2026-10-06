using System.Globalization;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

internal static class ChangePortfolioReplayMarkdownRenderer
{
    public static void Append(StringBuilder markdown, ChangePortfolioReport? report)
    {
        if (report?.ReplayAllocations is not { Count: > 0 } allocations) return;
        markdown.AppendLine();
        markdown.AppendLine("## Declared replay range allocation");
        markdown.AppendLine();
        markdown.AppendLine("Standalone comparisons are non-additive. Event allocation conserves the jointly deduplicated budget; replay/date declarations do not establish actual labor or historical causation. Missing evidence remains unresolved.");
        markdown.AppendLine();
        markdown.AppendLine("| Repository / event | Event UTC | Confidence | Status | Joint budget | Original reservation | Standalone novel | Event allocation | Capped |");
        markdown.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- |");
        foreach (ChangePortfolioReplayAllocation allocation in allocations)
        {
            ChangePortfolioReplayEvidence evidence = allocation.Evidence;
            markdown.Append("| ").Append(evidence.Review.RepositoryId).Append(" / ").Append(evidence.Event.Id)
                .Append(" | ").Append(evidence.Event.EventTimestamp?.ToString("O", CultureInfo.InvariantCulture) ?? "unresolved")
                .Append(" | ").Append(evidence.Review.ReplayConfidence).Append(" | ").Append(allocation.Status)
                .Append(" | ").Append(Hours(allocation.AvailableJointExpectedHours)).Append(" | ").Append(Hours(allocation.ReservedOriginalExpectedHours)).Append(" | ").Append(Hours(allocation.StandaloneNovelExpectedHours))
                .Append(" | ").Append(Hours(allocation.AllocatedEventExpectedHours)).Append(" | ").Append(allocation.AllocationCapped ? "yes" : "no").AppendLine(" |");
        }
    }

    private static string Hours(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "unresolved";
}
