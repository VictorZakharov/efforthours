namespace EffortHours.Analyzers.Terraform;

internal enum HclTokenKind
{
    Identifier,
    String,
    Number,
    Symbol,
    NewLine,
}

internal readonly record struct HclToken(
    HclTokenKind Kind,
    string Value,
    int Line,
    bool HasTemplate = false);

internal sealed record HclTokenizationResult
{
    public IReadOnlyList<HclToken> Tokens { get; init; } = [];

    public required bool Truncated { get; init; }

    public required bool UnterminatedConstruct { get; init; }

    public required int CommentCount { get; init; }

    public required int HeredocCount { get; init; }
}
