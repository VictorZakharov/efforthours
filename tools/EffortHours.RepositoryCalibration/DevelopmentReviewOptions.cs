namespace EffortHours.RepositoryCalibration;

internal sealed record DevelopmentReviewOptions
{
    public required string PlanPath { get; init; }

    public required string ManifestPath { get; init; }

    public required string PacketDirectory { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ReviewPlanPath { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out DevelopmentReviewOptions? options,
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
                    : "A review option requires a value.";
                return false;
            }

            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                options = null;
                error = $"Option '{arguments[index]}' was supplied more than once.";
                return false;
            }
        }

        string[] required = ["--plan", "--manifest", "--packets", "--outputs", "--output"];
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

        options = new DevelopmentReviewOptions
        {
            PlanPath = Path.GetFullPath(values["--plan"]),
            ManifestPath = Path.GetFullPath(values["--manifest"]),
            PacketDirectory = Path.GetFullPath(values["--packets"]),
            OutputDirectory = Path.GetFullPath(values["--outputs"]),
            ReviewPlanPath = Path.GetFullPath(values["--output"]),
        };
        error = null;
        return true;
    }
}
