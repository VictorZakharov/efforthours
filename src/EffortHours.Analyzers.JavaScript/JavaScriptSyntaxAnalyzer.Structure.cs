using Acornima.Ast;
using EffortHours.Analysis;

namespace EffortHours.Analyzers.JavaScript;

internal static partial class JavaScriptSyntaxAnalyzer
{
    private static void ApplyCallableStructuralMetrics(
        Node root,
        JavaScriptTokenization tokens,
        JavaScriptSourceMetrics metrics)
    {
        Stack<Node> pending = new();
        pending.Push(root);
        while (pending.TryPop(out Node? node))
        {
            if (IsCallable(node))
            {
                metrics.CallableStructuralMetrics.Add(AnalyzeCallable(node, tokens));
            }

            foreach (Node child in node.ChildNodes)
            {
                pending.Push(child);
            }
        }

        metrics.StructuralParserBackedFiles = 1;
        metrics.StructuralDetectedCallables = metrics.CallableStructuralMetrics.Count;
    }

    private static CallableStructuralMetric AnalyzeCallable(
        Node callable,
        JavaScriptTokenization tokens)
    {
        int complexity = 1;
        int maximumNesting = 0;
        Stack<(Node Node, int Depth)> pending = new();
        foreach (Node child in callable.ChildNodes)
        {
            pending.Push((child, 0));
        }

        while (pending.TryPop(out (Node Node, int Depth) current))
        {
            if (IsCallable(current.Node))
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
            foreach (Node child in current.Node.ChildNodes)
            {
                pending.Push((child, depth));
            }
        }

        return new CallableStructuralMetric(
            CountTokens(tokens.Tokens, callable.Start, callable.End),
            complexity,
            maximumNesting);
    }

    private static int CountTokens(
        IReadOnlyList<JavaScriptToken> tokens,
        int start,
        int end)
    {
        int first = LowerBound(tokens, start);
        int last = LowerBound(tokens, end);
        return Math.Max(1, last - first);
    }

    private static int LowerBound(IReadOnlyList<JavaScriptToken> tokens, int position)
    {
        int low = 0;
        int high = tokens.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (tokens[middle].Start < position)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static bool IsCallable(Node node) => node.Type is
        NodeType.FunctionDeclaration or NodeType.FunctionExpression or
        NodeType.ArrowFunctionExpression;

    private static bool IsComplexityDecision(Node node) =>
        IsNestingDecision(node) || node.Type == NodeType.LogicalExpression;

    private static bool IsNestingDecision(Node node) => node.Type is
        NodeType.IfStatement or NodeType.ForStatement or NodeType.ForInStatement or
        NodeType.ForOfStatement or NodeType.WhileStatement or NodeType.DoWhileStatement or
        NodeType.CatchClause or NodeType.ConditionalExpression or NodeType.SwitchCase;
}
