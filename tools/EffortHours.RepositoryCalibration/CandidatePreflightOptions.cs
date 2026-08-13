namespace EffortHours.RepositoryCalibration;

internal sealed record CandidatePreflightOptions
{
    public required string SamplingPlanPath { get; init; }

    public required string CorpusPath { get; init; }

    public required string SeedEvaluationPath { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ReportPath { get; init; }

    public required string SourceCommit { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out CandidatePreflightOptions? options,
        out string? error)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count ||
                !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                options = null;
                error = index < arguments.Count
                    ? $"Option '{arguments[index]}' requires a value."
                    : "A candidate-preflight option requires a value.";
                return false;
            }

            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                options = null;
                error = $"Option '{arguments[index]}' was supplied more than once.";
                return false;
            }
        }

        string[] required =
        [
            "--plan",
            "--corpus",
            "--seed-evaluation",
            "--outputs",
            "--output",
            "--source-commit",
        ];
        string? missing = required.FirstOrDefault(option => !values.ContainsKey(option));
        string? unknown = values.Keys.FirstOrDefault(option =>
            !required.Contains(option, StringComparer.Ordinal));
        if (missing is not null || unknown is not null)
        {
            options = null;
            error = missing is not null
                ? $"Required option '{missing}' is missing."
                : $"Unknown option '{unknown}'.";
            return false;
        }

        string sourceCommit = values["--source-commit"];
        if (sourceCommit.Length != 40 || sourceCommit.Any(character => !Uri.IsHexDigit(character)))
        {
            options = null;
            error = "Option '--source-commit' must be an exact 40-character hexadecimal Git commit.";
            return false;
        }

        options = new CandidatePreflightOptions
        {
            SamplingPlanPath = Path.GetFullPath(values["--plan"]),
            CorpusPath = Path.GetFullPath(values["--corpus"]),
            SeedEvaluationPath = Path.GetFullPath(values["--seed-evaluation"]),
            OutputDirectory = Path.GetFullPath(values["--outputs"]),
            ReportPath = Path.GetFullPath(values["--output"]),
            SourceCommit = sourceCommit.ToLowerInvariant(),
        };
        error = null;
        return true;
    }
}
