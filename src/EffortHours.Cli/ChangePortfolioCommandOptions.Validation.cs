using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static partial class ChangePortfolioCommandOptionsParser
{
    private static ChangePortfolioCommandParseResult Validate(
        ChangePortfolioCommandOptions options,
        string? since,
        string? until,
        bool authorPolicyProvided,
        bool timeZoneProvided,
        bool normalizationProvided,
        bool currencyProvided)
    {
        int selectors = (options.PullRequests.Count > 0 ? 1 : 0) +
            (options.ManifestPath is null ? 0 : 1) +
            (options.AuthorPeriodManifestPath is null ? 0 : 1) +
            (options.AuthorAliases.Count > 0 && !options.Today && !options.IsNativePeriod ? 1 : 0) +
            (options.Today ? 1 : 0) +
            (options.IsNativePeriod ? 1 : 0);
        if (selectors != 1)
        {
            return Error(
                "Select exactly one repeated --pr set, one --manifest, one " +
                "--author-period-manifest, one direct author-period selector, one --today selector, " +
                "or one native named-period selector.");
        }

        if (options.PullRequests.Count > 128)
        {
            return Error("A pull-request portfolio cannot contain more than 128 selected changes.");
        }

        if (options.AuthorAliases.Count > 128)
        {
            return Error("An author-period selector cannot contain more than 128 aliases.");
        }

        bool authorSelection = options.AuthorAliases.Count > 0;
        bool providerPeriod = options.Today || options.IsNativePeriod;
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
        if (options.Preflight && !options.IsAuthorPeriodManifest)
        {
            return Error("Option --preflight requires --author-period-manifest.");
        }
        if ((options.ManifestPath is not null || options.AuthorPeriodManifestPath is not null || providerPeriod) &&
            (options.RepositoryPath is not null || options.GitHubRepository is not null))
        {
            return Error(
                "A manifest supplies its repository paths; omit positional repository and --repo values.");
        }

        if (authorSelection && !providerPeriod && options.RepositoryPath is null &&
            options.GitHubRepository is null)
        {
            return Error(
                "Checkout-free direct author-period selection requires --repo <owner/name>; " +
                "otherwise supply a positional repository path.");
        }

        if (options.PullRequests.Count > 0 && options.RepositoryPath is null &&
            options.GitHubRepository is null &&
            options.PullRequests.Any(input => !IsAbsolutePullRequestUrl(input)))
        {
            return Error(
                "Checkout-free repeated PR numbers require --repo <owner/name>; " +
                "otherwise every --pr must be a full GitHub pull-request URL.");
        }

        if (options.GitHubRepository is not null && options.PullRequests.Count == 0 &&
            !authorSelection)
        {
            return Error("Option --repo is valid only with repeated --pr or direct --author selectors.");
        }

        if (options.FetchMissing && options.PullRequests.Count == 0 &&
            options.ManifestPath is null &&
            !(options.IsAuthorPeriodManifest && !options.Preflight) &&
            !(authorSelection && options.RepositoryPath is null && options.GitHubRepository is not null))
        {
            return Error(
                "Option --fetch-missing is valid only with checkout-free provider selectors, " +
                "repeated --pr, --manifest, or a non-preflight author-period manifest.");
        }

        bool authorOnlyOptions = since is not null || until is not null || authorPolicyProvided;
        if (!authorSelection && !providerPeriod && authorOnlyOptions)
        {
            return Error("Time, identity-policy, and --head options are valid only with --author.");
        }

        if (providerPeriod && (since is not null || until is not null))
        {
            return Error(
                "Provider-assisted period commands determine their own interval; omit --since and --until.");
        }

        ChangePortfolioCommandParseResult? authorError = authorSelection && !providerPeriod
            ? ParseAuthorPeriod(options, since, until, out options)
            : null;
        if (authorError is not null)
        {
            return authorError.Value;
        }

        if (options.Compact && options.Format != "json")
        {
            return Error("Option --compact can only be used with JSON output.");
        }

        if (options.Bucket is not null && options.BucketManifestPath is not null)
        {
            return Error("Use either --bucket or --bucket-manifest, not both.");
        }

        bool comparisonOption = options.IsComparison ||
            options.CapacityManifestPath is not null ||
            options.ComparisonView != ChangePortfolioComparisonView.Trend ||
            options.GeneratedAt is not null ||
            options.ReportTitle is not null ||
            options.CheckpointPath is not null ||
            options.NoCheckpoint ||
            normalizationProvided;
        if (options.Preflight && comparisonOption)
        {
            return Error(
                "Author-period preflight measures the manifest selection only; omit bucket, " +
                "capacity, checkpoint, title, generated-time, report-view, and normalization options.");
        }
        if (options.Preflight && (options.HourlyRate is not null || currencyProvided))
        {
            return Error(
                "Author-period preflight does not estimate EHE or pricing; omit hourly-rate and currency options.");
        }
        if (comparisonOption && !options.IsAuthorPeriodManifest && !providerPeriod)
        {
            return Error(
                "Time-bucketed comparison options require --author-period-manifest.");
        }

        if (!options.IsComparison && comparisonOption)
        {
            return Error("Select --bucket or --bucket-manifest for comparison output.");
        }

        if (options.IsComparison && !providerPeriod && options.OutputPath is null)
        {
            return Error("Time-bucketed comparison output requires an explicit --output path.");
        }

        if (options.NoCheckpoint && options.CheckpointPath is not null)
        {
            return Error("Option --no-checkpoint cannot be combined with --checkpoint.");
        }

        if (options.IsNativePeriod &&
            normalizationProvided &&
            options.ContributorNormalization != ChangePortfolioContributorNormalization.Isolated)
        {
            return Error("Native named-period reports require --normalization isolated.");
        }

        if (options.ReportTitle is { Length: > ChangePortfolioComparisonLimits.MaximumTitleLength } ||
            string.IsNullOrWhiteSpace(options.ReportTitle) && options.ReportTitle is not null)
        {
            return Error(
                $"Report title must contain 1 to " +
                $"{ChangePortfolioComparisonLimits.MaximumTitleLength} characters.");
        }

        if (options.NoRate && (options.HourlyRate is not null || currencyProvided))
        {
            return Error("Option --no-rate cannot be combined with --hourly-rate or --currency.");
        }

        if (currencyProvided && options.HourlyRate is null)
        {
            return Error("Option --currency requires a caller-supplied --hourly-rate.");
        }

        if (options.IsNativePeriod)
        {
            options = options with
            {
                ContributorNormalization = ChangePortfolioContributorNormalization.Isolated,
            };
        }

        return new ChangePortfolioCommandParseResult(options, null);
    }

    private static bool IsAbsolutePullRequestUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);

    private static ChangePortfolioCommandParseResult? ParseAuthorPeriod(
        ChangePortfolioCommandOptions source,
        string? since,
        string? until,
        out ChangePortfolioCommandOptions result)
    {
        result = source;
        if (since is null || until is null)
        {
            return Error("Author-period selection requires --since and --until.");
        }

        if (!ChangePortfolioTimeParser.TryResolveTimeZone(
            source.TimeZone,
            out TimeZoneInfo zone,
            out string? zoneError))
        {
            return Error(zoneError!);
        }

        if (!ChangePortfolioTimeParser.TryParse(
            since,
            zone,
            out DateTimeOffset parsedSince,
            out string? sinceError))
        {
            return Error(sinceError!);
        }

        if (!ChangePortfolioTimeParser.TryParse(
            until,
            zone,
            out DateTimeOffset parsedUntil,
            out string? untilError))
        {
            return Error(untilError!);
        }

        if (parsedSince >= parsedUntil)
        {
            return Error("The inclusive --since instant must be earlier than the exclusive --until instant.");
        }

        result = source with
        {
            SinceInclusive = parsedSince,
            UntilExclusive = parsedUntil,
            TimeZone = zone.Id,
        };
        return null;
    }
}
