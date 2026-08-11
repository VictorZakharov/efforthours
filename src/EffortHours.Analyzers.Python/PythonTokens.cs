namespace EffortHours.Analyzers.Python;

internal enum PythonTokenKind
{
    Identifier,
    Keyword,
    Number,
    String,
    Operator,
    NewLine,
    Indent,
    Dedent,
    End,
}

internal readonly record struct PythonToken(
    PythonTokenKind Kind,
    string Text,
    int Line,
    int Column);

internal sealed record PythonTokenization(
    IReadOnlyList<PythonToken> Tokens,
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
