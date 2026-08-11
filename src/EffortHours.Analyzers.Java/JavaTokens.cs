namespace EffortHours.Analyzers.Java;

internal enum JavaTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Character,
    Operator,
    End,
}

internal readonly record struct JavaToken(
    JavaTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record JavaTokenization(
    IReadOnlyList<JavaToken> Tokens,
    bool Truncated,
    bool UnterminatedLiteral,
    bool UnterminatedComment,
    bool UnbalancedDelimiters,
    bool ContainsUnicodeEscapes)
{
    public string Confidence =>
        Truncated || UnterminatedLiteral || UnterminatedComment ||
        UnbalancedDelimiters || ContainsUnicodeEscapes
            ? "low"
            : "medium";
}
