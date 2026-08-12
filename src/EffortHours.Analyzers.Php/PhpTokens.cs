namespace EffortHours.Analyzers.Php;

internal enum PhpTokenKind
{
    Identifier,
    Variable,
    String,
    Number,
    Symbol,
}

internal readonly record struct PhpToken(PhpTokenKind Kind, string Text, int Line);

internal sealed record PhpTokenizationResult(
    IReadOnlyList<PhpToken> Tokens,
    bool Truncated,
    bool StructurallyBalanced,
    int DocumentationComments,
    int InlineHtmlRegions,
    int PhpRegions);
