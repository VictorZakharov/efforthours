using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioCommandOptions
{
    public string? RepositoryPath { get; init; }

    public IReadOnlyList<string> PullRequests { get; init; } = [];

    public string? GitHubRepository { get; init; }

    public string? ManifestPath { get; init; }

    public IReadOnlyList<string> AuthorAliases { get; init; } = [];

    public DateTimeOffset? SinceInclusive { get; init; }

    public DateTimeOffset? UntilExclusive { get; init; }

    public string TimeZone { get; init; } = "UTC";

    public ChangePortfolioDateField DateField { get; init; } = ChangePortfolioDateField.Author;

    public ChangePortfolioMergePolicy MergePolicy { get; init; } = ChangePortfolioMergePolicy.Exclude;

    public ChangePortfolioCoauthorPolicy CoauthorPolicy { get; init; } =
        ChangePortfolioCoauthorPolicy.Include;

    public string HeadRevision { get; init; } = "HEAD";

    public EstimationProfile Profile { get; init; } = EstimationProfile.Implementation;

    public string Format { get; init; } = "json";

    public bool Compact { get; init; }

    public bool NoRate { get; init; }

    public decimal? HourlyRate { get; init; }

    public string Currency { get; init; } = "USD";

    public string? OutputPath { get; init; }

    public bool IsManifest => ManifestPath is not null;

    public bool IsAuthorPeriod => AuthorAliases.Count > 0;
}

internal readonly record struct ChangePortfolioCommandParseResult(
    ChangePortfolioCommandOptions? Options,
    string? Error,
    bool ShowHelp = false);

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
        decimal? hourlyRate = null;
        string currency = "USD";
        bool currencyProvided = false;
        bool authorPolicyProvided = false;
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
            ManifestPath = manifest,
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
        return Validate(options, since, until, authorPolicyProvided, currencyProvided);
    }

    private static bool EnumValue(string value, string first, string second, out string parsed)
    {
        parsed = value.ToLowerInvariant();
        return parsed == first || parsed == second;
    }

    private static ChangePortfolioCommandParseResult Error(string message) => new(null, message);
}
