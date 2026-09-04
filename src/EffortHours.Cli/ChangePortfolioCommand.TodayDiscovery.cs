using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<TodayDiscoveryOutcome> DiscoverTodayAsync(
        ChangePortfolioCommandOptions options,
        string title,
        DateTimeOffset generatedAt,
        long workflowStarted,
        ChangePortfolioExecutionTelemetry portfolioTelemetry,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!options.Today && !options.IsNativePeriod)
        {
            return new TodayDiscoveryOutcome(null, null);
        }

        EngineeringScopeProfile? engineeringScope = null;
        try
        {
            using (portfolioTelemetry.Measure(ChangePortfolioExecutionPhases.ScopeLoading))
            {
                engineeringScope = EngineeringScopeProfile.Load();
            }

            ChangePortfolioNamedPeriodRange? period = null;
            if (options.IsNativePeriod)
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
                period = ChangePortfolioNamedPeriodResolver.Resolve(
                    options.Period!.Value,
                    generatedAt,
                    zone);
            }

            GitHubContributorSampleRequest? sample = options.TeamComparison
                ? new GitHubContributorSampleRequest
                {
                    ContributorsFrom = options.ContributorsFrom!,
                    SampleSize = options.SampleSize!.Value,
                    SampleSeed = options.SampleSeed!,
                    IncludedAuthors = options.IncludedAuthors,
                }
                : null;
            GitHubAuthorPeriodDiscoveryResult today = await _discoverToday(
                new GitHubAuthorPeriodDiscoveryRequest
                {
                    Owner = options.Owner!,
                    AuthorAliases = options.TeamComparison ? [] : options.AuthorAliases,
                    ContributorId = options.NativePeriod ? "contributor" : "me",
                    ContributorSample = sample,
                    AsOf = generatedAt,
                    SinceInclusive = period?.SinceInclusive,
                    UntilExclusive = period?.UntilExclusive,
                    TimeZone = options.TimeZone,
                    Scope = options.Scope!,
                    IncludeOpenPullRequests = options.IncludeOpenPullRequests,
                    DateField = options.DateField,
                    MergePolicy = options.MergePolicy,
                    CoauthorPolicy = options.CoauthorPolicy,
                    ExecutionTelemetry = portfolioTelemetry,
                    EngineeringScope = engineeringScope,
                },
                cancellationToken).ConfigureAwait(false);
            return new TodayDiscoveryOutcome(today, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
                InvalidOperationException or IOException or UnauthorizedAccessException or
                System.Text.Json.JsonException)
        {
            int exitCode = await WriteTodaySetupFailureAsync(
                options,
                title,
                generatedAt,
                workflowStarted,
                engineeringScope,
                portfolioTelemetry,
                exception,
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false);
            return new TodayDiscoveryOutcome(null, exitCode);
        }
    }

    private sealed record TodayDiscoveryOutcome(
        GitHubAuthorPeriodDiscoveryResult? Today,
        int? ExitCode);

    private static int AdmittedChangeCount(
        IReadOnlyList<ChangePortfolioCandidate> candidates) => candidates.Count(candidate =>
            candidate.Report.Evidence.Paths.Count > 0);

    private static ChangePortfolioScopeSummary ScopeSummary(
        IReadOnlyList<ChangePortfolioRepositoryOutcome> outcomes) => new()
        {
            IdentitySelectedCommitCount = outcomes.Sum(outcome =>
                outcome.Scope?.SelectedChangeCount ?? outcome.Candidates.Count),
            AdmittedCommitCount = outcomes.Sum(outcome => outcome.AdmittedChangeCount),
            ScopeEmptyCommitCount = outcomes.Sum(outcome => outcome.ScopeEmptyChangeCount),
        };
}
