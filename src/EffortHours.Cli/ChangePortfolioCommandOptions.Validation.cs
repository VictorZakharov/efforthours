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
            (options.AuthorAliases.Count > 0 && !options.Today ? 1 : 0) +
            (options.Today ? 1 : 0);
        if (selectors != 1)
        {
            return Error(
                "Select exactly one repeated --pr set, one --manifest, one " +
                "--author-period-manifest, one direct author-period selector, or one --today selector.");
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
        if (options.Today && options.AuthorAliases.Count == 0)
        {
            return Error(
                "Today-to-date discovery requires at least one --author value, such as --author \"@me\".");
        }

        if (options.Today &&
            (string.IsNullOrWhiteSpace(options.Owner) || string.IsNullOrWhiteSpace(options.WorkspacePath)))
        {
            return Error("Today-to-date discovery requires --owner and --workspace.");
        }

        if (options.Today && options.CapacityHours is null)
        {
            return Error("Today-to-date capacity comparison requires --capacity-hours.");
        }

        if (options.Today && !timeZoneProvided)
        {
            return Error("Today-to-date discovery requires an explicit named --timezone.");
        }

        if (options.Today &&
            (options.Bucket is not null || options.BucketManifestPath is not null ||
             options.CapacityManifestPath is not null))
        {
            return Error(
                "Option --today creates its daily bucket and inline capacity; omit bucket and capacity manifests.");
        }

        if (options.Today && options.HeadRevision != "HEAD")
        {
            return Error("Today-to-date discovery resolves provider heads; omit --head.");
        }

        if (!options.Today &&
            (options.Owner is not null || options.WorkspacePath is not null ||
             options.IncludeOpenPullRequests || options.CapacityHours is not null))
        {
            return Error(
                "Options --owner, --workspace, --include-open-prs, and --capacity-hours require --today.");
        }
        if (options.Preflight && !options.IsAuthorPeriodManifest)
        {
            return Error("Option --preflight requires --author-period-manifest.");
        }
        if ((options.ManifestPath is not null || options.AuthorPeriodManifestPath is not null || options.Today) &&
            (options.RepositoryPath is not null || options.GitHubRepository is not null))
        {
            return Error(
                "A manifest supplies its repository paths; omit positional repository and --repo values.");
        }

        if ((options.PullRequests.Count > 0 || authorSelection && !options.Today) &&
            options.RepositoryPath is null)
        {
            return Error("A repository path is required for repeated PR and author-period selectors.");
        }

        if (options.GitHubRepository is not null && options.PullRequests.Count == 0)
        {
            return Error("Option --repo is valid only with repeated --pr selectors.");
        }

        if (options.FetchMissing && options.PullRequests.Count == 0 &&
            options.ManifestPath is null && !options.Today)
        {
            return Error(
                "Option --fetch-missing is valid only with repeated --pr or --manifest selectors, " +
                "or with --today discovery.");
        }

        bool authorOnlyOptions = since is not null || until is not null || authorPolicyProvided;
        if (!authorSelection && authorOnlyOptions)
        {
            return Error("Time, identity-policy, and --head options are valid only with --author.");
        }

        if (options.Today && (since is not null || until is not null))
        {
            return Error("Option --today determines its own local-day interval; omit --since and --until.");
        }

        ChangePortfolioCommandParseResult? authorError = authorSelection && !options.Today
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
        if (comparisonOption && !options.IsAuthorPeriodManifest && !options.Today)
        {
            return Error(
                "Time-bucketed comparison options require --author-period-manifest.");
        }

        if (!options.IsComparison && comparisonOption)
        {
            return Error("Select --bucket or --bucket-manifest for comparison output.");
        }

        if (options.IsComparison && !options.Today && options.OutputPath is null)
        {
            return Error("Time-bucketed comparison output requires an explicit --output path.");
        }

        if (options.NoCheckpoint && options.CheckpointPath is not null)
        {
            return Error("Option --no-checkpoint cannot be combined with --checkpoint.");
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

        return new ChangePortfolioCommandParseResult(options, null);
    }

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
