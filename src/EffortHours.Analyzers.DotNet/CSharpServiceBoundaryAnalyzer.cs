using System.Collections.Frozen;
using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffortHours.Analyzers.DotNet;

internal static class CSharpServiceBoundaryAnalyzer
{
    private static readonly FrozenSet<string> IntegrationTypeNames = new[]
    {
        "AmazonS3Client", "BlobServiceClient", "ConsumerBuilder", "GrpcChannel", "HttpClient",
        "HttpRequestMessage", "IHttpClientFactory", "NpgsqlConnection", "ProducerBuilder",
        "ServiceBusClient", "SmtpClient", "SqlConnection",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IntegrationInvocationNames = new[]
    {
        "AddGrpcClient", "AddHttpClient", "CreateClient", "ForAddress", "Publish", "PublishAsync",
        "Send", "SendAsync",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AuthenticationInvocationNames = new[]
    {
        "AddAuthentication", "AddAuthorization", "AddIdentity", "AddIdentityCore", "AddJwtBearer",
        "RequireAuthorization", "UseAuthentication", "UseAuthorization",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static void AddFacts(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types,
        IReadOnlyList<MethodDeclarationSyntax> methods,
        CompilationUnitSyntax root)
    {
        AddBackgroundFact(facts, path, projectScope, nodes, types, methods);
        AddIntegrationFact(facts, path, projectScope, nodes, root);
        AddSecurityFact(facts, path, projectScope, nodes);
    }

    private static void AddBackgroundFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types,
        IReadOnlyList<MethodDeclarationSyntax> methods)
    {
        BaseTypeDeclarationSyntax[] hostedServices =
        [
            .. types.Where(type => BaseTypeNames(type).Any(name =>
                name is "BackgroundService" or "IHostedService")),
        ];
        BaseTypeDeclarationSyntax[] handlers =
        [
            .. types.Where(type => BaseTypeNames(type).Any(name => name is
                "IRequestHandler" or "INotificationHandler" or "IConsumer" or "IHandleMessages")),
        ];
        MethodDeclarationSyntax[] functions =
        [
            .. methods.Where(method => AttributeNames(method.AttributeLists)
                .Any(name => name is "Function" or "FunctionName")),
        ];
        int registrations = nodes.OfType<InvocationExpressionSyntax>()
            .Count(invocation => GetInvocationName(invocation) == "AddHostedService");
        if (hostedServices.Length == 0 && handlers.Length == 0 &&
            functions.Length == 0 && registrations == 0)
        {
            return;
        }

        List<EvidenceLocation> locations =
        [
            .. hostedServices.Select(type => Location(path, type, GetDeclaredTypeName(type))),
            .. handlers.Select(type => Location(path, type, GetDeclaredTypeName(type))),
            .. functions.Select(method => Location(path, method, method.Identifier.ValueText)),
        ];
        facts.Add(DotNetEvidence.Fact(
            $"dotnet:background:{path}",
            EvidenceKinds.BackgroundWork,
            projectScope,
            $"Background work, function, or message-handler structure detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn base-interface, attribute, and registration syntax",
            NormalizeLocations(locations),
            [
                DotNetEvidence.Measurement("hosted-services", hostedServices.Length, "types"),
                DotNetEvidence.Measurement("message-handlers", handlers.Length, "types"),
                DotNetEvidence.Measurement("functions", functions.Length, "methods"),
                DotNetEvidence.Measurement("hosted-service-registrations", registrations, "calls"),
            ]));
    }

    private static void AddIntegrationFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        CompilationUnitSyntax root)
    {
        ObjectCreationExpressionSyntax[] clientCreations =
        [
            .. nodes.OfType<ObjectCreationExpressionSyntax>().Where(creation =>
                IntegrationTypeNames.Contains(GetSimpleName(creation.Type))),
        ];
        InvocationExpressionSyntax[] calls =
        [
            .. nodes.OfType<InvocationExpressionSyntax>().Where(invocation =>
                IntegrationInvocationNames.Contains(GetInvocationName(invocation))),
        ];
        string[] integrationNamespaces =
        [
            .. root.Usings
                .Select(usingDirective => usingDirective.Name?.ToString())
                .Where(name => name is not null && IsIntegrationNamespace(name))
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        if (clientCreations.Length == 0 && calls.Length == 0 && integrationNamespaces.Length == 0)
        {
            return;
        }

        List<string> technologies =
        [
            .. clientCreations.Select(creation => GetSimpleName(creation.Type)),
            .. integrationNamespaces.Select(NamespaceFamily),
        ];
        facts.Add(DotNetEvidence.Fact(
            $"dotnet:integration:{path}",
            EvidenceKinds.Integration,
            projectScope,
            $"External-client or protocol integration syntax detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn client-type construction, invocation, and namespace classification",
            NormalizeLocations(
            [
                .. clientCreations.Select(creation =>
                    Location(path, creation, GetSimpleName(creation.Type))),
                .. calls.Select(call => Location(path, call, GetInvocationName(call))),
            ]),
            [
                DotNetEvidence.Measurement("client-constructions", clientCreations.Length, "calls"),
                DotNetEvidence.Measurement("integration-calls", calls.Length, "calls"),
                DotNetEvidence.Measurement(
                    "integration-namespaces",
                    integrationNamespaces.Length,
                    "namespaces"),
            ],
            technologies.Select(technology => $"technology:{technology.ToLowerInvariant()}")));
    }

    private static void AddSecurityFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes)
    {
        AttributeSyntax[] authorizationAttributes =
        [
            .. nodes.OfType<AttributeSyntax>().Where(attribute =>
                GetAttributeName(attribute) is "Authorize" or "AllowAnonymous"),
        ];
        InvocationExpressionSyntax[] configurationCalls =
        [
            .. nodes.OfType<InvocationExpressionSyntax>().Where(invocation =>
                AuthenticationInvocationNames.Contains(GetInvocationName(invocation))),
        ];
        if (authorizationAttributes.Length == 0 && configurationCalls.Length == 0)
        {
            return;
        }

        facts.Add(DotNetEvidence.Fact(
            $"dotnet:security:{path}",
            EvidenceKinds.SecurityConfiguration,
            projectScope,
            $"Authentication or authorization behavior detected in '{path}'.",
            EvidenceSourceKind.Observed,
            "Roslyn authorization-attribute and authentication-pipeline invocation syntax",
            NormalizeLocations(
            [
                .. authorizationAttributes.Select(attribute =>
                    Location(path, attribute, GetAttributeName(attribute))),
                .. configurationCalls.Select(call => Location(path, call, GetInvocationName(call))),
            ]),
            [
                DotNetEvidence.Measurement(
                    "authorization-attributes",
                    authorizationAttributes.Length,
                    "attributes"),
                DotNetEvidence.Measurement(
                    "security-configuration-calls",
                    configurationCalls.Length,
                    "calls"),
            ]));
    }

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

    private static bool IsIntegrationNamespace(string name) =>
        name.StartsWith("Amazon.", StringComparison.Ordinal) ||
        name.StartsWith("Azure.", StringComparison.Ordinal) ||
        name.StartsWith("Confluent.Kafka", StringComparison.Ordinal) ||
        name.StartsWith("Grpc.", StringComparison.Ordinal) ||
        name.StartsWith("MassTransit", StringComparison.Ordinal) ||
        name.StartsWith("RabbitMQ.", StringComparison.Ordinal) ||
        name.StartsWith("System.Net.Http", StringComparison.Ordinal);

    private static string NamespaceFamily(string name)
    {
        int separator = name.IndexOf('.');
        return separator < 0 ? name : name[..separator];
    }
}
