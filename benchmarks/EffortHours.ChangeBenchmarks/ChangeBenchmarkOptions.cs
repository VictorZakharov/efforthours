using System.Globalization;
using EffortHours.Change;

namespace EffortHours.ChangeBenchmarks;

internal enum ChangeBenchmarkMode
{
    LargeTree,
    LongRange,
}

internal sealed record ChangeBenchmarkOptions(
    ChangeBenchmarkMode Mode,
    int Files,
    int LinesPerFile,
    int Commits,
    int MaximumRangeComponents,
    bool KeepRepository,
    decimal? MaximumSeconds,
    decimal? MaximumPeakMib)
{
    public const string Usage =
        "Usage: change-benchmark [--tree|--range] [--files <count>] " +
        "[--lines-per-file <count>] [--commits <count>] " +
        "[--maximum-range-components <count>] [--max-seconds <value>] " +
        "[--max-peak-mib <value>] [--keep]";

    public string Name => Mode == ChangeBenchmarkMode.LargeTree ? "large-tree" : "long-range";

    public static ChangeBenchmarkOptions Parse(string[] arguments)
    {
        ChangeBenchmarkMode? mode = null;
        int? files = null;
        int? linesPerFile = null;
        int? commits = null;
        int maximumRangeComponents = GitChangePlannerOptions.DefaultMaximumRangeComponents;
        bool keep = false;
        decimal? maximumSeconds = null;
        decimal? maximumPeakMib = null;

        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--tree":
                    mode = SelectMode(mode, ChangeBenchmarkMode.LargeTree);
                    break;
                case "--range":
                    mode = SelectMode(mode, ChangeBenchmarkMode.LongRange);
                    break;
                case "--files":
                    files = ReadPositiveInteger(arguments, ref index, "--files");
                    break;
                case "--lines-per-file":
                    linesPerFile = ReadPositiveInteger(arguments, ref index, "--lines-per-file");
                    break;
                case "--commits":
                    commits = ReadPositiveInteger(arguments, ref index, "--commits");
                    break;
                case "--maximum-range-components":
                    maximumRangeComponents = ReadPositiveInteger(
                        arguments,
                        ref index,
                        "--maximum-range-components");
                    break;
                case "--max-seconds":
                    maximumSeconds = ReadPositiveDecimal(arguments, ref index, "--max-seconds");
                    break;
                case "--max-peak-mib":
                    maximumPeakMib = ReadPositiveDecimal(arguments, ref index, "--max-peak-mib");
                    break;
                case "--keep":
                    keep = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arguments[index]}'.");
            }
        }

        ChangeBenchmarkMode selected = mode ?? ChangeBenchmarkMode.LargeTree;
        int selectedCommits = commits ?? (selected == ChangeBenchmarkMode.LargeTree ? 1 : 128);
        if (selected == ChangeBenchmarkMode.LargeTree && selectedCommits != 1)
        {
            throw new ArgumentException("Large-tree mode requires exactly one final change commit.");
        }

        if (maximumRangeComponents > GitChangePlannerOptions.MaximumSupportedRangeComponents)
        {
            throw new ArgumentException(
                $"Option '--maximum-range-components' cannot exceed " +
                $"{GitChangePlannerOptions.MaximumSupportedRangeComponents}.");
        }

        return new ChangeBenchmarkOptions(
            selected,
            files ?? (selected == ChangeBenchmarkMode.LargeTree ? 10_000 : 32),
            linesPerFile ?? (selected == ChangeBenchmarkMode.LargeTree ? 100 : 20),
            selectedCommits,
            maximumRangeComponents,
            keep,
            maximumSeconds,
            maximumPeakMib);
    }

    private static ChangeBenchmarkMode SelectMode(
        ChangeBenchmarkMode? current,
        ChangeBenchmarkMode selected)
    {
        if (current is not null)
        {
            throw new ArgumentException("Options '--tree' and '--range' are mutually exclusive.");
        }

        return selected;
    }

    private static int ReadPositiveInteger(string[] arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Length ||
            !int.TryParse(arguments[++index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
            value <= 0)
        {
            throw new ArgumentException($"Option '{option}' requires a positive integer.");
        }

        return value;
    }

    private static decimal ReadPositiveDecimal(string[] arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Length ||
            !decimal.TryParse(
                arguments[++index],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value) ||
            value <= 0m)
        {
            throw new ArgumentException($"Option '{option}' requires a positive decimal.");
        }

        return value;
    }
}
