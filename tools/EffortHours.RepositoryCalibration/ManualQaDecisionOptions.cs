namespace EffortHours.RepositoryCalibration;

internal sealed record ManualQaDecisionTemplateOptions
{
    public required string CorpusPath { get; init; }

    public required string ReviewPolicyPath { get; init; }

    public required string ExpectedReviewPolicyDigest { get; init; }

    public required string ReviewManifestPath { get; init; }

    public required string PacketDirectory { get; init; }

    public required string CompilerPolicyPath { get; init; }

    public required string ExpectedCompilerPolicyDigest { get; init; }

    public required string OutputPath { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ManualQaDecisionTemplateOptions? options,
        out string? error)
    {
        string[] required = ManualQaDecisionOptionParser.BoundaryOptions
            .Append("--output")
            .ToArray();
        if (!ManualQaDecisionOptionParser.TryRead(arguments, required, out var values, out error))
        {
            options = null;
            return false;
        }

        options = new ManualQaDecisionTemplateOptions
        {
            CorpusPath = Path.GetFullPath(values!["--corpus"]),
            ReviewPolicyPath = Path.GetFullPath(values["--review-policy"]),
            ExpectedReviewPolicyDigest = values["--expected-review-policy-digest"].ToLowerInvariant(),
            ReviewManifestPath = Path.GetFullPath(values["--review-manifest"]),
            PacketDirectory = Path.GetFullPath(values["--packets"]),
            CompilerPolicyPath = Path.GetFullPath(values["--compiler-policy"]),
            ExpectedCompilerPolicyDigest = values["--expected-compiler-policy-digest"].ToLowerInvariant(),
            OutputPath = Path.GetFullPath(values["--output"]),
        };
        return ManualQaDecisionOptionParser.ValidateDigests(
            options.ExpectedReviewPolicyDigest,
            options.ExpectedCompilerPolicyDigest,
            null,
            out error);
    }
}

internal sealed record ManualQaDecisionCompileOptions
{
    public required string CorpusPath { get; init; }

    public required string ReviewPolicyPath { get; init; }

    public required string ExpectedReviewPolicyDigest { get; init; }

    public required string ReviewManifestPath { get; init; }

    public required string PacketDirectory { get; init; }

    public required string CompilerPolicyPath { get; init; }

    public required string ExpectedCompilerPolicyDigest { get; init; }

    public required string PlanPath { get; init; }

    public required string ExpectedPlanDigest { get; init; }

    public required string OutputPath { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ManualQaDecisionCompileOptions? options,
        out string? error)
    {
        string[] required =
        [
            .. ManualQaDecisionOptionParser.BoundaryOptions,
            "--plan",
            "--expected-plan-digest",
            "--output",
        ];
        if (!ManualQaDecisionOptionParser.TryRead(arguments, required, out var values, out error))
        {
            options = null;
            return false;
        }

        options = new ManualQaDecisionCompileOptions
        {
            CorpusPath = Path.GetFullPath(values!["--corpus"]),
            ReviewPolicyPath = Path.GetFullPath(values["--review-policy"]),
            ExpectedReviewPolicyDigest = values["--expected-review-policy-digest"].ToLowerInvariant(),
            ReviewManifestPath = Path.GetFullPath(values["--review-manifest"]),
            PacketDirectory = Path.GetFullPath(values["--packets"]),
            CompilerPolicyPath = Path.GetFullPath(values["--compiler-policy"]),
            ExpectedCompilerPolicyDigest = values["--expected-compiler-policy-digest"].ToLowerInvariant(),
            PlanPath = Path.GetFullPath(values["--plan"]),
            ExpectedPlanDigest = values["--expected-plan-digest"].ToLowerInvariant(),
            OutputPath = Path.GetFullPath(values["--output"]),
        };
        return ManualQaDecisionOptionParser.ValidateDigests(
            options.ExpectedReviewPolicyDigest,
            options.ExpectedCompilerPolicyDigest,
            options.ExpectedPlanDigest,
            out error);
    }
}

internal static class ManualQaDecisionOptionParser
{
    public static IReadOnlyList<string> BoundaryOptions { get; } =
    [
        "--corpus",
        "--review-policy",
        "--expected-review-policy-digest",
        "--review-manifest",
        "--packets",
        "--compiler-policy",
        "--expected-compiler-policy-digest",
    ];

    public static bool TryRead(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> required,
        out Dictionary<string, string>? values,
        out string? error) => LogicalCandidateModelOptions.TryRead(
            arguments,
            required,
            required,
            out values,
            out error);

    public static bool ValidateDigests(
        string reviewPolicyDigest,
        string compilerPolicyDigest,
        string? planDigest,
        out string? error)
    {
        foreach ((string Name, string? Value) item in new[]
                 {
                     ("--expected-review-policy-digest", reviewPolicyDigest),
                     ("--expected-compiler-policy-digest", compilerPolicyDigest),
                     ("--expected-plan-digest", planDigest),
                 })
        {
            if (item.Value is null)
            {
                continue;
            }

            if (item.Value.Length != 71 ||
                !item.Value.StartsWith("sha256:", StringComparison.Ordinal) ||
                !item.Value[7..].All(Uri.IsHexDigit))
            {
                error = $"Option '{item.Name}' must be 'sha256:' followed by 64 hexadecimal characters.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
