namespace EffortHours.Analyzers.Rust;

internal enum RustTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Character,
    Lifetime,
    Operator,
    Documentation,
    End,
}

internal readonly record struct RustToken(
    RustTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record RustTokenization(
    IReadOnlyList<RustToken> Tokens,
    bool Truncated,
    bool UnterminatedLiteral,
    bool UnterminatedComment,
    bool UnbalancedDelimiters)
{
    public string Confidence =>
        Truncated || UnterminatedLiteral || UnterminatedComment || UnbalancedDelimiters
            ? "low"
            : "medium";
}
