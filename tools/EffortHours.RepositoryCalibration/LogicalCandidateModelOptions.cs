namespace EffortHours.RepositoryCalibration;

internal sealed record LogicalCandidateModelOptions
{
    public required string CorpusPath { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ModelPath { get; init; }

    public required string SourceCommit { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out LogicalCandidateModelOptions? options,
        out string? error)
    {
        string[] required = ["--corpus", "--outputs", "--output", "--source-commit"];
        if (!TryRead(arguments, required, out Dictionary<string, string>? values, out error))
        {
            options = null;
            return false;
        }

        string sourceCommit = values!["--source-commit"];
        if (!IsCommit(sourceCommit))
        {
            options = null;
            error = "Option '--source-commit' must be an exact 40-character hexadecimal Git commit.";
            return false;
        }

        options = new LogicalCandidateModelOptions
        {
            CorpusPath = Path.GetFullPath(values["--corpus"]),
            OutputDirectory = Path.GetFullPath(values["--outputs"]),
            ModelPath = Path.GetFullPath(values["--output"]),
            SourceCommit = sourceCommit.ToLowerInvariant(),
        };
        error = null;
        return true;
    }

    internal static bool TryRead(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> allowed,
        out Dictionary<string, string>? values,
        out string? error)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count ||
                !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                error = index < arguments.Count
                    ? $"Option '{arguments[index]}' requires a value."
                    : "A logical-candidate option requires a value.";
                return false;
            }

            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                error = $"Option '{arguments[index]}' was supplied more than once.";
                return false;
            }
        }

        string? missing = null;
        foreach (string option in allowed)
        {
            if (!values.ContainsKey(option))
            {
                missing = option;
                break;
            }
        }

        string? unknown = null;
        foreach (string option in values.Keys)
        {
            if (!allowed.Contains(option, StringComparer.Ordinal))
            {
                unknown = option;
                break;
            }
        }
        if (missing is not null || unknown is not null)
        {
            error = missing is not null
                ? $"Required option '{missing}' is missing."
                : $"Unknown option '{unknown}'.";
            return false;
        }

        error = null;
        return true;
    }

    internal static bool IsCommit(string value) =>
        value.Length == 40 && value.All(Uri.IsHexDigit);
}

internal sealed record LogicalCandidatePreflightOptions
{
    public required string SamplingPlanPath { get; init; }

    public required string CorpusPath { get; init; }

    public required string SeedEvaluationPath { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ModelPath { get; init; }

    public required string ReportPath { get; init; }

    public required string SourceCommit { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out LogicalCandidatePreflightOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--plan",
            "--corpus",
            "--seed-evaluation",
            "--outputs",
            "--model",
            "--output",
            "--source-commit",
        ];
        if (!LogicalCandidateModelOptions.TryRead(
                arguments,
                required,
                out Dictionary<string, string>? values,
                out error))
        {
            options = null;
            return false;
        }

        string sourceCommit = values!["--source-commit"];
        if (!LogicalCandidateModelOptions.IsCommit(sourceCommit))
        {
            options = null;
            error = "Option '--source-commit' must be an exact 40-character hexadecimal Git commit.";
            return false;
        }

        options = new LogicalCandidatePreflightOptions
        {
            SamplingPlanPath = Path.GetFullPath(values["--plan"]),
            CorpusPath = Path.GetFullPath(values["--corpus"]),
            SeedEvaluationPath = Path.GetFullPath(values["--seed-evaluation"]),
            OutputDirectory = Path.GetFullPath(values["--outputs"]),
            ModelPath = Path.GetFullPath(values["--model"]),
            ReportPath = Path.GetFullPath(values["--output"]),
            SourceCommit = sourceCommit.ToLowerInvariant(),
        };
        error = null;
        return true;
    }
}
