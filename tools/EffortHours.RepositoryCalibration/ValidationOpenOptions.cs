namespace EffortHours.RepositoryCalibration;

internal sealed record ValidationOpenOptions
{
    public required string RepositoryRoot { get; init; }

    public required string PlanPath { get; init; }

    public required string ReproductionManifestPath { get; init; }

    public required string CustodyPath { get; init; }

    public required string CandidateManifestPath { get; init; }

    public required string SourceCommit { get; init; }

    public required string WorkspacePath { get; init; }

    public required string CliPath { get; init; }

    public required string PacketDirectory { get; init; }

    public required string OutputPath { get; init; }

    public string GitHubCliPath { get; init; } = "gh";

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ValidationOpenOptions? options,
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
                    : "A validation-open option requires a value.";
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
            "--repository-root",
            "--plan",
            "--reproduction-manifest",
            "--custody",
            "--candidate-manifest",
            "--source-commit",
            "--workspace",
            "--cli",
            "--packets",
            "--output",
        ];
        string? missing = required.FirstOrDefault(option => !values.ContainsKey(option));
        string[] supported = [.. required, "--gh"];
        string? unknown = values.Keys.FirstOrDefault(option =>
            !supported.Contains(option, StringComparer.Ordinal));
        if (missing is not null || unknown is not null)
        {
            options = null;
            error = missing is not null
                ? $"Required option '{missing}' is missing."
                : $"Unknown option '{unknown}'.";
            return false;
        }

        options = new ValidationOpenOptions
        {
            RepositoryRoot = Path.GetFullPath(values["--repository-root"]),
            PlanPath = Path.GetFullPath(values["--plan"]),
            ReproductionManifestPath = Path.GetFullPath(values["--reproduction-manifest"]),
            CustodyPath = Path.GetFullPath(values["--custody"]),
            CandidateManifestPath = Path.GetFullPath(values["--candidate-manifest"]),
            SourceCommit = values["--source-commit"],
            WorkspacePath = Path.GetFullPath(values["--workspace"]),
            CliPath = Path.GetFullPath(values["--cli"]),
            PacketDirectory = Path.GetFullPath(values["--packets"]),
            OutputPath = Path.GetFullPath(values["--output"]),
            GitHubCliPath = values.GetValueOrDefault("--gh", "gh"),
        };
        error = null;
        return true;
    }
}
