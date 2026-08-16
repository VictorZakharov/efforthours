namespace EffortHours.RepositoryCalibration;

internal sealed record ManualQaReviewFreezeOptions
{
    public required string CorpusPath { get; init; }

    public required string PolicyPath { get; init; }

    public required string ExpectedPolicyDigest { get; init; }

    public required string PacketDirectory { get; init; }

    public required string ManifestPath { get; init; }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ManualQaReviewFreezeOptions? options,
        out string? error)
    {
        string[] required =
        [
            "--corpus",
            "--policy",
            "--expected-policy-digest",
            "--packets",
            "--manifest",
        ];
        if (!LogicalCandidateModelOptions.TryRead(
                arguments,
                required,
                required,
                out Dictionary<string, string>? values,
                out error))
        {
            options = null;
            return false;
        }

        string digest = values!["--expected-policy-digest"].ToLowerInvariant();
        if (digest.Length != 71 ||
            !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            !digest[7..].All(Uri.IsHexDigit))
        {
            options = null;
            error = "Option '--expected-policy-digest' must be 'sha256:' followed by 64 hexadecimal characters.";
            return false;
        }

        options = new ManualQaReviewFreezeOptions
        {
            CorpusPath = Path.GetFullPath(values["--corpus"]),
            PolicyPath = Path.GetFullPath(values["--policy"]),
            ExpectedPolicyDigest = digest,
            PacketDirectory = Path.GetFullPath(values["--packets"]),
            ManifestPath = Path.GetFullPath(values["--manifest"]),
        };
        error = null;
        return true;
    }
}
