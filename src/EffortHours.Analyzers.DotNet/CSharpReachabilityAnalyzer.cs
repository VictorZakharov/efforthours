using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffortHours.Analyzers.DotNet;

internal static class CSharpReachabilityAnalyzer
{
    public static CSharpReachabilityResult Analyze(
        CompilationUnitSyntax root,
        string path,
        string projectScope)
    {
        MethodDeclarationSyntax[] candidates =
        [
            .. root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(IsBoundedCandidate),
        ];
        if (candidates.Length == 0)
        {
            return CSharpReachabilityResult.Empty;
        }

        HashSet<MethodDeclarationSyntax> candidateSet = [.. candidates];
        Dictionary<string, MethodDeclarationSyntax[]> candidatesByName = candidates
            .GroupBy(method => method.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        HashSet<MethodDeclarationSyntax> reachable = [];
        Queue<MethodDeclarationSyntax> pending = new();

        foreach (SimpleNameSyntax reference in root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(reference => !IsDeclarationName(reference, candidateSet) &&
                !HasCandidateAncestor(reference, candidateSet)))
        {
            AddNamedCandidates(reference.Identifier.ValueText, candidatesByName, reachable, pending);
        }

        foreach (LiteralExpressionSyntax literal in root.DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                !HasCandidateAncestor(literal, candidateSet)))
        {
            AddNamedCandidates(literal.Token.ValueText, candidatesByName, reachable, pending);
        }

        while (pending.TryDequeue(out MethodDeclarationSyntax? method))
        {
            foreach (SimpleNameSyntax reference in method.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (!IsDeclarationName(reference, candidateSet))
                {
                    AddNamedCandidates(reference.Identifier.ValueText, candidatesByName, reachable, pending);
                }
            }

            foreach (LiteralExpressionSyntax literal in method.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)))
            {
                AddNamedCandidates(literal.Token.ValueText, candidatesByName, reachable, pending);
            }
        }

        HashSet<MethodDeclarationSyntax> excluded =
        [
            .. candidates.Where(method => !reachable.Contains(method)),
        ];
        if (excluded.Count == 0)
        {
            return CSharpReachabilityResult.Empty;
        }

        EvidenceFact fact = DotNetEvidence.Fact(
            $"dotnet:excluded-unreferenced-private:{path}",
            EvidenceKinds.ExcludedContent,
            projectScope,
            $"{excluded.Count} private method(s) in '{path}' had no bounded intra-file reference and were excluded from represented structure and semantic evidence.",
            EvidenceSourceKind.Inferred,
            "bounded Roslyn intra-file private-method reachability",
            excluded.OrderBy(method => method.SpanStart).Select(method => DotNetEvidence.Location(
                path,
                method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                method.Identifier.ValueText)),
            [DotNetEvidence.Measurement("excluded-private-methods", excluded.Count, "methods")],
            [
                "classification:unreferenced-private",
                "reachability:bounded-intra-file",
                "reachability:framework-and-partial-types-retained",
            ]);
        return new CSharpReachabilityResult(excluded, fact);
    }

    private static bool IsBoundedCandidate(MethodDeclarationSyntax method) =>
        method.Modifiers.Any(SyntaxKind.PrivateKeyword) &&
        method.Identifier.ValueText != "Main" &&
        !method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword) ||
            modifier.IsKind(SyntaxKind.ExternKeyword)) &&
        method.AttributeLists.Count == 0 &&
        method.Parent is TypeDeclarationSyntax &&
        method.Ancestors().OfType<TypeDeclarationSyntax>().All(type =>
            type.BaseList is null &&
            type.AttributeLists.Count == 0 &&
            !type.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static bool HasCandidateAncestor(
        SyntaxNode node,
        HashSet<MethodDeclarationSyntax> candidates) =>
        node.Ancestors().OfType<MethodDeclarationSyntax>().Any(candidates.Contains);

    private static bool IsDeclarationName(
        SimpleNameSyntax name,
        HashSet<MethodDeclarationSyntax> candidates) =>
        name.Parent is MethodDeclarationSyntax method &&
        candidates.Contains(method) &&
        method.Identifier.Span == name.Identifier.Span;

    private static void AddNamedCandidates(
        string name,
        Dictionary<string, MethodDeclarationSyntax[]> candidatesByName,
        HashSet<MethodDeclarationSyntax> reachable,
        Queue<MethodDeclarationSyntax> pending)
    {
        if (!candidatesByName.TryGetValue(name, out MethodDeclarationSyntax[]? matches))
        {
            return;
        }

        foreach (MethodDeclarationSyntax match in matches)
        {
            if (reachable.Add(match))
            {
                pending.Enqueue(match);
            }
        }
    }
}

internal sealed record CSharpReachabilityResult(
    IReadOnlySet<MethodDeclarationSyntax> ExcludedMethods,
    EvidenceFact? ExclusionFact)
{
    public static CSharpReachabilityResult Empty { get; } = new(
        new HashSet<MethodDeclarationSyntax>(),
        null);

    public bool IsExcluded(SyntaxNode node) => node
        .AncestorsAndSelf()
        .OfType<MethodDeclarationSyntax>()
        .Any(ExcludedMethods.Contains);
}
