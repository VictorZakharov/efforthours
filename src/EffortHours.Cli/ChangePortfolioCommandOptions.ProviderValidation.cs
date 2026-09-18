using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static partial class ChangePortfolioCommandOptionsParser
{
    private static ChangePortfolioCommandParseResult? ValidateProviderOptions(
        ref ChangePortfolioCommandOptions options,
        bool timeZoneProvided)
    {
        bool providerPeriod = options.Today || options.IsNativePeriod;
        if (options.ProviderLogin is { } providerLogin &&
            (!providerPeriod || options.TeamComparison || !options.IncludeOpenPullRequests ||
             !providerLogin.Equals("@me", StringComparison.OrdinalIgnoreCase) && (providerLogin.Length is < 1 or > 39 ||
             providerLogin.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))))
        {
            return Error("--provider-login requires today or a single-contributor period, " +
                "--include-open-prs, and a GitHub login or @me.");
        }

        if (options.NativePeriod && options.TeamComparison)
        {
            return Error("Select either a single-contributor period report or a team comparison.");
        }

        if (options.Today &&
            (options.NativeOptionsProvided || options.Period is not null ||
             options.CapacityHoursPerDay is not null ||
             options.ContributorsFrom is not null || options.SampleSize is not null ||
             options.SampleSeed is not null || options.IncludedAuthors.Count > 0))
        {
            return Error("Today-to-date does not accept named-period or team-sampling options.");
        }

        if (options.IsNativePeriod && options.CapacityHours is not null)
        {
            return Error("Named-period reports use --capacity-hours-per-day; omit --capacity-hours.");
        }

        if (options.Today && options.AuthorAliases.Count == 0)
        {
            return Error(
                "Today-to-date discovery requires at least one --author value, such as --author \"@me\".");
        }

        if (options.NativePeriod && options.AuthorAliases.Count == 0)
        {
            return Error(
                "A single-contributor period report requires at least one --author value.");
        }

        if (options.TeamComparison && options.AuthorAliases.Count > 0)
        {
            return Error(
                "Team comparison uses --include-author for explicit contributors; omit --author.");
        }

        if (providerPeriod && string.IsNullOrWhiteSpace(options.Owner))
        {
            return Error("Provider-assisted period discovery requires --owner.");
        }

        if (providerPeriod && options.Scope != "engineering")
        {
            return Error("Provider-assisted period discovery requires --scope engineering.");
        }

        if (providerPeriod && options.WorkspacePath is not null)
        {
            return Error(
                "Provider-assisted period discovery uses the EffortHours-managed repository cache; omit --workspace.");
        }

        if (providerPeriod && options.FetchMissing)
        {
            return Error(
                "Provider-assisted period discovery acquires required immutable objects automatically; omit --fetch-missing.");
        }

        if (options.Today && options.CapacityHours is null)
        {
            return Error("Today-to-date capacity comparison requires --capacity-hours.");
        }

        if (options.IsNativePeriod && options.CapacityHoursPerDay is null)
        {
            return Error("Named-period reports require --capacity-hours-per-day.");
        }

        if (options.IsNativePeriod && options.Period is null)
        {
            return Error("Named-period reports require --period.");
        }

        if (options.TeamComparison &&
            (string.IsNullOrWhiteSpace(options.ContributorsFrom) ||
             options.SampleSize is null ||
             string.IsNullOrWhiteSpace(options.SampleSeed)))
        {
            return Error(
                "Team comparison requires --contributors-from, --sample, and --sample-seed.");
        }

        if (options.SampleSeed is { Length: > 256 })
        {
            return Error("Sample seed cannot exceed 256 characters.");
        }

        if (options.IncludedAuthors.Distinct(StringComparer.OrdinalIgnoreCase).Count() +
            (options.SampleSize ?? 0) >
            ChangeAuthorPeriodManifestLimits.MaximumContributors)
        {
            return Error(
                $"Team comparison cannot select more than " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumContributors} contributors.");
        }

        if (options.NativePeriod &&
            (options.ContributorsFrom is not null || options.SampleSize is not null ||
             options.SampleSeed is not null || options.IncludedAuthors.Count > 0))
        {
            return Error(
                "Single-contributor period reports do not accept team sampling options.");
        }

        if (providerPeriod && !timeZoneProvided)
        {
            return Error("Provider-assisted period discovery requires an explicit named --timezone.");
        }

        if (providerPeriod)
        {
            if (!ChangePortfolioTimeParser.TryResolveTimeZone(
                    options.TimeZone,
                    out TimeZoneInfo zone,
                    out string? zoneError))
            {
                return Error(zoneError!);
            }

            options = options with { TimeZone = zone.Id };
        }

        if (providerPeriod &&
            (options.Bucket is not null || options.BucketManifestPath is not null ||
             options.CapacityManifestPath is not null))
        {
            return Error(
                "Provider-assisted period commands create their buckets and inline capacity; " +
                "omit bucket and capacity manifests.");
        }

        if (providerPeriod && options.HeadRevision != "HEAD")
        {
            return Error("Provider-assisted period discovery resolves provider heads; omit --head.");
        }

        if (!providerPeriod &&
            (options.Owner is not null || options.WorkspacePath is not null || options.Scope is not null ||
             options.IncludeOpenPullRequests || options.CapacityHours is not null ||
             options.NativeOptionsProvided ||
             options.CapacityHoursPerDay is not null || options.Period is not null ||
             options.ContributorsFrom is not null || options.SampleSize is not null ||
             options.SampleSeed is not null || options.IncludedAuthors.Count > 0))
        {
            return Error(
                "Provider discovery, named-period, inline-capacity, and team-sampling options " +
                "require change today, change period, or change compare-team.");
        }
        return null;
    }
}
