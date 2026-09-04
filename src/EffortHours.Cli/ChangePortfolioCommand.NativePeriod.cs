using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private static string DefaultComparisonTitle(ChangePortfolioCommandOptions options) =>
        options.Today
            ? "EffortHours today-to-date capacity"
            : options.TeamComparison
                ? "EffortHours team period comparison"
                : options.NativePeriod
                    ? "EffortHours contributor period report"
                    : options.ComparisonView == ChangePortfolioComparisonView.Findings
                        ? "EffortHours engineering findings"
                        : "EffortHours portfolio trend";

    private static ChangePortfolioNativePeriod? CreateNativePeriodMetadata(
        ChangePortfolioCommandOptions options,
        GitHubAuthorPeriodDiscoveryResult? discovery) =>
        discovery is null || !options.IsNativePeriod
            ? null
            : new ChangePortfolioNativePeriod
            {
                Kind = options.Period!.Value,
                Breakdown = options.Breakdown,
                CapacityHoursPerDay = options.CapacityHoursPerDay!.Value,
                ContributorSelection = discovery.ContributorSelection,
            };
}
