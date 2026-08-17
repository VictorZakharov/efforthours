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

    private static readonly FrozenSet<string> ValidationAttributes = new[]
    {
        "Compare", "CreditCard", "EmailAddress", "MaxLength", "MinLength", "Phone", "Range",
        "RegularExpression", "Required", "StringLength", "Url",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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
        RepositoryAnalysisArtifactCache? artifactCache =
            (_fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache;
        string? contentId = null;
        try
        {
            contentId = _fileSystem.GetFileMetadata(fullPath).ContentId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        string? artifactKey = contentId is null
            ? null
            : AnalysisArtifactKey(
                contentId,
                expectedSha256,
                relativePath,
                projectScope,
                isTestFile);
        if (artifactKey is not null &&
            artifactCache?.TryGet(artifactKey, out CSharpFileAnalysis cached) == true)
        {
            return cached;
        }

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

        CSharpReachabilityResult reachability = syntaxErrors == 0
            ? CSharpReachabilityAnalyzer.Analyze(root, relativePath, projectScope)
            : CSharpReachabilityResult.Empty;
        SyntaxNode[] nodes =
        [
            .. root.DescendantNodes().Where(node => !reachability.IsExcluded(node)),
        ];

        BaseTypeDeclarationSyntax[] types = [.. nodes.OfType<BaseTypeDeclarationSyntax>()];
        MethodDeclarationSyntax[] methods = [.. nodes.OfType<MethodDeclarationSyntax>()];
        CSharpStructureMetrics structure = new(
            Files: 1,
            Types: types.Length + nodes.OfType<DelegateDeclarationSyntax>().Count(),
            PublicTypes: types.Count(IsPublic) + nodes.OfType<DelegateDeclarationSyntax>().Count(IsPublic),
            Methods: methods.Length,
            PublicMethods: methods.Count(IsPublic),
            AsyncMethods: methods.Count(method => method.Modifiers.Any(SyntaxKind.AsyncKeyword)),
            BranchPoints: nodes.Count(IsBranchPoint),
            StructuralParserBackedFiles: syntaxErrors == 0 ? 1 : 0,
            StructuralDetectedCallables: CSharpCallableStructuralAnalyzer.CountDetected(methods),
            CallableStructuralMetrics: syntaxErrors == 0
                ? CSharpCallableStructuralAnalyzer.Analyze(methods)
                : []);

        List<EvidenceFact> facts = [];
        if (reachability.ExclusionFact is not null)
        {
            facts.Add(reachability.ExclusionFact);
        }

        CSharpApplicationBoundaryAnalyzer.AddFacts(
            facts,
            relativePath,
            projectScope,
            nodes,
            types,
            methods);
        CSharpDataEvidenceAnalyzer.AddFact(facts, relativePath, projectScope, nodes, types);
        CSharpServiceBoundaryAnalyzer.AddFacts(
            facts,
            relativePath,
            projectScope,
            nodes,
            types,
            methods,
            root);
        AddValidationFact(facts, relativePath, projectScope, nodes, types);
        AddTestFact(facts, relativePath, projectScope, nodes, methods, isTestFile);
        AddUiFact(facts, relativePath, projectScope, nodes, types);

        CSharpFileAnalysis result = new(structure, facts, diagnostics);
        if (artifactKey is not null)
        {
            artifactCache?.Add(artifactKey, result);
        }

        return result;
    }

    private static string AnalysisArtifactKey(
        string contentId,
        string expectedSha256,
        string relativePath,
        string projectScope,
        bool isTestFile)
    {
        string identity = string.Join(
            '\0',
            contentId,
            expectedSha256,
            relativePath,
            projectScope,
            isTestFile ? "test" : "source");
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"dotnet-csharp/{DotNetEvidence.AnalyzerVersion}/{digest}";
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

    private static CSharpFileAnalysis Failure(string code, string path, string message) => new(
        new CSharpStructureMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, []),
        [],
        [DotNetEvidence.Diagnostic(code, ContractDiagnosticSeverity.Warning, message, path)]);

    private string ToFullPath(string relativePath) => Path.GetFullPath(Path.Combine(
        _rootPath,
        relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
