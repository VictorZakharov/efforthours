using System.Collections.Frozen;
using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffortHours.Analyzers.DotNet;

internal static class CSharpApplicationBoundaryAnalyzer
{
    private static readonly FrozenSet<string> HttpAttributes = new[]
    {
        "HttpDelete", "HttpGet", "HttpHead", "HttpOptions", "HttpPatch", "HttpPost", "HttpPut",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> MinimalApiMethods = new[]
    {
        "MapDelete", "MapFallback", "MapGet", "MapMethods", "MapPatch", "MapPost", "MapPut",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static void AddFacts(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types,
        IReadOnlyList<MethodDeclarationSyntax> methods)
    {
        AddEntryPointFact(facts, path, projectScope, nodes, methods);
        AddApiFact(facts, path, projectScope, nodes, types);
    }

    private static void AddEntryPointFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<MethodDeclarationSyntax> methods)
    {
        GlobalStatementSyntax[] globalStatements = [.. nodes.OfType<GlobalStatementSyntax>()];
        MethodDeclarationSyntax[] mainMethods =
        [
            .. methods.Where(method => method.Identifier.ValueText == "Main" &&
                method.Modifiers.Any(SyntaxKind.StaticKeyword)),
        ];
        InvocationExpressionSyntax[] hostBuilders =
        [
            .. nodes.OfType<InvocationExpressionSyntax>().Where(invocation =>
                GetInvocationName(invocation) is "CreateBuilder" or "CreateDefaultBuilder"),
        ];
        ObjectCreationExpressionSyntax[] rootCommands =
        [
            .. nodes.OfType<ObjectCreationExpressionSyntax>().Where(creation =>
                GetSimpleName(creation.Type) == "RootCommand"),
        ];
        if (globalStatements.Length == 0 &&
            mainMethods.Length == 0 &&
            hostBuilders.Length == 0 &&
            rootCommands.Length == 0)
        {
            return;
        }

        List<string> tags = [];
        if (globalStatements.Length > 0)
        {
            tags.Add("entry-point:top-level-statements");
        }

        if (mainMethods.Length > 0)
        {
            tags.Add("entry-point:main-method");
        }

        if (hostBuilders.Length > 0)
        {
            tags.Add("entry-point:host-builder");
        }

        if (rootCommands.Length > 0)
        {
            tags.Add("entry-point:root-command");
        }

        facts.Add(DotNetEvidence.Fact(
            $"dotnet:entry-point:{path}",
            EvidenceKinds.EntryPoint,
            projectScope,
            $"Application startup or command entry-point syntax detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn global-statement, static Main, host-builder, and root-command syntax",
            NormalizeLocations(
            [
                .. mainMethods.Select(method => Location(path, method, method.Identifier.ValueText)),
                .. hostBuilders.Select(call => Location(path, call, GetInvocationName(call))),
                .. rootCommands.Select(command => Location(path, command, "RootCommand")),
            ]),
            [
                DotNetEvidence.Measurement("top-level-statements", globalStatements.Length, "statements"),
                DotNetEvidence.Measurement("main-methods", mainMethods.Length, "methods"),
                DotNetEvidence.Measurement("host-builder-calls", hostBuilders.Length, "calls"),
                DotNetEvidence.Measurement("root-commands", rootCommands.Length, "commands"),
            ],
            tags));
    }

    private static void AddApiFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types)
    {
        List<EvidenceLocation> locations = [];
        HashSet<string> verbs = new(StringComparer.Ordinal);
        int controllerCount = 0;
        int attributedEndpoints = 0;
        foreach (ClassDeclarationSyntax controller in types.OfType<ClassDeclarationSyntax>()
            .Where(IsController))
        {
            controllerCount++;
            locations.Add(Location(path, controller, controller.Identifier.ValueText));
            foreach (MethodDeclarationSyntax method in controller.Members.OfType<MethodDeclarationSyntax>())
            {
                string[] methodVerbs =
                [
                    .. AttributeNames(method.AttributeLists).Where(HttpAttributes.Contains),
                ];
                if (methodVerbs.Length == 0)
                {
                    continue;
                }

                attributedEndpoints++;
                locations.Add(Location(
                    path,
                    method,
                    $"{controller.Identifier.ValueText}.{method.Identifier.ValueText}"));
                foreach (string verb in methodVerbs)
                {
                    verbs.Add(verb[4..].ToLowerInvariant());
                }
            }
        }

        InvocationExpressionSyntax[] minimalEndpoints =
        [
            .. nodes.OfType<InvocationExpressionSyntax>()
                .Where(invocation => MinimalApiMethods.Contains(GetInvocationName(invocation))),
        ];
        foreach (InvocationExpressionSyntax endpoint in minimalEndpoints)
        {
            string method = GetInvocationName(endpoint);
            locations.Add(Location(path, endpoint, method));
            verbs.Add(method[3..].ToLowerInvariant());
        }

        int mapGroups = nodes.OfType<InvocationExpressionSyntax>()
            .Count(invocation => GetInvocationName(invocation) == "MapGroup");
        if (controllerCount == 0 && attributedEndpoints == 0 &&
            minimalEndpoints.Length == 0 && mapGroups == 0)
        {
            return;
        }

        facts.Add(DotNetEvidence.Fact(
            $"dotnet:api:{path}",
            EvidenceKinds.ApiSurface,
            projectScope,
            $"ASP.NET endpoint surface detected in '{path}'.",
            EvidenceSourceKind.Observed,
            "Roslyn controller attributes, base types, and minimal-API invocation syntax",
            NormalizeLocations(locations),
            [
                DotNetEvidence.Measurement("controllers", controllerCount, "types"),
                DotNetEvidence.Measurement("attributed-endpoints", attributedEndpoints, "endpoints"),
                DotNetEvidence.Measurement("minimal-api-endpoints", minimalEndpoints.Length, "endpoints"),
                DotNetEvidence.Measurement("route-groups", mapGroups, "groups"),
            ],
            verbs.Select(verb => $"http-method:{verb}")));
    }

    private static bool IsController(BaseTypeDeclarationSyntax type) =>
        GetDeclaredTypeName(type).EndsWith("Controller", StringComparison.Ordinal) ||
        BaseTypeNames(type).Any(name => name is "Controller" or "ControllerBase") ||
        type is TypeDeclarationSyntax declaration &&
        AttributeNames(declaration.AttributeLists).Any(name => name is "ApiController" or "Route");

    private static IEnumerable<string> BaseTypeNames(BaseTypeDeclarationSyntax type) =>
        type.BaseList?.Types.Select(baseType => GetSimpleName(baseType.Type)) ?? [];

    private static IEnumerable<string> AttributeNames(SyntaxList<AttributeListSyntax> lists) =>
        lists.SelectMany(list => list.Attributes).Select(GetAttributeName);

    private static string GetAttributeName(AttributeSyntax attribute)
    {
        string name = GetSimpleName(attribute.Name);
        return name.EndsWith("Attribute", StringComparison.Ordinal)
            ? name[..^9]
            : name;
    }

    private static string GetInvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => string.Empty,
        };

    private static string GetSimpleName(SyntaxNode node) =>
        node.DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .LastOrDefault()?
            .Identifier.ValueText ?? string.Empty;

    private static string GetDeclaredTypeName(BaseTypeDeclarationSyntax type) => type switch
    {
        TypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
        EnumDeclarationSyntax declaration => declaration.Identifier.ValueText,
        _ => string.Empty,
    };

    private static EvidenceLocation Location(
        string path,
        SyntaxNode node,
        string? symbol = null) => DotNetEvidence.Location(
            path,
            node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            symbol);

    private static EvidenceLocation[] NormalizeLocations(IEnumerable<EvidenceLocation> locations) =>
        [
            .. locations
                .Distinct()
                .OrderBy(location => location.Line ?? int.MaxValue)
                .ThenBy(location => location.Symbol, StringComparer.Ordinal)
                .Take(50),
        ];
}
