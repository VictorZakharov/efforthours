namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    internal Task<int> ExecuteCalendarAsync(ChangePortfolioCommandOptions options,
        ResolvedChangeAuthorPeriodManifest manifest, TextWriter stdout, TextWriter stderr, CancellationToken token) =>
        ExecuteComparisonAsync(options, null, stdout, stderr, token, manifest);
}
