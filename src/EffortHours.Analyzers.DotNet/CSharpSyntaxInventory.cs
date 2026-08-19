using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffortHours.Analyzers.DotNet;

internal sealed record CSharpSyntaxInventory(
    IReadOnlyList<BaseTypeDeclarationSyntax> Types,
    IReadOnlyList<DelegateDeclarationSyntax> Delegates,
    IReadOnlyList<MethodDeclarationSyntax> Methods,
    int DbSetProperties,
    IReadOnlyList<GlobalStatementSyntax> GlobalStatements,
    IReadOnlyList<InvocationExpressionSyntax> Invocations,
    IReadOnlyList<ObjectCreationExpressionSyntax> ObjectCreations,
    IReadOnlyList<AttributeSyntax> Attributes,
    IReadOnlyList<SimpleNameSyntax> SimpleNames,
    IReadOnlyList<SyntaxNode> BranchPointNodes)
{
    public int BranchPoints => BranchPointNodes.Count;

    public static CSharpSyntaxInventory Create(CompilationUnitSyntax root)
    {
        List<BaseTypeDeclarationSyntax> types = [];
        List<DelegateDeclarationSyntax> delegates = [];
        List<MethodDeclarationSyntax> methods = [];
        int dbSetProperties = 0;
        List<GlobalStatementSyntax> globalStatements = [];
        List<InvocationExpressionSyntax> invocations = [];
        List<ObjectCreationExpressionSyntax> objectCreations = [];
        List<AttributeSyntax> attributes = [];
        List<SimpleNameSyntax> simpleNames = [];
        List<SyntaxNode> branchPoints = [];

        foreach (SyntaxNode node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax type:
                    types.Add(type);
                    break;
                case DelegateDeclarationSyntax @delegate:
                    delegates.Add(@delegate);
                    break;
                case MethodDeclarationSyntax method:
                    methods.Add(method);
                    break;
                case PropertyDeclarationSyntax property:
                    if (GetSimpleName(property.Type) == "DbSet")
                    {
                        dbSetProperties++;
                    }

                    break;
                case GlobalStatementSyntax globalStatement:
                    globalStatements.Add(globalStatement);
                    break;
                case InvocationExpressionSyntax invocation:
                    invocations.Add(invocation);
                    break;
                case ObjectCreationExpressionSyntax objectCreation:
                    objectCreations.Add(objectCreation);
                    break;
                case AttributeSyntax attribute:
                    attributes.Add(attribute);
                    break;
                case SimpleNameSyntax simpleName when CSharpFileAnalyzer.IsRelevantSimpleName(
                    simpleName.Identifier.ValueText):
                    simpleNames.Add(simpleName);
                    break;
            }

            if (CSharpFileAnalyzer.IsBranchPoint(node))
            {
                branchPoints.Add(node);
            }
        }

        return new CSharpSyntaxInventory(
            types,
            delegates,
            methods,
            dbSetProperties,
            globalStatements,
            invocations,
            objectCreations,
            attributes,
            simpleNames,
            branchPoints);
    }

    public CSharpSyntaxInventory WithoutExcluded(CSharpReachabilityResult reachability)
    {
        if (!reachability.HasExclusions)
        {
            return this;
        }

        return new CSharpSyntaxInventory(
            Filter(Types, reachability),
            Filter(Delegates, reachability),
            Filter(Methods, reachability),
            DbSetProperties,
            Filter(GlobalStatements, reachability),
            Filter(Invocations, reachability),
            Filter(ObjectCreations, reachability),
            Filter(Attributes, reachability),
            Filter(SimpleNames, reachability),
            Filter(BranchPointNodes, reachability));
    }

    private static string GetSimpleName(TypeSyntax type)
    {
        if (type is PredefinedTypeSyntax)
        {
            return string.Empty;
        }

        SimpleNameSyntax? last = null;
        foreach (SyntaxNode node in type.DescendantNodesAndSelf())
        {
            if (node is SimpleNameSyntax simpleName)
            {
                last = simpleName;
            }
        }

        return last?.Identifier.ValueText ?? string.Empty;
    }

    private static IReadOnlyList<T> Filter<T>(
        IReadOnlyList<T> nodes,
        CSharpReachabilityResult reachability)
        where T : SyntaxNode => [.. nodes.Where(node => !reachability.IsExcluded(node))];
}
