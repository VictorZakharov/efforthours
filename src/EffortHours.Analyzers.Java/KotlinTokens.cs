namespace EffortHours.Analyzers.Java;

internal enum KotlinTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Character,
    Operator,
    End,
}

internal readonly record struct KotlinToken(
    KotlinTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record KotlinTokenization(
    IReadOnlyList<KotlinToken> Tokens,
    bool Truncated,
    bool UnterminatedLiteral,
    bool UnterminatedComment,
    bool UnbalancedDelimiters,
    bool HasShebang)
{
    public string Confidence =>
        Truncated || UnterminatedLiteral || UnterminatedComment || UnbalancedDelimiters
            ? "low"
            : "medium";
}
