namespace EffortHours.RepositoryCalibration;

internal sealed record LogicalCandidateProjectionOptions
{
    public required string EstimatePath { get; init; }

    public required string EvidencePath { get; init; }

    public required string ModelPath { get; init; }

    public required string ExpectedModelDigest { get; init; }

    public required string PrimaryStratum { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out LogicalCandidateProjectionOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--estimate",
            "--evidence",
            "--model",
            "--expected-model-digest",
            "--primary-stratum",
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

        string digest = values!["--expected-model-digest"].ToLowerInvariant();
        if (!IsSha256(digest))
        {
            options = null;
            error = "Option '--expected-model-digest' must be 'sha256:' followed by 64 hexadecimal characters.";
            return false;
        }

        string stratum = values["--primary-stratum"].Trim().ToLowerInvariant();
        if (stratum.Length == 0)
        {
            options = null;
            error = "Option '--primary-stratum' cannot be empty.";
            return false;
        }

        options = new LogicalCandidateProjectionOptions
        {
            EstimatePath = Path.GetFullPath(values["--estimate"]),
            EvidencePath = Path.GetFullPath(values["--evidence"]),
            ModelPath = Path.GetFullPath(values["--model"]),
            ExpectedModelDigest = digest,
            PrimaryStratum = stratum,
        };
        error = null;
        return true;
    }

    private static bool IsSha256(string value) =>
        value.Length == 71 &&
        value.StartsWith("sha256:", StringComparison.Ordinal) &&
        value[7..].All(Uri.IsHexDigit);
}
