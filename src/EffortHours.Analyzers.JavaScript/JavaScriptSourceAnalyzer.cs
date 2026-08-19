using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal sealed class JavaScriptSourceAnalyzer(RepositoryTextReader textReader)
{
    private const long MaximumSourceBytes = 8 * 1024 * 1024;

    private readonly RepositoryTextReader _textReader = textReader;
    private readonly ConcurrentDictionary<string, string> _packageIdentities =
        new(StringComparer.Ordinal);

    public async Task<JavaScriptFileAnalysis> AnalyzeAsync(
        EvidenceFact fileFact,
        JavaScriptPackageModel? package,
        CancellationToken cancellationToken)
    {
        string path = fileFact.Scope;
        string? expectedSha256 = JavaScriptEvidence.FindTagValue(fileFact.Tags, "sha256:");
        string? artifactKey = expectedSha256 is null
            ? null
            : $"javascript-source/{JavaScriptEvidence.AnalyzerVersion}/{expectedSha256}/" +
                $"{path}/{PackageIdentity(package)}";
        RepositoryAnalysisArtifactCache? artifactCache = _textReader.AnalysisArtifactCache;
        if (artifactKey is not null && artifactCache is not null)
        {
            return await artifactCache.GetOrCreateAsync(
                artifactKey,
                itemCancellationToken => AnalyzeUncachedAsync(
                    fileFact,
                    package,
                    itemCancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await AnalyzeUncachedAsync(
            fileFact,
            package,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<JavaScriptFileAnalysis> AnalyzeUncachedAsync(
        EvidenceFact fileFact,
        JavaScriptPackageModel? package,
        CancellationToken cancellationToken)
    {
        string path = fileFact.Scope;
        RepositoryTextReadResult read = await _textReader.ReadAsync(
            fileFact,
            MaximumSourceBytes,
            "FB4101",
            cancellationToken).ConfigureAwait(false);
        if (read.Diagnostic is not null)
        {
            return new JavaScriptFileAnalysis(new JavaScriptSourceMetrics(), [], [read.Diagnostic], []);
        }

        using IDisposable cpuLease = await RepositoryAnalysisConcurrency
            .AcquireFileAnalysisAsync(
                RepositoryAnalysisWorkKind.SemanticFileAnalysis,
                cancellationToken)
            .ConfigureAwait(false);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        string source = extension is ".vue" or ".svelte"
            ? ExtractScriptBlocks(read.Text!)
            : read.Text!;
        JavaScriptTokenization tokens = JavaScriptLexer.Tokenize(source);
        bool isTypeScript = extension is ".ts" or ".tsx" or ".mts" or ".cts" ||
            (extension is ".vue" or ".svelte" && ContainsTypeScriptScriptTag(read.Text!));
        bool hasJsx = extension is ".jsx" or ".tsx";
        JavaScriptSyntaxResult syntax = JavaScriptSyntaxAnalyzer.Analyze(
            source,
            path,
            tokens,
            isTypeScript || extension is ".vue" or ".svelte",
            hasJsx);
        AddPackageTechnologies(package, syntax.Metrics);
        AddFrameworkFileSignals(extension, package, syntax.Metrics);
        bool isTestFile = fileFact.Tags.Contains("classification:test", StringComparer.Ordinal);
        IReadOnlyList<AngularComponentMetadata> angularComponents = isTestFile
            ? []
            : AngularComponentAnalyzer.Analyze(
                path,
                package?.Scope ?? ".",
                tokens);
        JavaScriptEntrypointClassifier.AddSignal(
            path,
            package,
            tokens,
            syntax.Metrics,
            isTestFile);

        List<EvidenceFact> facts = CreateFacts(path, package?.Scope ?? ".", fileFact, syntax);
        List<Diagnostic> diagnostics = [];
        if (syntax.ParseErrorLine is not null)
        {
            diagnostics.Add(JavaScriptEvidence.Diagnostic(
                "FB4102",
                DiagnosticSeverity.Warning,
                $"JavaScript syntax in '{path}' could not be fully parsed; bounded token evidence was retained.",
                path,
                syntax.ParseErrorLine));
        }

        return new JavaScriptFileAnalysis(
            syntax.Metrics,
            facts,
            diagnostics,
            angularComponents);
    }

    private string PackageIdentity(JavaScriptPackageModel? package)
    {
        if (package is null)
        {
            return "no-package";
        }

        return _packageIdentities.GetOrAdd(package.ManifestPath, _ =>
        {
            IEnumerable<string?> values =
            [
                package.ManifestPath,
                package.Scope,
                package.Name,
                package.Version,
                package.ModuleType,
                package.PackageManager,
                package.Role,
                package.IsPrivate.ToString(),
                package.HasBin.ToString(),
                package.HasLibraryExports.ToString(),
                .. package.Technologies.Order(StringComparer.Ordinal),
                .. package.ScriptNames.Order(StringComparer.Ordinal),
                .. package.Dependencies
                    .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
                    .ThenBy(dependency => dependency.Kind, StringComparer.Ordinal)
                    .Select(dependency =>
                        $"{dependency.Name}\0{dependency.Specifier}\0{dependency.Kind}"),
            ];
            string source = string.Join('\n', values);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
                .ToLowerInvariant();
        });
    }

    private static List<EvidenceFact> CreateFacts(
        string path,
        string packageScope,
        EvidenceFact fileFact,
        JavaScriptSyntaxResult syntax)
    {
        JavaScriptSourceMetrics metrics = syntax.Metrics;
        string syntaxTag = $"syntax:{syntax.Method}";
        List<EvidenceFact> facts = [];
        if (metrics.EntryPointLine is not null)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:entry-point:{path}",
                EvidenceKinds.EntryPoint,
                packageScope,
                $"JavaScript or TypeScript entry point detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "package role, conventional entry filename, or hashbang classification",
                [JavaScriptEvidence.Location(path, metrics.EntryPointLine)],
                tags: [syntaxTag]));
        }

        if (metrics.ApiEndpoints > 0 || metrics.ApiControllers > 0 || metrics.GraphQlOperations > 0)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:api:{path}",
                EvidenceKinds.ApiSurface,
                packageScope,
                $"Server route or API surface detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "AST-backed structure with framework call and decorator classification",
                [JavaScriptEvidence.Location(path, metrics.ApiLine)],
                [
                    JavaScriptEvidence.Measurement("endpoints", metrics.ApiEndpoints, "endpoints"),
                    JavaScriptEvidence.Measurement("controllers", metrics.ApiControllers, "controllers"),
                    JavaScriptEvidence.Measurement("graphql-operations", metrics.GraphQlOperations, "operations"),
                ],
                [syntaxTag, .. metrics.HttpMethods.Select(method => $"http-method:{method}")]));
        }

        JavaScriptUiEvidence.AddFact(facts, path, packageScope, syntaxTag, metrics);

        if (metrics.DataCalls > 0 || metrics.Migrations > 0)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:data:{path}",
                EvidenceKinds.DataAccess,
                packageScope,
                $"Data access, persistence, or migration structure detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "framework-aware call, decorator, and migration-path classification",
                [JavaScriptEvidence.Location(path, metrics.DataLine)],
                [
                    JavaScriptEvidence.Measurement("data-calls", metrics.DataCalls, "calls"),
                    JavaScriptEvidence.Measurement("migrations", metrics.Migrations, "files"),
                ],
                [syntaxTag, .. TechnologyTags(metrics, "data")]));
        }

        if (metrics.IntegrationCalls > 0)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:integration:{path}",
                EvidenceKinds.Integration,
                packageScope,
                $"External-client or protocol integration syntax detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "known client dependency and call-shape classification",
                [JavaScriptEvidence.Location(path, metrics.IntegrationLine)],
                [JavaScriptEvidence.Measurement("integration-calls", metrics.IntegrationCalls, "calls")],
                [syntaxTag, .. TechnologyTags(metrics, "integration")]));
        }

        if (metrics.SecurityUsages > 0)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:security:{path}",
                EvidenceKinds.SecurityConfiguration,
                packageScope,
                $"Authentication, authorization, or credential-handling structure detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "known security dependency, call, and decorator classification",
                [JavaScriptEvidence.Location(path, metrics.SecurityLine)],
                [JavaScriptEvidence.Measurement("security-usages", metrics.SecurityUsages, "usages")],
                [syntaxTag, .. TechnologyTags(metrics, "security")]));
        }

        if (metrics.ValidationUsages > 0)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:validation:{path}",
                EvidenceKinds.Validation,
                packageScope,
                $"Input or domain validation structure detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "known validation dependency, call, and decorator classification",
                [JavaScriptEvidence.Location(path, metrics.ValidationLine)],
                [JavaScriptEvidence.Measurement("validation-usages", metrics.ValidationUsages, "usages")],
                [syntaxTag, .. TechnologyTags(metrics, "validation")]));
        }

        if (metrics.BackgroundUsages > 0)
        {
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:background:{path}",
                EvidenceKinds.BackgroundWork,
                packageScope,
                $"Queue, scheduled, or background work structure detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "known background-work dependency, call, and decorator classification",
                [JavaScriptEvidence.Location(path, metrics.BackgroundLine)],
                [JavaScriptEvidence.Measurement("background-usages", metrics.BackgroundUsages, "usages")],
                [syntaxTag, .. TechnologyTags(metrics, "background")]));
        }

        bool isTest = fileFact.Tags.Contains("classification:test", StringComparer.Ordinal) ||
            metrics.TestCases > 0 || metrics.TestSuites > 0;
        if (isTest)
        {
            string testType = ClassifyTestType(metrics, path);
            facts.Add(JavaScriptEvidence.Fact(
                $"javascript:test:{path}",
                EvidenceKinds.JavaScriptTest,
                packageScope,
                $"{testType.Replace('-', ' ')} test structure detected in '{path}'.",
                EvidenceSourceKind.Inferred,
                "test path, framework, case, assertion, and browser or integration primitive classification",
                [JavaScriptEvidence.Location(path, metrics.TestLine ?? 1)],
                [
                    JavaScriptEvidence.Measurement("test-cases", metrics.TestCases, "cases"),
                    JavaScriptEvidence.Measurement("test-suites", metrics.TestSuites, "suites"),
                    JavaScriptEvidence.Measurement("assertions", metrics.Assertions, "calls"),
                    JavaScriptEvidence.Measurement("mock-usages", metrics.MockUsages, "usages"),
                    JavaScriptEvidence.Measurement("accessibility-checks", metrics.AccessibilityTestUsages, "checks"),
                ],
                [
                    syntaxTag,
                    $"test-type:{testType}",
                    TestEstimatorTag(testType),
                    .. AccessibilityTestTags(metrics),
                    .. TestTechnologyTags(metrics),
                ]));
        }

        return facts;
    }

    private static string ClassifyTestType(JavaScriptSourceMetrics metrics, string path)
    {
        string lowerPath = path.ToLowerInvariant();
        if (metrics.EndToEndTestUsages > 0 ||
            metrics.ObservedTechnologyFamilies.Contains("test-e2e") ||
            metrics.ObservedTechnologyFamilies.Contains("test-accessibility-e2e") ||
            lowerPath.Contains("/e2e/", StringComparison.Ordinal))
        {
            return "end-to-end";
        }

        if (metrics.IntegrationTestUsages > 0 ||
            metrics.ObservedTechnologyFamilies.Contains("test-integration") ||
            lowerPath.Contains("integration", StringComparison.Ordinal))
        {
            return "integration";
        }

        if (metrics.ComponentTestUsages > 0 ||
            metrics.ObservedTechnologyFamilies.Contains("test-component") ||
            metrics.ObservedTechnologyFamilies.Contains("test-accessibility-component"))
        {
            return "component";
        }

        return "unit";
    }

    private static string TestEstimatorTag(string testType) => testType switch
    {
        "end-to-end" => "e2e",
        "integration" or "component" => "integration",
        _ => "unit",
    };

    private static IEnumerable<string> TechnologyTags(
        JavaScriptSourceMetrics metrics,
        params string[] families) => metrics.TechnologyFamilies.Any(families.Contains)
            ? metrics.Technologies.Select(technology => $"technology:{technology}")
            : [];

    private static IEnumerable<string> TestTechnologyTags(JavaScriptSourceMetrics metrics) =>
        metrics.Technologies.Where(technology =>
            technology is "vitest" or "jest" or "mocha" or "jasmine" or "ava" or
                "playwright" or "cypress" or "puppeteer" or "webdriverio" or
                "supertest" or "testcontainers" or "pact" or "testing-library" or
                "jest-axe" or "axe-playwright")
            .Select(technology => $"technology:{technology}");

    private static IEnumerable<string> AccessibilityTestTags(JavaScriptSourceMetrics metrics) =>
        metrics.AccessibilityTestUsages > 0 ? ["test-focus:accessibility"] : [];

    private static void AddPackageTechnologies(
        JavaScriptPackageModel? package,
        JavaScriptSourceMetrics metrics)
    {
        if (package is null)
        {
            return;
        }

        foreach (JavaScriptDependency dependency in package.Dependencies)
        {
            TechnologyClassification? classification = JavaScriptTechnologyCatalog.Classify(dependency.Name);
            if (classification is not null)
            {
                metrics.Technologies.Add(classification.Name);
                metrics.TechnologyFamilies.Add(classification.Family);
            }
        }
    }

    private static void AddFrameworkFileSignals(
        string extension,
        JavaScriptPackageModel? package,
        JavaScriptSourceMetrics metrics)
    {
        if (extension is ".vue" or ".svelte")
        {
            metrics.UiComponents = Math.Max(metrics.UiComponents, 1);
            metrics.UiLine ??= 1;
            metrics.HasExplicitUiFile = true;
            string technology = extension[1..];
            metrics.Technologies.Add(technology);
            metrics.TechnologyFamilies.Add("ui");
        }

        if (extension is ".jsx" or ".tsx" && metrics.JsxElements > 0)
        {
            metrics.UiComponents = Math.Max(metrics.UiComponents, 1);
            metrics.UiLine ??= 1;
        }

        if (package?.Role is "web-ui" or "full-stack-web" && metrics.UiPages > 0)
        {
            metrics.UiLine ??= 1;
        }
    }

    private static string ExtractScriptBlocks(string source)
    {
        char[] result = [.. source.Select(character => character is '\r' or '\n' ? character : ' ')];
        int searchIndex = 0;
        while (searchIndex < source.Length)
        {
            int opening = source.IndexOf("<script", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (opening < 0)
            {
                break;
            }

            int contentStart = source.IndexOf('>', opening);
            if (contentStart < 0)
            {
                break;
            }

            contentStart++;
            int closing = source.IndexOf("</script", contentStart, StringComparison.OrdinalIgnoreCase);
            if (closing < 0)
            {
                closing = source.Length;
            }

            source.AsSpan(contentStart, closing - contentStart).CopyTo(result.AsSpan(contentStart));
            searchIndex = closing + 1;
        }

        return new string(result);
    }

    private static bool ContainsTypeScriptScriptTag(string source)
    {
        int searchIndex = 0;
        while (searchIndex < source.Length)
        {
            int opening = source.IndexOf("<script", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (opening < 0)
            {
                return false;
            }

            int end = source.IndexOf('>', opening);
            if (end < 0)
            {
                return false;
            }

            ReadOnlySpan<char> tag = source.AsSpan(opening, end - opening + 1);
            if (tag.Contains("lang=\"ts\"", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("lang='ts'", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            searchIndex = end + 1;
        }

        return false;
    }
}
