namespace EffortHours.Analyzers.GDScript;

internal enum GDScriptTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Documentation,
    Operator,
    NewLine,
    Indent,
    Dedent,
    End,
}

internal readonly record struct GDScriptToken(
    GDScriptTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record GDScriptTokenization(
    IReadOnlyList<GDScriptToken> Tokens,
    bool Truncated,
    bool UnterminatedString,
    bool InvalidIndentation,
    bool UnbalancedDelimiters)
{
    public string Confidence =>
        Truncated || UnterminatedString || InvalidIndentation || UnbalancedDelimiters
            ? "low"
            : "medium";
}
