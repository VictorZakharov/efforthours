namespace EffortHours.Cli;

internal static partial class ChangePortfolioCommandOptionsParser
{
    private static ChangePortfolioCommandParseResult Validate(
        ChangePortfolioCommandOptions options,
        string? since,
        string? until,
        bool authorPolicyProvided,
        bool currencyProvided)
    {
        int selectors = (options.PullRequests.Count > 0 ? 1 : 0) +
            (options.ManifestPath is null ? 0 : 1) +
            (options.AuthorPeriodManifestPath is null ? 0 : 1) +
            (options.AuthorAliases.Count > 0 ? 1 : 0);
        if (selectors != 1)
        {
            return Error(
                "Select exactly one repeated --pr set, one --manifest, one " +
                "--author-period-manifest, or one direct author-period selector.");
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
        if ((options.ManifestPath is not null || options.AuthorPeriodManifestPath is not null) &&
            (options.RepositoryPath is not null || options.GitHubRepository is not null))
        {
            return Error(
                "A manifest supplies its repository paths; omit positional repository and --repo values.");
        }

        if ((options.PullRequests.Count > 0 || authorSelection) && options.RepositoryPath is null)
        {
            return Error("A repository path is required for repeated PR and author-period selectors.");
        }

        if (options.GitHubRepository is not null && options.PullRequests.Count == 0)
        {
            return Error("Option --repo is valid only with repeated --pr selectors.");
        }

        bool authorOnlyOptions = since is not null || until is not null || authorPolicyProvided;
        if (!authorSelection && authorOnlyOptions)
        {
            return Error("Time, identity-policy, and --head options are valid only with --author.");
        }

        ChangePortfolioCommandParseResult? authorError = authorSelection
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
