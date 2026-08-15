namespace EffortHours.RepositoryCalibration;

internal sealed record ValidationSelectionOptions
{
    public required string RepositoryRoot { get; init; }

    public required string SamplingPlanPath { get; init; }

    public required string OpeningPath { get; init; }

    public required string CorpusPath { get; init; }

    public required string CandidateManifestPath { get; init; }

    public required string ModelPath { get; init; }

    public required string SeedOutputDirectory { get; init; }

    public required string CandidateOutputDirectory { get; init; }

    public required string SeedEvaluationPath { get; init; }

    public required string CandidateEvaluationPath { get; init; }

    public required string DecisionPath { get; init; }

    public required string SourceCommit { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ValidationSelectionOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--repository-root",
            "--plan",
            "--opening",
            "--corpus",
            "--candidate-manifest",
            "--model",
            "--seed-outputs",
            "--candidate-outputs",
            "--seed-evaluation",
            "--candidate-evaluation",
            "--decision",
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

        options = new ValidationSelectionOptions
        {
            RepositoryRoot = Path.GetFullPath(values["--repository-root"]),
            SamplingPlanPath = Path.GetFullPath(values["--plan"]),
            OpeningPath = Path.GetFullPath(values["--opening"]),
            CorpusPath = Path.GetFullPath(values["--corpus"]),
            CandidateManifestPath = Path.GetFullPath(values["--candidate-manifest"]),
            ModelPath = Path.GetFullPath(values["--model"]),
            SeedOutputDirectory = Path.GetFullPath(values["--seed-outputs"]),
            CandidateOutputDirectory = Path.GetFullPath(values["--candidate-outputs"]),
            SeedEvaluationPath = Path.GetFullPath(values["--seed-evaluation"]),
            CandidateEvaluationPath = Path.GetFullPath(values["--candidate-evaluation"]),
            DecisionPath = Path.GetFullPath(values["--decision"]),
            SourceCommit = commit,
        };
        error = null;
        return true;
    }
}
