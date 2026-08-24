using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static partial class ChangePortfolioCommandOptionsParser
{
    public static ChangePortfolioCommandParseResult Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? repositoryPath = arguments.Length > 0 && !arguments[0].StartsWith('-')
            ? arguments[0]
            : null;
        int firstOption = repositoryPath is null ? 0 : 1;
        List<string> pullRequests = [];
        List<string> authors = [];
        string? githubRepository = null;
        string? manifest = null;
        string? authorPeriodManifest = null;
        string? owner = null;
        string? workspace = null;
        string? scope = null;
        bool today = false;
        bool includeOpenPullRequests = false;
        decimal? capacityHours = null;
        string? bucket = null;
        string? bucketManifest = null;
        string? capacityManifest = null;
        ChangePortfolioComparisonView comparisonView = ChangePortfolioComparisonView.Trend;
        ChangePortfolioContributorNormalization contributorNormalization =
            ChangePortfolioContributorNormalization.Joint;
        DateTimeOffset? generatedAt = null;
        string? reportTitle = null;
        string? checkpointPath = null;
        bool noCheckpoint = false;
        bool preflight = false;
        string? since = null;
        string? until = null;
        string timeZone = "UTC";
        ChangePortfolioDateField dateField = ChangePortfolioDateField.Author;
        ChangePortfolioMergePolicy mergePolicy = ChangePortfolioMergePolicy.Exclude;
        ChangePortfolioCoauthorPolicy coauthorPolicy = ChangePortfolioCoauthorPolicy.Include;
        string head = "HEAD";
        EstimationProfile profile = EstimationProfile.Implementation;
        string format = "json";
        bool compact = false;
        bool noRate = false;
        bool fetchMissing = false;
        decimal? hourlyRate = null;
        string currency = "USD";
        bool currencyProvided = false;
        bool authorPolicyProvided = false;
        bool timeZoneProvided = false;
        bool normalizationProvided = false;
        string? output = null;
        for (int index = firstOption; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (option is "help" or "--help" or "-h")
            {
                return new ChangePortfolioCommandParseResult(null, null, ShowHelp: true);
            }

            if (option == "--compact")
            {
                compact = true;
                continue;
            }

            if (option == "--no-rate")
            {
                noRate = true;
                continue;
            }

            if (option == "--fetch-missing")
            {
                fetchMissing = true;
                continue;
            }

            if (option == "--no-checkpoint")
            {
                noCheckpoint = true;
                continue;
            }

            if (option == "--preflight")
            {
                preflight = true;
                continue;
            }

            if (option == "--today")
            {
                today = true;
                continue;
            }

            if (option == "--include-open-prs")
            {
                includeOpenPullRequests = true;
                continue;
            }

            if (option == "--author" && today &&
                (index + 1 >= arguments.Length || arguments[index + 1].StartsWith('-')))
            {
                authors.Add("@me");
                continue;
            }

            if (index + 1 >= arguments.Length)
            {
                return Error($"Option '{option}' requires a value.");
            }

            string value = arguments[++index];
            switch (option)
            {
                case "--pr":
                    pullRequests.Add(value);
                    break;
                case "--repo":
                    githubRepository = value;
                    break;
                case "--manifest":
                    manifest = value;
                    break;
                case "--author-period-manifest":
                    authorPeriodManifest = value;
                    break;
                case "--owner":
                    owner = value;
                    break;
                case "--workspace":
                    workspace = value;
                    break;
                case "--scope":
                    scope = value.ToLowerInvariant();
                    break;
                case "--capacity-hours":
                    if (!decimal.TryParse(
                            value,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out decimal parsedCapacity) ||
                        parsedCapacity <= 0m)
                    {
                        return Error(
                            "Capacity hours must be a positive decimal using '.' as the decimal separator.");
                    }

                    capacityHours = parsedCapacity;
                    break;
                case "--bucket":
                    bucket = value.ToLowerInvariant();
                    if (bucket is not ("calendar-month" or "calendar-week"))
                    {
                        return Error("Bucket must be 'calendar-month' or 'calendar-week'.");
                    }

                    break;
                case "--bucket-manifest":
                    bucketManifest = value;
                    break;
                case "--capacity-manifest":
                    capacityManifest = value;
                    break;
                case "--report-view":
                    string view = value.ToLowerInvariant();
                    if (view is not ("trend" or "findings"))
                    {
                        return Error("Report view must be 'trend' or 'findings'.");
                    }

                    comparisonView = view == "trend"
                        ? ChangePortfolioComparisonView.Trend
                        : ChangePortfolioComparisonView.Findings;
                    break;
                case "--normalization":
                    if (!EnumValue(value, "joint", "isolated", out string normalizationValue))
                    {
                        return Error("Normalization must be 'joint' or 'isolated'.");
                    }

                    contributorNormalization = normalizationValue == "joint"
                        ? ChangePortfolioContributorNormalization.Joint
                        : ChangePortfolioContributorNormalization.Isolated;
                    normalizationProvided = true;
                    break;
                case "--generated-at":
                    if (!DateTimeOffset.TryParse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out DateTimeOffset parsedGeneratedAt))
                    {
                        return Error("Generated timestamp must be a valid ISO-8601 instant.");
                    }

                    generatedAt = parsedGeneratedAt.ToUniversalTime();
                    break;
                case "--title":
                    reportTitle = value;
                    break;
                case "--checkpoint":
                    checkpointPath = value;
                    break;
                case "--author":
                    authors.Add(value);
                    break;
                case "--since":
                    since = value;
                    break;
                case "--until":
                    until = value;
                    break;
                case "--timezone":
                    timeZone = value;
                    authorPolicyProvided = true;
                    timeZoneProvided = true;
                    break;
                case "--date-field":
                    if (!EnumValue(value, "author", "committer", out string dateValue))
                    {
                        return Error("Date field must be 'author' or 'committer'.");
                    }

                    dateField = dateValue == "author"
                        ? ChangePortfolioDateField.Author
                        : ChangePortfolioDateField.Committer;
                    authorPolicyProvided = true;
                    break;
                case "--merge-policy":
                    if (!EnumValue(value, "exclude", "first-parent", out string mergeValue))
                    {
                        return Error("Merge policy must be 'exclude' or 'first-parent'.");
                    }

                    mergePolicy = mergeValue == "exclude"
                        ? ChangePortfolioMergePolicy.Exclude
                        : ChangePortfolioMergePolicy.FirstParent;
                    authorPolicyProvided = true;
                    break;
                case "--coauthors":
                    if (!EnumValue(value, "include", "exclude", out string coauthorValue))
                    {
                        return Error("Co-author policy must be 'include' or 'exclude'.");
                    }

                    coauthorPolicy = coauthorValue == "include"
                        ? ChangePortfolioCoauthorPolicy.Include
                        : ChangePortfolioCoauthorPolicy.Exclude;
                    authorPolicyProvided = true;
                    break;
                case "--head":
                    head = value;
                    authorPolicyProvided = true;
                    break;
                case "--profile":
                    if (!EnumValue(value, "implementation", "recreation", out string profileValue))
                    {
                        return Error("Profile must be 'implementation' or 'recreation'.");
                    }

                    profile = profileValue == "implementation"
                        ? EstimationProfile.Implementation
                        : EstimationProfile.Recreation;
                    break;
                case "--format":
                    format = value.ToLowerInvariant();
                    if (format is not ("json" or "markdown"))
                    {
                        return Error("Format must be 'json' or 'markdown'.");
                    }

                    break;
                case "--hourly-rate":
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rate) ||
                        rate < 0m)
                    {
                        return Error("Hourly rate must be a non-negative decimal using '.' as the decimal separator.");
                    }

                    hourlyRate = rate;
                    break;
                case "--currency":
                    currency = value.ToUpperInvariant();
                    currencyProvided = true;
                    if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
                    {
                        return Error("Currency must be a three-letter uppercase currency code.");
                    }

                    break;
                case "--output":
                    output = value;
                    break;
                default:
                    return Error($"Unknown change portfolio option '{option}'.");
            }
        }

        ChangePortfolioCommandOptions options = new()
        {
            RepositoryPath = repositoryPath,
            PullRequests = pullRequests,
            GitHubRepository = githubRepository,
            FetchMissing = fetchMissing,
            ManifestPath = manifest,
            AuthorPeriodManifestPath = authorPeriodManifest,
            Owner = owner,
            WorkspacePath = workspace,
            Scope = scope,
            Today = today,
            IncludeOpenPullRequests = includeOpenPullRequests,
            CapacityHours = capacityHours,
            Preflight = preflight,
            Bucket = bucket,
            BucketManifestPath = bucketManifest,
            CapacityManifestPath = capacityManifest,
            ComparisonView = comparisonView,
            ContributorNormalization = contributorNormalization,
            GeneratedAt = generatedAt,
            ReportTitle = reportTitle,
            CheckpointPath = checkpointPath,
            NoCheckpoint = noCheckpoint,
            AuthorAliases = authors,
            TimeZone = timeZone,
            DateField = dateField,
            MergePolicy = mergePolicy,
            CoauthorPolicy = coauthorPolicy,
            HeadRevision = head,
            Profile = profile,
            Format = format,
            Compact = compact,
            NoRate = noRate,
            HourlyRate = hourlyRate,
            Currency = currency,
            OutputPath = output,
        };
        return Validate(
            options,
            since,
            until,
            authorPolicyProvided,
            timeZoneProvided,
            normalizationProvided,
            currencyProvided);
    }

    private static bool EnumValue(string value, string first, string second, out string parsed)
    {
        parsed = value.ToLowerInvariant();
        return parsed == first || parsed == second;
    }

    private static ChangePortfolioCommandParseResult Error(string message) => new(null, message);
}
