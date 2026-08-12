namespace EffortHours.Analyzers.Terraform;

internal sealed record HclDocumentAnalysis
{
    public IReadOnlyList<HclBlockAnalysis> Blocks { get; init; } = [];

    public IReadOnlyList<HclAttributeAnalysis> Attributes { get; init; } = [];

    public required bool Truncated { get; init; }

    public required bool StructurallyBalanced { get; init; }

    public required bool UnterminatedConstruct { get; init; }

    public required int UnknownConstructs { get; init; }

    public required int CommentCount { get; init; }

    public required int HeredocCount { get; init; }

    public required int FirstSemanticLine { get; init; }

    public string ParserConfidence => Truncated || !StructurallyBalanced || UnterminatedConstruct
        ? "low"
        : UnknownConstructs > Math.Max(3, Blocks.Count + Attributes.Count / 3)
            ? "medium"
            : "high";

    public IEnumerable<HclBlockAnalysis> DescendantBlocks() =>
        Blocks.SelectMany(block => block.SelfAndDescendants());

    public IEnumerable<HclAttributeAnalysis> DescendantAttributes() =>
        Attributes.Concat(DescendantBlocks().SelectMany(block => block.Attributes));
}

internal sealed record HclBlockAnalysis
{
    public required string Type { get; init; }

    public IReadOnlyList<string> Labels { get; init; } = [];

    public required int Line { get; init; }

    public IReadOnlyList<HclAttributeAnalysis> Attributes { get; init; } = [];

    public IReadOnlyList<HclBlockAnalysis> Blocks { get; init; } = [];

    public IEnumerable<HclBlockAnalysis> SelfAndDescendants()
    {
        yield return this;
        foreach (HclBlockAnalysis child in Blocks)
        {
            foreach (HclBlockAnalysis descendant in child.SelfAndDescendants())
            {
                yield return descendant;
            }
        }
    }
}

internal sealed record HclAttributeAnalysis
{
    public required string Name { get; init; }

    public required int Line { get; init; }

    public string? LiteralString { get; init; }

    public bool? LiteralBoolean { get; init; }

    public IReadOnlyList<string> StringLiterals { get; init; } = [];

    public IReadOnlyList<string> Identifiers { get; init; } = [];

    public required int Tokens { get; init; }

    public required int Traversals { get; init; }

    public required int FunctionCalls { get; init; }

    public required int Conditionals { get; init; }

    public required int ForExpressions { get; init; }

    public required int TemplateExpressions { get; init; }

    public bool IsDynamic => TemplateExpressions > 0 ||
        LiteralString is null && Identifiers.Any(identifier => identifier is
            "var" or "local" or "module" or "data" or "each" or "count");
}
