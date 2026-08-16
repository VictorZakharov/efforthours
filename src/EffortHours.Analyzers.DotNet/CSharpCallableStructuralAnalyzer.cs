using EffortHours.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffortHours.Analyzers.DotNet;

internal static class CSharpCallableStructuralAnalyzer
{
    public static IReadOnlyList<CallableStructuralMetric> Analyze(
        IEnumerable<MethodDeclarationSyntax> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);
        return
        [
            .. methods
                .Where(HasExecutableBody)
                .Select(AnalyzeMethod),
        ];
    }

    public static int CountDetected(IEnumerable<MethodDeclarationSyntax> methods) =>
        methods.Count(HasExecutableBody);

    private static CallableStructuralMetric AnalyzeMethod(MethodDeclarationSyntax method)
    {
        SyntaxNode body = (SyntaxNode?)method.Body ?? method.ExpressionBody!.Expression;
        int complexity = 1;
        int maximumNesting = 0;
        Stack<(SyntaxNode Node, int Depth)> pending = new();
        foreach (SyntaxNode child in body.ChildNodes())
        {
            pending.Push((child, 0));
        }

        while (pending.TryPop(out (SyntaxNode Node, int Depth) current))
        {
            if (IsNestedCallable(current.Node))
            {
                continue;
            }

            bool complexityDecision = IsComplexityDecision(current.Node);
            bool nestingDecision = IsNestingDecision(current.Node);
            if (complexityDecision)
            {
                complexity++;
            }

            int depth = current.Depth + (nestingDecision ? 1 : 0);
            maximumNesting = Math.Max(maximumNesting, depth);
            foreach (SyntaxNode child in current.Node.ChildNodes())
            {
                pending.Push((child, depth));
            }
        }

        return new CallableStructuralMetric(
            method.DescendantTokens(descendIntoTrivia: false).Count(),
            complexity,
            maximumNesting);
    }

    private static bool HasExecutableBody(MethodDeclarationSyntax method) =>
        method.Body is not null || method.ExpressionBody is not null;

    private static bool IsNestedCallable(SyntaxNode node) => node is
        LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax;

    private static bool IsComplexityDecision(SyntaxNode node) =>
        IsNestingDecision(node) || node.IsKind(SyntaxKind.LogicalAndExpression) ||
        node.IsKind(SyntaxKind.LogicalOrExpression);

    private static bool IsNestingDecision(SyntaxNode node) => node is
        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or
        WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax or
        ConditionalExpressionSyntax or SwitchExpressionArmSyntax or SwitchSectionSyntax;
}
