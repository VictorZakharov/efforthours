using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    // Everything outside a commit's own plan that can change its estimate: the
    // exact build (dev builds can change analysis without a version bump), the
    // repository identity that names reports and selects path admission, and the
    // admission policy itself. Selection windows, contributors, and heads are
    // deliberately absent, so moving a window still reuses unchanged commits.
    private static string ResultCacheContext(
        ChangeAuthorPeriodManifestRepository repository,
        ChangePathAdmission? pathAdmission,
        string? scopeProfileDigest,
        ChangePortfolioCommandOptions options) =>
        ChangePortfolioComparisonIdentity.ComputeTextDigest(string.Join('\n',
            ChangePortfolioResultCacheKeys.Protocol,
            CliVersion(),
            repository.Id,
            repository.ScopeRepository ?? "",
            repository.GitHubRepository ?? "",
            scopeProfileDigest ?? "",
            pathAdmission?.ProfileDigest ?? "",
            options.Scope ?? ""));

    private static async Task TrimResultCacheAsync(
        ChangePortfolioResultCacheStore? results,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (results is null)
            return;
        string retained;
        try
        {
            ChangePortfolioResultCacheTrim trim = results.Trim(cancellationToken);
            retained = $"entries={trim.Entries}, bytes={trim.Bytes}, evicted={trim.Evicted}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            retained = "trim=failed";
        }

        await standardError.WriteLineAsync(
            $"eh: portfolio result-cache hits={results.Hits}, misses={results.Misses}, " +
            $"writes={results.Writes}, write-failures={results.WriteFailures}, " +
            $"rejected={results.Rejected}; {retained}").ConfigureAwait(false);
    }
}
