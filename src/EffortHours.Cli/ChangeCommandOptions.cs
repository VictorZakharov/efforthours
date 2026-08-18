using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangeCommandOptions
{
    public string? RepositoryPath { get; init; }

    public string? BaseRevision { get; init; }

    public string? HeadRevision { get; init; }

    public string? Commit { get; init; }

    public string? Parent { get; init; }

    public string? Range { get; init; }

    public string? PullRequest { get; init; }

    public string? GitHubRepository { get; init; }

    public bool FetchMissing { get; init; }

    public string? BaseDirectory { get; init; }

    public string? HeadDirectory { get; init; }

    public string? BaseEvidencePath { get; init; }

    public string? HeadEvidencePath { get; init; }

    public EstimationProfile Profile { get; init; } = EstimationProfile.Implementation;

    public string Format { get; init; } = "json";

    public bool Compact { get; init; }

    public bool NoRate { get; init; }

    public decimal? HourlyRate { get; init; }

    public string Currency { get; init; } = "USD";

    public string? OutputPath { get; init; }

    public bool IsDirectorySelection => BaseDirectory is not null;

    public bool IsEvidenceSelection => BaseEvidencePath is not null;
}

internal readonly record struct ChangeCommandParseResult(
    ChangeCommandOptions? Options,
    string? Error,
    bool ShowHelp = false);

internal static class ChangeCommandOptionsParser
{
    public static ChangeCommandParseResult Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length == 0)
        {
            return Error("A change selector is required.");
        }

        string? repositoryPath = arguments[0].StartsWith('-')
            ? null
            : arguments[0];
        int firstOptionIndex = repositoryPath is null ? 0 : 1;

        string? baseRevision = null;
        string? headRevision = null;
        string? commit = null;
        string? parent = null;
        string? range = null;
        string? pullRequest = null;
        string? githubRepository = null;
        string? baseDirectory = null;
        string? headDirectory = null;
        string? baseEvidencePath = null;
        string? headEvidencePath = null;
        EstimationProfile profile = EstimationProfile.Implementation;
        string format = "json";
        bool compact = false;
        bool noRate = false;
        bool fetchMissing = false;
        decimal? hourlyRate = null;
        string currency = "USD";
        bool currencyProvided = false;
        string? outputPath = null;

        for (int index = firstOptionIndex; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                return new ChangeCommandParseResult(null, null, ShowHelp: true);
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

            if (index + 1 >= arguments.Length)
            {
                return Error($"Option '{option}' requires a value.");
            }

            string value = arguments[++index];
            switch (option)
            {
                case "--base":
                    baseRevision = value;
                    break;
                case "--head":
                    headRevision = value;
                    break;
                case "--commit":
                    commit = value;
                    break;
                case "--parent":
                    parent = value;
                    break;
                case "--range":
                    range = value;
                    break;
                case "--pr":
                    pullRequest = value;
                    break;
                case "--repo":
                    githubRepository = value;
                    break;
                case "--base-path":
                    baseDirectory = value;
                    break;
                case "--head-path":
                    headDirectory = value;
                    break;
                case "--base-evidence":
                    baseEvidencePath = value;
                    break;
                case "--head-evidence":
                    headEvidencePath = value;
                    break;
                case "--profile":
                    if (!TryParseProfile(value, out profile))
                    {
                        return Error("Profile must be 'implementation' or 'recreation'.");
                    }

                    break;
                case "--format":
                    format = value.ToLowerInvariant();
                    if (format is not ("json" or "markdown"))
                    {
                        return Error("Format must be 'json' or 'markdown'.");
                    }

                    break;
                case "--hourly-rate":
                    if (!decimal.TryParse(
                            value,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out decimal parsedRate) ||
                        parsedRate < 0m)
                    {
                        return Error(
                            "Hourly rate must be a non-negative decimal using '.' as the decimal separator.");
                    }

                    hourlyRate = parsedRate;
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
                    outputPath = value;
                    break;
                default:
                    return Error($"Unknown change option '{option}'.");
            }
        }

        if ((baseRevision is null) != (headRevision is null))
        {
            return Error("Options --base and --head must be supplied together.");
        }

        if ((baseDirectory is null) != (headDirectory is null))
        {
            return Error("Options --base-path and --head-path must be supplied together.");
        }

        if ((baseEvidencePath is null) != (headEvidencePath is null))
        {
            return Error("Options --base-evidence and --head-evidence must be supplied together.");
        }

        int selectorCount = (commit is null ? 0 : 1) +
            (range is null ? 0 : 1) +
            (pullRequest is null ? 0 : 1) +
            (baseRevision is null ? 0 : 1) +
            (baseDirectory is null ? 0 : 1) +
            (baseEvidencePath is null ? 0 : 1);
        if (selectorCount != 1)
        {
            return Error(
                "Select exactly one Git revision selector, one --base-path/--head-path pair, " +
                "or one --base-evidence/--head-evidence pair.");
        }

        bool nonGitSelection = baseDirectory is not null || baseEvidencePath is not null;
        if (nonGitSelection && repositoryPath is not null)
        {
            return Error(
                "Do not supply a positional repository path with directory or evidence snapshot selectors.");
        }

        if (!nonGitSelection && repositoryPath is null)
        {
            return Error("A repository path is required for Git revision and pull-request selectors.");
        }

        if (parent is not null && commit is null)
        {
            return Error("Option --parent is valid only with --commit.");
        }

        if (githubRepository is not null && pullRequest is null)
        {
            return Error("Option --repo is valid only with --pr.");
        }

        if (fetchMissing && pullRequest is null)
        {
            return Error("Option --fetch-missing is valid only with --pr.");
        }

        if (compact && format != "json")
        {
            return Error("Option --compact can only be used with JSON output.");
        }

        if (noRate && (hourlyRate is not null || currencyProvided))
        {
            return Error("Option --no-rate cannot be combined with --hourly-rate or --currency.");
        }

        if (currencyProvided && hourlyRate is null)
        {
            return Error("Option --currency requires a caller-supplied --hourly-rate.");
        }

        return new ChangeCommandParseResult(new ChangeCommandOptions
        {
            RepositoryPath = repositoryPath,
            BaseRevision = baseRevision,
            HeadRevision = headRevision,
            Commit = commit,
            Parent = parent,
            Range = range,
            PullRequest = pullRequest,
            GitHubRepository = githubRepository,
            FetchMissing = fetchMissing,
            BaseDirectory = baseDirectory,
            HeadDirectory = headDirectory,
            BaseEvidencePath = baseEvidencePath,
            HeadEvidencePath = headEvidencePath,
            Profile = profile,
            Format = format,
            Compact = compact,
            NoRate = noRate,
            HourlyRate = hourlyRate,
            Currency = currency,
            OutputPath = outputPath,
        }, null);
    }

    private static ChangeCommandParseResult Error(string message) => new(null, message);

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h";

    private static bool TryParseProfile(string value, out EstimationProfile profile)
    {
        switch (value.ToLowerInvariant())
        {
            case "implementation":
                profile = EstimationProfile.Implementation;
                return true;
            case "recreation":
                profile = EstimationProfile.Recreation;
                return true;
            default:
                profile = default;
                return false;
        }
    }
}
