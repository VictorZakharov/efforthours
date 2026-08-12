namespace EffortHours.Analyzers.Cpp;

internal enum CppTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Character,
    Operator,
    Documentation,
    Preprocessor,
    End,
}

internal readonly record struct CppToken(
    CppTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record CppTokenization(
    IReadOnlyList<CppToken> Tokens,
    bool Truncated,
    bool UnterminatedLiteral,
    bool UnterminatedComment,
    bool UnbalancedDelimiters,
    bool InvalidLineSplice)
{
    public string Confidence =>
        Truncated || UnterminatedLiteral || UnterminatedComment ||
        UnbalancedDelimiters || InvalidLineSplice
            ? "low"
            : "medium";
}
