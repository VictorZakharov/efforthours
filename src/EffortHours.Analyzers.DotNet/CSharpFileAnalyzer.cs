using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ContractDiagnostic = EffortHours.Contracts.V1.Diagnostic;
using ContractDiagnosticSeverity = EffortHours.Contracts.V1.DiagnosticSeverity;

namespace EffortHours.Analyzers.DotNet;

internal sealed class CSharpFileAnalyzer(
    IRepositoryFileSystem fileSystem,
    string rootPath)
{
    private static readonly FrozenSet<string> HttpAttributes = new[]
    {
        "HttpDelete", "HttpGet", "HttpHead", "HttpOptions", "HttpPatch", "HttpPost", "HttpPut",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> MinimalApiMethods = new[]
    {
        "MapDelete", "MapFallback", "MapGet", "MapMethods", "MapPatch", "MapPost", "MapPut",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TestAttributes = new[]
    {
        "Fact", "Theory", "Test", "TestCase", "TestMethod", "DataTestMethod",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ParameterizedTestAttributes = new[]
    {
        "Theory", "TestCase", "TestCaseSource", "DataRow", "DynamicData", "InlineData", "MemberData",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> AssertionMethods = new[]
    {
        "AreEqual", "AreNotEqual", "Contains", "DoesNotContain", "Empty", "Equal", "Equivalent",
        "False", "IsFalse", "IsNotNull", "IsNull", "IsTrue", "NotEmpty", "NotEqual", "NotNull",
        "Null", "Same", "Should", "Single", "That", "Throws", "ThrowsAsync", "True",
    }.ToFrozenSet(StringComparer.Ordinal);

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

    private static readonly FrozenSet<string> ValidationAttributes = new[]
    {
        "Compare", "CreditCard", "EmailAddress", "MaxLength", "MinLength", "Phone", "Range",
        "RegularExpression", "Required", "StringLength", "Url",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> DataInvocationNames = new[]
    {
        "Execute", "ExecuteAsync", "ExecuteScalar", "ExecuteSql", "ExecuteSqlRaw", "FromSql",
        "FromSqlRaw", "Query", "QueryAsync", "SaveChanges", "SaveChangesAsync",
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = fileSystem.GetFullPath(rootPath);

    public async Task<CSharpFileAnalysis> AnalyzeAsync(
        string relativePath,
        string expectedSha256,
        string projectScope,
        bool isTestFile,
        CancellationToken cancellationToken)
    {
        string fullPath = ToFullPath(relativePath);
        byte[] bytes;
        try
        {
            bytes = await _fileSystem.ReadAllBytesAsync(
                fullPath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(
                "FB3101",
                relativePath,
                $"Could not parse C# file '{relativePath}': repository content could not be read.");
        }

        string actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!actualSha256.Equals(expectedSha256, StringComparison.Ordinal))
        {
            return Failure(
                "FB3102",
                relativePath,
                $"C# file '{relativePath}' changed after common scanning; semantic evidence was skipped.");
        }

        string source;
        using (MemoryStream stream = new(bytes, writable: false))
        using (StreamReader reader = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true))
        {
            source = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        SourceText sourceText = SourceText.From(
            source,
            Encoding.UTF8,
            SourceHashAlgorithm.Sha256);
        CSharpParseOptions parseOptions = new(
            LanguageVersion.Preview,
            DocumentationMode.Parse,
            SourceCodeKind.Regular);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            sourceText,
            parseOptions,
            relativePath,
            cancellationToken);
        CompilationUnitSyntax root = (CompilationUnitSyntax)await tree.GetRootAsync(cancellationToken)
            .ConfigureAwait(false);
        SyntaxNode[] nodes = [.. root.DescendantNodes()];
        List<ContractDiagnostic> diagnostics = [];
        int syntaxErrors = tree.GetDiagnostics(cancellationToken)
            .Count(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        if (syntaxErrors > 0)
        {
            diagnostics.Add(DotNetEvidence.Diagnostic(
                "FB3100",
                ContractDiagnosticSeverity.Warning,
                $"Roslyn reported {syntaxErrors} syntax error(s) in '{relativePath}'; partial evidence was retained.",
                relativePath));
        }

        BaseTypeDeclarationSyntax[] types = [.. nodes.OfType<BaseTypeDeclarationSyntax>()];
        MethodDeclarationSyntax[] methods = [.. nodes.OfType<MethodDeclarationSyntax>()];
        CSharpStructureMetrics structure = new(
            Files: 1,
            Types: types.Length + nodes.OfType<DelegateDeclarationSyntax>().Count(),
            PublicTypes: types.Count(IsPublic) + nodes.OfType<DelegateDeclarationSyntax>().Count(IsPublic),
            Methods: methods.Length,
            PublicMethods: methods.Count(IsPublic),
            AsyncMethods: methods.Count(method => method.Modifiers.Any(SyntaxKind.AsyncKeyword)),
            BranchPoints: nodes.Count(IsBranchPoint));

        List<EvidenceFact> facts = [];
        AddEntryPointFact(facts, relativePath, projectScope, nodes, methods);
        AddApiFact(facts, relativePath, projectScope, nodes, types);
        AddDataFact(facts, relativePath, projectScope, nodes, types);
        AddBackgroundFact(facts, relativePath, projectScope, nodes, types, methods);
        AddIntegrationFact(facts, relativePath, projectScope, nodes, root);
        AddSecurityFact(facts, relativePath, projectScope, nodes);
        AddValidationFact(facts, relativePath, projectScope, nodes, types);
        AddTestFact(facts, relativePath, projectScope, nodes, methods, isTestFile);
        AddUiFact(facts, relativePath, projectScope, nodes, types);

        return new CSharpFileAnalysis(structure, facts, diagnostics);
    }

    private static void AddEntryPointFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<MethodDeclarationSyntax> methods)
    {
        GlobalStatementSyntax[] globalStatements = [.. nodes.OfType<GlobalStatementSyntax>()];
        MethodDeclarationSyntax[] mainMethods = [.. methods
            .Where(method => method.Identifier.ValueText == "Main" &&
                method.Modifiers.Any(SyntaxKind.StaticKeyword))];
        InvocationExpressionSyntax[] hostBuilders = [.. nodes.OfType<InvocationExpressionSyntax>().Where(invocation => GetInvocationName(invocation) is "CreateBuilder" or "CreateDefaultBuilder")];
        ObjectCreationExpressionSyntax[] rootCommands = [.. nodes.OfType<ObjectCreationExpressionSyntax>().Where(creation => GetSimpleName(creation.Type) == "RootCommand")];
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
                string[] methodVerbs = [.. AttributeNames(method.AttributeLists).Where(HttpAttributes.Contains)];
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

        InvocationExpressionSyntax[] minimalEndpoints = [.. nodes
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => MinimalApiMethods.Contains(GetInvocationName(invocation)))];
        foreach (InvocationExpressionSyntax endpoint in minimalEndpoints)
        {
            string method = GetInvocationName(endpoint);
            locations.Add(Location(path, endpoint, method));
            verbs.Add(method[3..].ToLowerInvariant());
        }

        int mapGroups = nodes.OfType<InvocationExpressionSyntax>()
            .Count(invocation => GetInvocationName(invocation) == "MapGroup");
        if (controllerCount == 0 && attributedEndpoints == 0 && minimalEndpoints.Length == 0 && mapGroups == 0)
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

    private static void AddDataFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types)
    {
        BaseTypeDeclarationSyntax[] contexts = [.. types.Where(type => BaseTypeNames(type).Contains("DbContext", StringComparer.Ordinal))];
        int dbSets = nodes.OfType<PropertyDeclarationSyntax>()
            .Count(property => GetSimpleName(property.Type) == "DbSet");
        BaseTypeDeclarationSyntax[] migrations = [.. types
            .Where(type => BaseTypeNames(type).Contains("Migration", StringComparer.Ordinal) ||
                path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase))];
        int entityConfigurations = types.Count(type =>
            BaseTypeNames(type).Contains("IEntityTypeConfiguration", StringComparer.Ordinal));
        InvocationExpressionSyntax[] dataCalls = [.. nodes.OfType<InvocationExpressionSyntax>().Where(invocation => DataInvocationNames.Contains(GetInvocationName(invocation)))];
        bool hasDataPrimitives = nodes.OfType<SimpleNameSyntax>()
            .Select(name => name.Identifier.ValueText)
            .Any(name => name is
                "DbContext" or "DbSet" or "IDbConnection" or "IDataReader" or "IQueryable");
        int repositoryTypes = hasDataPrimitives
            ? types.Count(type => GetDeclaredTypeName(type).EndsWith("Repository", StringComparison.Ordinal))
            : 0;
        if (contexts.Length == 0 && dbSets == 0 && migrations.Length == 0 &&
            entityConfigurations == 0 && repositoryTypes == 0 && dataCalls.Length == 0)
        {
            return;
        }

        List<EvidenceLocation> locations =
        [
            .. contexts.Select(context => Location(path, context, GetDeclaredTypeName(context))),
            .. migrations.Select(migration => Location(path, migration, GetDeclaredTypeName(migration))),
            .. dataCalls.Select(call => Location(path, call, GetInvocationName(call))),
        ];
        facts.Add(DotNetEvidence.Fact(
            $"dotnet:data:{path}",
            EvidenceKinds.DataAccess,
            projectScope,
            $"Data access or persistence structure detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn base-type, property-type, path, and invocation classification",
            NormalizeLocations(locations),
            [
                DotNetEvidence.Measurement("db-contexts", contexts.Length, "types"),
                DotNetEvidence.Measurement("db-sets", dbSets, "properties"),
                DotNetEvidence.Measurement("migrations", migrations.Length, "types"),
                DotNetEvidence.Measurement("entity-configurations", entityConfigurations, "types"),
                DotNetEvidence.Measurement("repository-types", repositoryTypes, "types"),
                DotNetEvidence.Measurement("data-calls", dataCalls.Length, "calls"),
            ]));
    }

    private static void AddBackgroundFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types,
        IReadOnlyList<MethodDeclarationSyntax> methods)
    {
        BaseTypeDeclarationSyntax[] hostedServices = [.. types.Where(type => BaseTypeNames(type).Any(name => name is "BackgroundService" or "IHostedService"))];
        BaseTypeDeclarationSyntax[] handlers = [.. types
            .Where(type => BaseTypeNames(type).Any(name => name is
                "IRequestHandler" or "INotificationHandler" or "IConsumer" or "IHandleMessages"))];
        MethodDeclarationSyntax[] functions = [.. methods.Where(method => AttributeNames(method.AttributeLists).Any(name => name is "Function" or "FunctionName"))];
        int registrations = nodes.OfType<InvocationExpressionSyntax>()
            .Count(invocation => GetInvocationName(invocation) == "AddHostedService");
        if (hostedServices.Length == 0 && handlers.Length == 0 && functions.Length == 0 && registrations == 0)
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
        ObjectCreationExpressionSyntax[] clientCreations = [.. nodes.OfType<ObjectCreationExpressionSyntax>().Where(creation => IntegrationTypeNames.Contains(GetSimpleName(creation.Type)))];
        InvocationExpressionSyntax[] calls = [.. nodes.OfType<InvocationExpressionSyntax>().Where(invocation => IntegrationInvocationNames.Contains(GetInvocationName(invocation)))];
        string[] integrationNamespaces = [.. root.Usings
            .Select(usingDirective => usingDirective.Name?.ToString())
            .Where(name => name is not null && IsIntegrationNamespace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
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
                .. clientCreations.Select(creation => Location(path, creation, GetSimpleName(creation.Type))),
                .. calls.Select(call => Location(path, call, GetInvocationName(call))),
            ]),
            [
                DotNetEvidence.Measurement("client-constructions", clientCreations.Length, "calls"),
                DotNetEvidence.Measurement("integration-calls", calls.Length, "calls"),
                DotNetEvidence.Measurement("integration-namespaces", integrationNamespaces.Length, "namespaces"),
            ],
            technologies.Select(technology => $"technology:{technology.ToLowerInvariant()}")));
    }

    private static void AddSecurityFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes)
    {
        AttributeSyntax[] authorizationAttributes = [.. nodes.OfType<AttributeSyntax>().Where(attribute => GetAttributeName(attribute) is "Authorize" or "AllowAnonymous")];
        InvocationExpressionSyntax[] configurationCalls = [.. nodes.OfType<InvocationExpressionSyntax>().Where(invocation => AuthenticationInvocationNames.Contains(GetInvocationName(invocation)))];
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
                .. authorizationAttributes.Select(attribute => Location(path, attribute, GetAttributeName(attribute))),
                .. configurationCalls.Select(call => Location(path, call, GetInvocationName(call))),
            ]),
            [
                DotNetEvidence.Measurement("authorization-attributes", authorizationAttributes.Length, "attributes"),
                DotNetEvidence.Measurement("security-configuration-calls", configurationCalls.Length, "calls"),
            ]));
    }

    private static void AddValidationFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types)
    {
        AttributeSyntax[] attributes = [.. nodes.OfType<AttributeSyntax>().Where(attribute => ValidationAttributes.Contains(GetAttributeName(attribute)))];
        BaseTypeDeclarationSyntax[] validators = [.. types.Where(type => BaseTypeNames(type).Contains("AbstractValidator", StringComparer.Ordinal))];
        InvocationExpressionSyntax[] rules = [.. nodes.OfType<InvocationExpressionSyntax>().Where(invocation => GetInvocationName(invocation) == "RuleFor")];
        if (attributes.Length == 0 && validators.Length == 0 && rules.Length == 0)
        {
            return;
        }

        facts.Add(DotNetEvidence.Fact(
            $"dotnet:validation:{path}",
            EvidenceKinds.Validation,
            projectScope,
            $"Input or domain validation structure detected in '{path}'.",
            EvidenceSourceKind.Observed,
            "Roslyn data-annotation, validator-base, and validation-rule syntax",
            NormalizeLocations(
            [
                .. attributes.Select(attribute => Location(path, attribute, GetAttributeName(attribute))),
                .. validators.Select(validator => Location(path, validator, GetDeclaredTypeName(validator))),
                .. rules.Select(rule => Location(path, rule, "RuleFor")),
            ]),
            [
                DotNetEvidence.Measurement("validation-attributes", attributes.Length, "attributes"),
                DotNetEvidence.Measurement("validator-types", validators.Length, "types"),
                DotNetEvidence.Measurement("validation-rules", rules.Length, "rules"),
            ]));
    }

    private static void AddTestFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<MethodDeclarationSyntax> methods,
        bool isTestFile)
    {
        MethodDeclarationSyntax[] testMethods = [.. methods.Where(method => AttributeNames(method.AttributeLists).Any(TestAttributes.Contains))];
        int dataCases = nodes.OfType<AttributeSyntax>()
            .Count(attribute => ParameterizedTestAttributes.Contains(GetAttributeName(attribute)));
        InvocationExpressionSyntax[] assertions = [.. nodes.OfType<InvocationExpressionSyntax>().Where(invocation => AssertionMethods.Contains(GetInvocationName(invocation)))];
        string[] identifiers = [.. nodes.OfType<SimpleNameSyntax>()
            .Select(name => name.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal)];
        if (!isTestFile && testMethods.Length == 0)
        {
            return;
        }

        bool pathIndicatesEndToEnd = path.Contains(
            "endtoend",
            StringComparison.OrdinalIgnoreCase) ||
            path.Split('/').Any(segment => segment.Equals(
                "e2e",
                StringComparison.OrdinalIgnoreCase));
        string testType = pathIndicatesEndToEnd || identifiers.Any(identifier => identifier is
            "IPage" or "IPlaywright" or "IWebDriver" or "PageTest" or "Playwright")
            ? "end-to-end"
            : identifiers.Any(identifier => identifier is
                "ComponentTestFixture" or "IRenderedComponent" or "RenderComponent" or
                "RenderComponentAsync" or "RenderedFragment")
                ? "component"
            : identifiers.Any(identifier => identifier is
                "TestServer" or "WebApplicationFactory" or "TestcontainersBuilder" or "Respawner")
                ? "integration"
                : "unit";
        int mockUsages = identifiers.Count(identifier => identifier is
            "Mock" or "Substitute" or "A" or "FakeItEasy");
        facts.Add(DotNetEvidence.Fact(
            $"dotnet:test:{path}",
            EvidenceKinds.DotNetTest,
            projectScope,
            $"{testType.Replace('-', ' ')} test structure detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn test-attribute, assertion, fixture, and framework-type classification",
            NormalizeLocations(
            [
                .. testMethods.Select(method => Location(path, method, method.Identifier.ValueText)),
                .. assertions.Select(assertion => Location(path, assertion, GetInvocationName(assertion))),
            ]),
            [
                DotNetEvidence.Measurement("test-methods", testMethods.Length, "methods"),
                DotNetEvidence.Measurement("parameterized-cases", dataCases, "cases"),
                DotNetEvidence.Measurement("assertions", assertions.Length, "calls"),
                DotNetEvidence.Measurement("mock-usages", mockUsages, "usages"),
            ],
            [$"test-type:{testType}"]));
    }

    private static void AddUiFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types)
    {
        BaseTypeDeclarationSyntax[] components = [.. types
            .Where(type => BaseTypeNames(type).Any(name => name is
                "ComponentBase" or "PageModel" or "UserControl" or "Window"))];
        int parameters = nodes.OfType<AttributeSyntax>()
            .Count(attribute => GetAttributeName(attribute) is "Parameter" or "CascadingParameter");
        int commands = nodes.OfType<ObjectCreationExpressionSyntax>()
            .Count(creation => GetSimpleName(creation.Type) is "Command" or "RootCommand");
        if (components.Length == 0 && parameters == 0 && commands == 0)
        {
            return;
        }

        facts.Add(DotNetEvidence.Fact(
            $"dotnet:ui:{path}",
            EvidenceKinds.UserInterface,
            projectScope,
            $".NET UI, page-model, component, or command surface detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn UI base-type, component-parameter, and command syntax",
            NormalizeLocations(components.Select(type =>
                Location(path, type, GetDeclaredTypeName(type)))),
            [
                DotNetEvidence.Measurement("ui-types", components.Length, "types"),
                DotNetEvidence.Measurement("component-parameters", parameters, "properties"),
                DotNetEvidence.Measurement("commands", commands, "commands"),
            ]));
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

    private static bool IsPublic(BaseTypeDeclarationSyntax declaration) =>
        declaration.Modifiers.Any(SyntaxKind.PublicKeyword);

    private static bool IsPublic(DelegateDeclarationSyntax declaration) =>
        declaration.Modifiers.Any(SyntaxKind.PublicKeyword);

    private static bool IsPublic(MethodDeclarationSyntax declaration) =>
        declaration.Modifiers.Any(SyntaxKind.PublicKeyword);

    private static bool IsBranchPoint(SyntaxNode node) => node is
        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or
        DoStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or SwitchExpressionArmSyntax or
        SwitchSectionSyntax;

    private static EvidenceLocation Location(
        string path,
        SyntaxNode node,
        string? symbol = null) => DotNetEvidence.Location(path, GetLine(node), symbol);

    private static int GetLine(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static EvidenceLocation[] NormalizeLocations(IEnumerable<EvidenceLocation> locations) =>
        [.. locations
            .Distinct()
            .OrderBy(location => location.Line ?? int.MaxValue)
            .ThenBy(location => location.Symbol, StringComparer.Ordinal)
            .Take(50)];

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

    private static CSharpFileAnalysis Failure(string code, string path, string message) => new(
        new CSharpStructureMetrics(0, 0, 0, 0, 0, 0, 0),
        [],
        [DotNetEvidence.Diagnostic(code, ContractDiagnosticSeverity.Warning, message, path)]);

    private string ToFullPath(string relativePath) => Path.GetFullPath(Path.Combine(
        _rootPath,
        relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
