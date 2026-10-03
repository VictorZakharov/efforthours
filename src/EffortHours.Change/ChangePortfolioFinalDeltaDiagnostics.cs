namespace EffortHours.Change;

/// <summary>Privacy-safe outcome of the selected endpoint proof.</summary>
public sealed record ChangePortfolioFinalDeltaRejection
{
    private static readonly System.Buffers.SearchValues<char> s_hexCharacters = System.Buffers.SearchValues.Create("0123456789abcdef");

    public required string Code { get; init; }
    public required string InputDigest { get; init; }
    public string? PathDigest { get; init; }

    public bool IsValid() => (Code is "composition-unproven" or "inventory-mismatch" or
        "unsupported-mode" or "suppressed-raw-mismatch" or "anchor-bound") &&
        Digest(InputDigest) && (PathDigest is null || Digest(PathDigest));

    private static bool Digest(string? value) => value is not null && value.Length == 71 &&
        value.StartsWith("sha256:", StringComparison.Ordinal) &&
        value.AsSpan(7).IndexOfAnyExcept(s_hexCharacters) < 0;
}
