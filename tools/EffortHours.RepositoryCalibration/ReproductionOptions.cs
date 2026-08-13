namespace EffortHours.RepositoryCalibration;

internal sealed record ReproductionOptions
{
    public required string PlanPath { get; init; }

    public required string WorkspacePath { get; init; }

    public required string CliPath { get; init; }

    public required string PacketDirectory { get; init; }

    public required string OutputPath { get; init; }

    public required string CustodyPath { get; init; }

    public string GitHubCliPath { get; init; } = "gh";

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ReproductionOptions? options,
        out string? error)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count || !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                options = null;
                error = $"Option '{arguments[index]}' requires a value.";
                return false;
            }

            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                options = null;
                error = $"Option '{arguments[index]}' was supplied more than once.";
                return false;
            }
        }

        string[] required = ["--plan", "--workspace", "--cli", "--packets", "--output", "--custody"];
        string? missing = required.FirstOrDefault(option => !values.ContainsKey(option));
        if (missing is not null)
        {
            options = null;
            error = $"Required option '{missing}' is missing.";
            return false;
        }

        string[] supported = [.. required, "--gh"];
        string? unknown = values.Keys.FirstOrDefault(option => !supported.Contains(option, StringComparer.Ordinal));
        if (unknown is not null)
        {
            options = null;
            error = $"Unknown option '{unknown}'.";
            return false;
        }

        options = new ReproductionOptions
        {
            PlanPath = Path.GetFullPath(values["--plan"]),
            WorkspacePath = Path.GetFullPath(values["--workspace"]),
            CliPath = Path.GetFullPath(values["--cli"]),
            PacketDirectory = Path.GetFullPath(values["--packets"]),
            OutputPath = Path.GetFullPath(values["--output"]),
            CustodyPath = Path.GetFullPath(values["--custody"]),
            GitHubCliPath = values.GetValueOrDefault("--gh", "gh"),
        };
        error = null;
        return true;
    }
}
