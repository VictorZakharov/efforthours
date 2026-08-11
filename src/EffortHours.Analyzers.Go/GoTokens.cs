namespace EffortHours.Analyzers.Go;

internal enum GoTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Rune,
    Operator,
    NewLine,
    End,
}

internal readonly record struct GoToken(
    GoTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record GoDirective(string Text, int Line);

internal sealed record GoTokenization(
    IReadOnlyList<GoToken> Tokens,
    IReadOnlyList<GoDirective> Directives,
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
