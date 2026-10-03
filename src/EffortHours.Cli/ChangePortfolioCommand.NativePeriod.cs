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
                RetainedHistory = options.IncludeHistoricalPullRequests ? true : null,
            };

    private static ChangePortfolioNamedPeriodRange ResolveProviderPeriod(
        ChangePortfolioCommandOptions options,
        DateTimeOffset asOf,
        TimeZoneInfo zone)
    {
        if (options.Period != ChangePortfolioNativePeriodKind.CustomRange)
        {
            return ChangePortfolioNamedPeriodResolver.Resolve(options.Period!.Value, asOf, zone);
        }

        if (options.UntilExclusive > asOf)
        {
            throw new ArgumentException("Historical --until must be no later than the frozen report instant.");
        }

        return new ChangePortfolioNamedPeriodRange(ChangePortfolioNativePeriodKind.CustomRange,
            options.SinceInclusive!.Value, options.UntilExclusive!.Value);
    }
}
