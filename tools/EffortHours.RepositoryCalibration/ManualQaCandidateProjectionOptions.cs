namespace EffortHours.RepositoryCalibration;

internal sealed record ManualQaCandidateProjectionOptions
{
    public required string EstimatePath { get; init; }

    public required string PolicyPath { get; init; }

    public required string ExpectedPolicyDigest { get; init; }

    public string? OutputPath { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ManualQaCandidateProjectionOptions? options,
        out string? error)
    {
        string[] required = ["--estimate", "--policy", "--expected-policy-digest"];
        string[] allowed = [.. required, "--output"];
        if (!LogicalCandidateModelOptions.TryRead(
                arguments,
                required,
                allowed,
                out Dictionary<string, string>? values,
                out error))
        {
            options = null;
            return false;
        }

        string digest = values!["--expected-policy-digest"].ToLowerInvariant();
        if (!IsSha256(digest))
        {
            options = null;
            error = "Option '--expected-policy-digest' must be 'sha256:' followed by 64 hexadecimal characters.";
            return false;
        }

        options = new ManualQaCandidateProjectionOptions
        {
            EstimatePath = Path.GetFullPath(values["--estimate"]),
            PolicyPath = Path.GetFullPath(values["--policy"]),
            ExpectedPolicyDigest = digest,
            OutputPath = values.TryGetValue("--output", out string? output)
                ? Path.GetFullPath(output)
                : null,
        };
        error = null;
        return true;
    }

    private static bool IsSha256(string value) =>
        value.Length == 71 &&
        value.StartsWith("sha256:", StringComparison.Ordinal) &&
        value[7..].All(Uri.IsHexDigit);
}
