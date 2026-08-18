using System.Globalization;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal enum ChangeBenchmarkMode
{
    LargeTree,
    LongRange,
    AuthorPeriod,
    AuthorPeriodManifest,
}

internal sealed record ChangeBenchmarkOptions(
    ChangeBenchmarkMode Mode,
    int Files,
    int ContextProjects,
    int LinesPerFile,
    int Commits,
    int MaximumRangeComponents,
    bool CompareIndependent,
    bool CombinedOnly,
    bool ProcessMatrix,
    string? SourceTreePath,
    bool KeepRepository,
    bool PrepareOnly,
    string? PreparedFixturePath,
    int? FileAnalysisWorkers,
    decimal? MaximumSeconds,
    decimal? MaximumPeakMib)
{
    public const string Usage =
        "Usage: change-benchmark [--tree|--range|--author-period|--author-period-manifest] " +
        "[--files <count>] [--context-projects <count>] " +
        "[--lines-per-file <count>] [--commits <count>] " +
        "[--maximum-range-components <count>] [--max-seconds <value>] " +
        "[--max-peak-mib <value>] [--compare-independent] [--process-matrix] " +
        "[--combined-only] " +
        "[--source-tree <path>] [--keep] [--prepare-only] " +
        "[--prepared-fixture <descriptor-path>] [--file-analysis-workers <count>]";

    public string Name => Mode switch
    {
        ChangeBenchmarkMode.LargeTree => "large-tree",
        ChangeBenchmarkMode.LongRange => "long-range",
        ChangeBenchmarkMode.AuthorPeriod => "author-period",
        ChangeBenchmarkMode.AuthorPeriodManifest => "author-period-manifest",
        _ => throw new InvalidOperationException($"Unsupported benchmark mode '{Mode}'."),
    };

    public static ChangeBenchmarkOptions Parse(string[] arguments)
    {
        ChangeBenchmarkMode? mode = null;
        int? files = null;
        int? contextProjects = null;
        int? linesPerFile = null;
        int? commits = null;
        int maximumRangeComponents = GitChangePlannerOptions.DefaultMaximumRangeComponents;
        bool compareIndependent = false;
        bool combinedOnly = false;
        bool processMatrix = false;
        string? sourceTreePath = null;
        bool keep = false;
        bool prepareOnly = false;
        string? preparedFixturePath = null;
        int? fileAnalysisWorkers = null;
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
                case "--author-period":
                    mode = SelectMode(mode, ChangeBenchmarkMode.AuthorPeriod);
                    break;
                case "--author-period-manifest":
                    mode = SelectMode(mode, ChangeBenchmarkMode.AuthorPeriodManifest);
                    break;
                case "--files":
                    files = ReadPositiveInteger(arguments, ref index, "--files");
                    break;
                case "--context-projects":
                    contextProjects = ReadNonNegativeInteger(
                        arguments,
                        ref index,
                        "--context-projects");
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
                case "--prepare-only":
                    prepareOnly = true;
                    break;
                case "--prepared-fixture":
                    preparedFixturePath = ReadValue(arguments, ref index, "--prepared-fixture");
                    break;
                case "--file-analysis-workers":
                    fileAnalysisWorkers = ReadPositiveInteger(
                        arguments,
                        ref index,
                        "--file-analysis-workers");
                    break;
                case "--compare-independent":
                    compareIndependent = true;
                    break;
                case "--combined-only":
                    combinedOnly = true;
                    break;
                case "--process-matrix":
                    processMatrix = true;
                    break;
                case "--source-tree":
                    sourceTreePath = ReadValue(arguments, ref index, "--source-tree");
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arguments[index]}'.");
            }
        }

        ChangeBenchmarkMode selected = mode ?? ChangeBenchmarkMode.LargeTree;
        int selectedCommits = commits ?? selected switch
        {
            ChangeBenchmarkMode.LargeTree => 1,
            ChangeBenchmarkMode.LongRange => 128,
            ChangeBenchmarkMode.AuthorPeriod => 8,
            ChangeBenchmarkMode.AuthorPeriodManifest => 3,
            _ => throw new InvalidOperationException($"Unsupported benchmark mode '{selected}'."),
        };
        if (selected == ChangeBenchmarkMode.LargeTree && selectedCommits != 1)
        {
            throw new ArgumentException("Large-tree mode requires exactly one final change commit.");
        }

        if (selected == ChangeBenchmarkMode.AuthorPeriod && selectedCommits > 9)
        {
            throw new ArgumentException(
                "Author-period mode permits at most nine qualifying commits so the fixture retains a narrow selection.");
        }

        int maximumManifestBenchmarkCommits = Math.Min(
            GitPortfolioPlannerOptions.MaximumSupportedHistoryCommits,
            ChangeAuthorPeriodManifestLimits.MaximumSelectedCommits / 2);
        if (selected == ChangeBenchmarkMode.AuthorPeriodManifest &&
            (selectedCommits < 3 || selectedCommits > maximumManifestBenchmarkCommits))
        {
            throw new ArgumentException(
                "Author-period-manifest mode requires at least three qualifying commits per repository " +
                $"and at most {maximumManifestBenchmarkCommits}.");
        }

        if (selected == ChangeBenchmarkMode.AuthorPeriodManifest &&
            !compareIndependent &&
            !combinedOnly &&
            !prepareOnly)
        {
            throw new ArgumentException(
                "Author-period-manifest mode requires '--compare-independent' or " +
                "'--combined-only'.");
        }

        if (compareIndependent && combinedOnly)
        {
            throw new ArgumentException(
                "Options '--compare-independent' and '--combined-only' are mutually exclusive.");
        }

        if (combinedOnly && selected != ChangeBenchmarkMode.AuthorPeriodManifest)
        {
            throw new ArgumentException(
                "Option '--combined-only' is valid only with '--author-period-manifest'.");
        }

        if (compareIndependent && selected is not (
            ChangeBenchmarkMode.AuthorPeriod or ChangeBenchmarkMode.AuthorPeriodManifest))
        {
            throw new ArgumentException(
                "Option '--compare-independent' is valid only with an author-period mode.");
        }

        if (processMatrix && selected != ChangeBenchmarkMode.AuthorPeriod)
        {
            throw new ArgumentException(
                "Option '--process-matrix' is valid only with '--author-period'.");
        }

        if (processMatrix && compareIndependent)
        {
            throw new ArgumentException(
                "Options '--process-matrix' and '--compare-independent' measure separate baselines and cannot be combined.");
        }

        if (processMatrix && (maximumSeconds is not null || maximumPeakMib is not null))
        {
            throw new ArgumentException(
                "Process-matrix wall time and memory are observations and cannot be threshold gates.");
        }

        if (sourceTreePath is not null && selected != ChangeBenchmarkMode.AuthorPeriod)
        {
            throw new ArgumentException(
                "Option '--source-tree' is valid only with '--author-period'.");
        }

        if (sourceTreePath is not null && files is not null)
        {
            throw new ArgumentException(
                "Options '--source-tree' and '--files' cannot be combined.");
        }

        if (prepareOnly && selected != ChangeBenchmarkMode.AuthorPeriodManifest)
        {
            throw new ArgumentException(
                "Option '--prepare-only' is valid only with '--author-period-manifest'.");
        }

        if (preparedFixturePath is not null && selected != ChangeBenchmarkMode.AuthorPeriodManifest)
        {
            throw new ArgumentException(
                "Option '--prepared-fixture' is valid only with '--author-period-manifest'.");
        }

        if (prepareOnly && preparedFixturePath is not null)
        {
            throw new ArgumentException(
                "Options '--prepare-only' and '--prepared-fixture' cannot be combined.");
        }

        if (preparedFixturePath is not null &&
            (files is not null || contextProjects is not null || linesPerFile is not null ||
                commits is not null))
        {
            throw new ArgumentException(
                "A prepared fixture owns its file, context-project, line, and commit shape; " +
                "do not repeat those options with '--prepared-fixture'.");
        }

        if (prepareOnly && (maximumSeconds is not null || maximumPeakMib is not null))
        {
            throw new ArgumentException(
                "Fixture preparation is outside the measured benchmark and cannot use " +
                "analysis time or memory thresholds.");
        }

        if (fileAnalysisWorkers is not null &&
            selected != ChangeBenchmarkMode.AuthorPeriodManifest)
        {
            throw new ArgumentException(
                "Option '--file-analysis-workers' is valid only with " +
                "'--author-period-manifest'.");
        }

        if (fileAnalysisWorkers > Math.Max(1, Environment.ProcessorCount))
        {
            throw new ArgumentException(
                "Option '--file-analysis-workers' cannot exceed the available logical processors.");
        }

        if (prepareOnly && fileAnalysisWorkers is not null)
        {
            throw new ArgumentException(
                "Fixture preparation does not run analysis and cannot select analysis workers.");
        }

        if (maximumRangeComponents > GitChangePlannerOptions.MaximumSupportedRangeComponents)
        {
            throw new ArgumentException(
                $"Option '--maximum-range-components' cannot exceed " +
                $"{GitChangePlannerOptions.MaximumSupportedRangeComponents}.");
        }

        return new ChangeBenchmarkOptions(
            selected,
            files ?? selected switch
            {
                ChangeBenchmarkMode.LargeTree => 10_000,
                ChangeBenchmarkMode.LongRange => 32,
                ChangeBenchmarkMode.AuthorPeriod => 29_225,
                ChangeBenchmarkMode.AuthorPeriodManifest => 1_025,
                _ => throw new InvalidOperationException($"Unsupported benchmark mode '{selected}'."),
            },
            contextProjects ?? (selected == ChangeBenchmarkMode.AuthorPeriodManifest ? 512 : 0),
            linesPerFile ?? selected switch
            {
                ChangeBenchmarkMode.LargeTree => 100,
                ChangeBenchmarkMode.LongRange => 20,
                ChangeBenchmarkMode.AuthorPeriod => 8,
                ChangeBenchmarkMode.AuthorPeriodManifest => 8,
                _ => throw new InvalidOperationException($"Unsupported benchmark mode '{selected}'."),
            },
            selectedCommits,
            maximumRangeComponents,
            compareIndependent,
            combinedOnly,
            processMatrix,
            sourceTreePath is null ? null : Path.GetFullPath(sourceTreePath),
            keep,
            prepareOnly,
            preparedFixturePath is null ? null : Path.GetFullPath(preparedFixturePath),
            fileAnalysisWorkers,
            maximumSeconds,
            maximumPeakMib);
    }

    private static ChangeBenchmarkMode SelectMode(
        ChangeBenchmarkMode? current,
        ChangeBenchmarkMode selected)
    {
        if (current is not null)
        {
            throw new ArgumentException(
                "Benchmark mode options are mutually exclusive.");
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

    private static int ReadNonNegativeInteger(string[] arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Length ||
            !int.TryParse(arguments[++index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
            value < 0)
        {
            throw new ArgumentException($"Option '{option}' requires a non-negative integer.");
        }

        return value;
    }

    private static string ReadValue(string[] arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[++index]))
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return arguments[index];
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
