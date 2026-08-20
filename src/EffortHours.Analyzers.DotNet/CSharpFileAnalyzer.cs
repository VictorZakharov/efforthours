using System.Collections.Frozen;
using System.Security.Cryptography;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ContractDiagnosticSeverity = EffortHours.Contracts.V1.DiagnosticSeverity;

namespace EffortHours.Analyzers.DotNet;

internal sealed partial class CSharpFileAnalyzer(
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

    private static readonly FrozenSet<string> TestClassificationIdentifiers = new[]
    {
        "A", "ComponentTestFixture", "FakeItEasy", "IPage", "IPlaywright",
        "IRenderedComponent", "IWebDriver", "Mock", "PageTest", "Playwright",
        "RenderedFragment", "RenderComponent", "RenderComponentAsync", "Respawner",
        "Substitute", "TestcontainersBuilder", "TestServer", "WebApplicationFactory",
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
        if (artifactKey is not null && artifactCache is not null)
        {
            return await artifactCache.GetOrCreateAsync(
                artifactKey,
                itemCancellationToken => AnalyzeUncachedAsync(
                    fullPath,
                    relativePath,
                    contentId,
                    expectedSha256,
                    projectScope,
                    isTestFile,
                    itemCancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await AnalyzeUncachedAsync(
            fullPath,
            relativePath,
            contentId,
            expectedSha256,
            projectScope,
            isTestFile,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CSharpFileAnalysis> AnalyzeUncachedAsync(
        string fullPath,
        string relativePath,
        string? contentId,
        string expectedSha256,
        string projectScope,
        bool isTestFile,
        CancellationToken cancellationToken)
    {
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

        SourceText sourceText;
        CSharpSyntaxTree tree;
        int syntaxErrors;
        CSharpFileAnalysis analysis;
        using (IDisposable cpuLease = await RepositoryAnalysisConcurrency
            .AcquireFileAnalysisAsync(
                RepositoryAnalysisWorkKind.SemanticFileAnalysis,
                cancellationToken)
            .ConfigureAwait(false))
        {
            sourceText = CSharpEvidenceLineage.CreateSourceText(bytes);
            tree = CSharpEvidenceLineage.Parse(
                sourceText,
                relativePath,
                cancellationToken);
            syntaxErrors = CSharpEvidenceLineage.CountSyntaxErrors(tree, cancellationToken);
            analysis = AnalyzeParsed(
                tree,
                syntaxErrors,
                relativePath,
                projectScope,
                isTestFile,
                cancellationToken);
        }

        await CSharpEvidenceLineage.StoreAnalyzedVersionAsync(
            _fileSystem,
            fullPath,
            relativePath,
            contentId,
            sourceText,
            tree,
            syntaxErrors,
            cancellationToken).ConfigureAwait(false);
        return analysis;
    }

    private static void AddValidationFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        CSharpSyntaxInventory inventory)
    {
        AttributeSyntax[] attributes = [.. inventory.Attributes.Where(attribute => ValidationAttributes.Contains(GetAttributeName(attribute)))];
        BaseTypeDeclarationSyntax[] validators = [.. inventory.Types.Where(type => BaseTypeNames(type).Contains("AbstractValidator", StringComparer.Ordinal))];
        InvocationExpressionSyntax[] rules = [.. inventory.Invocations.Where(invocation => GetInvocationName(invocation) == "RuleFor")];
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
        CSharpSyntaxInventory inventory,
        bool isTestFile)
    {
        MethodDeclarationSyntax[] testMethods = [.. inventory.Methods.Where(method => AttributeNames(method.AttributeLists).Any(TestAttributes.Contains))];
        if (!isTestFile && testMethods.Length == 0)
        {
            return;
        }

        int dataCases = inventory.Attributes
            .Count(attribute => ParameterizedTestAttributes.Contains(GetAttributeName(attribute)));
        InvocationExpressionSyntax[] assertions = [.. inventory.Invocations.Where(invocation => AssertionMethods.Contains(GetInvocationName(invocation)))];
        string[] identifiers = [.. inventory.SimpleNames
            .Select(name => name.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal)];
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
        CSharpSyntaxInventory inventory)
    {
        BaseTypeDeclarationSyntax[] components = [.. inventory.Types
            .Where(type => BaseTypeNames(type).Any(name => name is
                "ComponentBase" or "PageModel" or "UserControl" or "Window"))];
        int parameters = inventory.Attributes
            .Count(attribute => GetAttributeName(attribute) is "Parameter" or "CascadingParameter");
        int commands = inventory.ObjectCreations
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

    internal static bool IsBranchPoint(SyntaxNode node) => node is
        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or
        DoStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or SwitchExpressionArmSyntax or
        SwitchSectionSyntax;

    internal static bool IsRelevantSimpleName(string name) =>
        CSharpDataEvidenceAnalyzer.IsDataPrimitiveName(name) ||
        TestClassificationIdentifiers.Contains(name);

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
