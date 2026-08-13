namespace EffortHours.RepositoryCalibration;

internal sealed record LogicalCandidateOperationalOptions
{
    public required string SamplingPlanPath { get; init; }

    public required string CorpusPath { get; init; }

    public required string SeedEvaluationPath { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ModelPath { get; init; }

    public required string NumericalPreflightPath { get; init; }

    public required string ReportPath { get; init; }

    public required string SourceCommit { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out LogicalCandidateOperationalOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--plan",
            "--corpus",
            "--seed-evaluation",
            "--outputs",
            "--model",
            "--numerical-preflight",
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

        string commit = values!["--source-commit"].ToLowerInvariant();
        if (!LogicalCandidateModelOptions.IsCommit(commit))
        {
            options = null;
            error = "Option '--source-commit' must be an exact 40-character hexadecimal Git commit.";
            return false;
        }

        options = new LogicalCandidateOperationalOptions
        {
            SamplingPlanPath = Path.GetFullPath(values["--plan"]),
            CorpusPath = Path.GetFullPath(values["--corpus"]),
            SeedEvaluationPath = Path.GetFullPath(values["--seed-evaluation"]),
            OutputDirectory = Path.GetFullPath(values["--outputs"]),
            ModelPath = Path.GetFullPath(values["--model"]),
            NumericalPreflightPath = Path.GetFullPath(values["--numerical-preflight"]),
            ReportPath = Path.GetFullPath(values["--output"]),
            SourceCommit = commit,
        };
        error = null;
        return true;
    }
}
