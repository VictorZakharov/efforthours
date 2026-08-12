using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Cpp;

internal static class CppFactFactory
{
    public static EvidenceFact Project(
        CppProjectModel project,
        IReadOnlyList<CppFileAnalysis> files)
    {
        bool declared = project.ManifestPaths.Count > 0;
        return CppEvidence.Fact(
            $"cpp:project:{CppEvidence.IdToken(project.Directory)}",
            EvidenceKinds.EcosystemPackage,
            project.Directory,
            declared
                ? "Static C/C++ project and target scope from bounded build metadata."
                : "Implicit repository-level C/C++ source scope without build metadata.",
            declared ? EvidenceSourceKind.Observed : EvidenceSourceKind.Inferred,
            declared
                ? "bounded literal CMake, Make, Meson, MSBuild, and supplemental package metadata"
                : "fallback scope derived from scanner-admitted maintained C/C++ source",
            declared
                ? project.ManifestPaths.Select(path => CppEvidence.Location(path))
                : files.Take(1).Select(file => CppEvidence.Location(file.File.Scope)),
            [
                Measure("source-files", files.Count, "files"),
                Measure("translation-units", files.Count(file => CppPath.IsSource(file.File.Scope)), "files"),
                Measure("headers", files.Count(file => CppPath.IsHeader(file.File.Scope)), "files"),
                Measure("targets", project.Targets, "targets"),
                Measure("dependencies", project.DependencyNames.Count, "packages"),
                Measure("local-projects", project.LocalReferences, "projects"),
            ],
            [
                "ecosystem:cpp", $"package-role:{project.Role}", "scope:analyzed", "metadata:static-only",
                declared ? "cpp-project:declared" : "cpp-project:implicit-fallback",
                "compiler:not-invoked", "build-system:not-invoked",
                .. Languages(project, files).Select(language => $"declared-language:{language}"),
                .. project.BuildSystems.Select(system => $"build-system:{system}"),
            ]);
    }

    public static EvidenceFact? BuildConfiguration(
        CppProjectModel project,
        IReadOnlyList<CppFileAnalysis> files)
    {
        CppPreprocessorAnalysis[] preprocessor = [.. files.Select(file => file.Syntax.Preprocessor)];
        if (project.ManifestPaths.Count == 0 && preprocessor.All(item => item.Directives == 0)) return null;
        return CppEvidence.Fact(
            $"cpp:build:{CppEvidence.IdToken(project.Directory)}",
            EvidenceKinds.BuildConfiguration,
            project.Directory,
            "Static C/C++ build, target, preprocessor, and standard-selection evidence.",
            EvidenceSourceKind.Measured,
            "bounded literal build readers and non-expanding preprocessor inventory",
            project.ManifestPaths.Select(path => CppEvidence.Location(path))
                .Concat(files.Where(file => file.Syntax.Preprocessor.Directives > 0)
                    .Select(file => CppEvidence.Location(file.File.Scope))),
            [
                Measure("files", project.ManifestPaths.Count, "files"),
                Measure("build-systems", project.BuildSystems.Count, "systems"),
                Measure("targets", project.Targets, "targets"),
                Measure("executables", project.Executables, "targets"),
                Measure("libraries", project.Libraries, "targets"),
                Measure("plugins", project.Plugins, "targets"),
                Measure("test-targets", project.TestTargets, "targets"),
                Measure("benchmark-targets", project.BenchmarkTargets, "targets"),
                Measure("include-roots", project.IncludeRoots.Count, "directories"),
                Measure("standards", project.Standards.Count, "standards"),
                Measure("configuration-variants", project.ConfigurationVariants, "variants"),
                Measure("compile-definitions", project.CompileDefinitions, "definitions"),
                Measure("generation-boundaries", project.GenerationSignals, "boundaries"),
                Measure("unresolved-values", project.UnresolvedValues, "values"),
                Measure("preprocessor-directives", Sum(preprocessor, item => item.Directives), "directives"),
                Measure("macro-definitions", Sum(preprocessor, item => item.MacroDefinitions), "macros"),
                Measure("conditional-groups", Sum(preprocessor, item => item.ConditionalGroups), "groups"),
                Measure("conditional-branches", Sum(preprocessor, item => item.ConditionalBranches), "branches"),
            ],
            [
                "ecosystem:cpp", "metadata:static-only", "compiler:not-invoked", "linker:not-invoked",
                "preprocessor:not-invoked", "macro-expansion:not-performed", "conditions:not-selected",
                project.UnresolvedValues > 0 ? "build-model:partially-unresolved" : "build-model:literal",
                .. Languages(project, files).Select(language => $"declared-language:{language}"),
                .. project.BuildSystems.Select(system => $"build-system:{system}"),
                .. project.Standards.Select(standard => $"declared-standard:{standard}"),
            ]);
    }

    public static EvidenceFact? Delivery(CppProjectModel project)
    {
        if (project.Executables + project.Libraries + project.Plugins + project.InstallRules == 0) return null;
        return CppEvidence.Fact(
            $"cpp:delivery:{CppEvidence.IdToken(project.Directory)}",
            EvidenceKinds.DeliveryAutomation,
            project.Directory,
            "Static C/C++ distributable target and install surface.",
            EvidenceSourceKind.Observed,
            "literal build target kinds and install rules without build execution",
            project.ManifestPaths.Select(path => CppEvidence.Location(path)),
            [
                Measure("binary-targets", project.Executables, "targets"),
                Measure("library-targets", project.Libraries, "targets"),
                Measure("plugin-targets", project.Plugins, "targets"),
                Measure("install-rules", project.InstallRules, "rules"),
            ],
            ["ecosystem:cpp", "delivery:native-targets", "packaging:not-invoked"]);
    }

    public static EvidenceFact SourceStructure(
        CppProjectModel project,
        IReadOnlyList<CppFileAnalysis> files)
    {
        CppSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return CppEvidence.Fact(
            $"cpp:source:{CppEvidence.IdToken(project.Directory)}",
            EvidenceKinds.SourceStructure,
            project.Directory,
            "Bounded token and declaration structure for maintained C/C++ source and headers.",
            EvidenceSourceKind.Measured,
            "bounded managed C/C++ tokenization, declaration recognition, and conditional-branch normalization",
            files.Select(file => CppEvidence.Location(file.File.Scope)),
            [
                Measure("files", files.Count, "files"),
                Measure("translation-units", Sum(metrics, item => item.TranslationUnits), "files"),
                Measure("headers", Sum(metrics, item => item.Headers), "files"),
                Measure("namespaces", Sum(metrics, item => item.Namespaces), "namespaces"),
                Measure("functions", Sum(metrics, item => item.Functions), "symbols"),
                Measure("methods", Sum(metrics, item => item.Methods), "symbols"),
                Measure("types", Sum(metrics, item => item.Types), "symbols"),
                Measure("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                Measure("templates", Sum(metrics, item => item.Templates), "declarations"),
                Measure("concepts", Sum(metrics, item => item.Concepts), "declarations"),
                Measure("lambdas", Sum(metrics, item => item.Lambdas), "expressions"),
                Measure("modules", Sum(metrics, item => item.Modules), "modules"),
                Measure("imports", Sum(metrics, item => item.Imports), "imports"),
                Measure("async-units", Sum(metrics, item => item.AsyncUnits), "units"),
                Measure("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                Measure("error-paths", Sum(metrics, item => item.ErrorPaths), "paths"),
                Measure("documentation-comments", Sum(metrics, item => item.DocumentationComments), "comments"),
            ],
            [
                "ecosystem:cpp", "syntax:token-backed", "parser:bounded-managed-declaration-recognizer",
                $"parser-confidence:{Confidence(files)}", "source-excerpts:not-emitted",
                "preprocessing:not-performed", "template-instantiation:not-performed",
                "header-fanout:not-multiplied", "structure:production-only",
                .. HeaderOwnershipTags(project, files),
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(CppFileAnalysis file)
    {
        CppSourceMetrics metrics = file.Syntax.Metrics;
        string path = file.File.Scope;
        string token = CppEvidence.IdToken(path);
        EvidenceLocation[] location = [CppEvidence.Location(path)];
        string[] commonTags =
        [
            "ecosystem:cpp", "syntax:token-backed", $"parser-confidence:{file.Syntax.Confidence}",
            "source-excerpts:not-emitted", .. metrics.Technologies.Order(StringComparer.Ordinal)
                .Select(technology => $"technology:{technology}"),
        ];
        bool test = IsTest(file);
        if (!test && metrics.EntryPoints + metrics.CliCommands > 0)
            yield return Semantic("entry", EvidenceKinds.EntryPoint, "entry point or CLI",
                "literal main function, executable target, or qualified CLI surface",
                "entry-points", Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces");
        if (!test && metrics.ApiSurfaces > 0)
            yield return Semantic("api", EvidenceKinds.ApiSurface, "server, HTTP, or RPC API",
                "include/dependency-qualified API types and call shapes", "endpoints", metrics.ApiSurfaces, "surfaces");
        if (!test && metrics.DataCalls > 0)
            yield return Semantic("data", EvidenceKinds.DataAccess, "persistence",
                "include/dependency-qualified database types and call shapes", "data-calls", metrics.DataCalls, "calls");
        if (!test && metrics.IntegrationCalls > 0)
            yield return Semantic("integration", EvidenceKinds.Integration, "external integration",
                "include/dependency-qualified protocol, cloud, device, or messaging calls",
                "integration-calls", metrics.IntegrationCalls, "calls");
        if (!test && metrics.SecurityUsages > 0)
            yield return Semantic("security", EvidenceKinds.SecurityConfiguration, "security",
                "include/dependency-qualified cryptography, TLS, or credential calls",
                "security-usages", metrics.SecurityUsages, "usages");
        if (!test && metrics.UiSurfaces > 0)
            yield return Semantic("ui", EvidenceKinds.UserInterface, "GUI or rendering",
                "include/dependency-qualified GUI and rendering types or calls",
                "ui-surfaces", metrics.UiSurfaces, "surfaces");
        if (!test && metrics.ConcurrencyUsages + metrics.AsyncUnits > 0)
            yield return CppEvidence.Fact(
                $"cpp:background:{token}", EvidenceKinds.BackgroundWork, file.Project.Directory,
                $"Static C/C++ concurrency or coroutine surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "standard-library-qualified thread, atomic, synchronization, or coroutine tokens",
                location,
                [
                    Measure("background-usages", Math.Max(1, metrics.AsyncUnits), "usages"),
                    Measure("concurrency-usages", metrics.ConcurrencyUsages, "usages"),
                ],
                commonTags);
        if (!test && metrics.ValidationRules > 0)
            yield return Semantic("validation", EvidenceKinds.Validation, "validation and error handling",
                "bounded validation call and error-path shapes", "validation-rules", metrics.ValidationRules, "rules");
        if (!test && metrics.FfiBoundaries > 0)
            yield return CppEvidence.Fact(
                $"cpp:ffi:{token}", EvidenceKinds.Integration, file.Project.Directory,
                $"Static C ABI, dynamic-library, or foreign-function boundary in '{path}'.",
                EvidenceSourceKind.Inferred,
                "extern C linkage or qualified dynamic library calls",
                location,
                [Measure("integration-calls", metrics.FfiBoundaries, "boundaries")],
                [.. commonTags, "integration-kind:ffi", "abi-compatibility:not-verified"]);

        if (test || metrics.TestCases + metrics.Benchmarks + metrics.FuzzTargets > 0)
        {
            string testType = metrics.FuzzTargets > 0 ? "fuzz" :
                metrics.Benchmarks > 0 ? "benchmark" :
                HasPathSegment(path, "e2e") || HasPathSegment(path, "end-to-end") ? "end-to-end" :
                HasPathSegment(path, "integration") ? "integration" : "unit";
            yield return CppEvidence.Fact(
                $"cpp:test:{token}", EvidenceKinds.EcosystemTest, file.Project.Directory,
                $"Static C/C++ {testType} evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "conventional test paths plus framework-qualified test, assertion, benchmark, and fuzz tokens",
                location,
                [
                    Measure("test-cases", Math.Max(1, metrics.TestCases), "cases"),
                    Measure("assertions", metrics.Assertions, "assertions"),
                    Measure("benchmarks", metrics.Benchmarks, "benchmarks"),
                    Measure("fuzz-targets", metrics.FuzzTargets, "targets"),
                ],
                [.. commonTags, $"test-type:{testType}", "tests:not-executed"]);
        }

        EvidenceFact Semantic(
            string id,
            string kind,
            string description,
            string method,
            string measurement,
            int value,
            string unit) => CppEvidence.Fact(
                $"cpp:{id}:{token}", kind, file.Project.Directory,
                $"Qualified C/C++ {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [Measure(measurement, value, unit)], commonTags);
    }

    public static EvidenceFact ProjectReference(
        CppProjectModel source,
        CppProjectModel target,
        IReadOnlyList<CppFileAnalysis> files,
        bool buildReference)
    {
        return CppEvidence.Fact(
            $"cpp:reference:{CppEvidence.IdToken(source.Directory)}:{CppEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            buildReference
                ? "Static local C/C++ project reference through literal build metadata and any maintained include edge."
                : "Static local C/C++ project reference through an unambiguous maintained include edge.",
            EvidenceSourceKind.Inferred,
            buildReference
                ? "literal local build target/subdirectory/project reference without build evaluation"
                : "literal repository-local include resolution without preprocessing",
            files.Select(file => CppEvidence.Location(file.File.Scope))
                .Concat(buildReference
                    ? source.ManifestPaths.Select(path => CppEvidence.Location(path))
                    : []),
            [Measure("reference-edges", 1, "edges")],
            [
                "ecosystem:cpp", $"target-scope:{target.Directory}",
                buildReference ? "reference-kind:build" : "reference-kind:include",
            ]);
    }

    private static bool IsTest(CppFileAnalysis file) =>
        file.File.Tags.Contains("classification:test", StringComparer.Ordinal) ||
        file.Syntax.Metrics.TestCases + file.Syntax.Metrics.Benchmarks + file.Syntax.Metrics.FuzzTargets > 0;

    private static bool HasPathSegment(string path, string value) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Contains(value, StringComparer.OrdinalIgnoreCase);

    private static EvidenceMeasurement Measure(string name, decimal value, string unit) =>
        CppEvidence.Measurement(name, value, unit);

    private static int Sum(IEnumerable<CppSourceMetrics> metrics, Func<CppSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static int Sum(IEnumerable<CppPreprocessorAnalysis> metrics, Func<CppPreprocessorAnalysis, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<CppFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static IReadOnlyList<string> Languages(
        CppProjectModel project,
        IReadOnlyList<CppFileAnalysis> files)
    {
        HashSet<string> languages = [.. project.DeclaredLanguages];
        foreach (CppFileAnalysis file in files)
        {
            string extension = Path.GetExtension(file.File.Scope).ToLowerInvariant();
            if (extension == ".c") languages.Add("c");
            else if (CppPath.IsSource(file.File.Scope)) languages.Add("cpp");
        }
        return [.. languages.Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> HeaderOwnershipTags(
        CppProjectModel project,
        IReadOnlyList<CppFileAnalysis> files)
    {
        if (!files.Any(file => Path.GetExtension(file.File.Scope)
            .Equals(".h", StringComparison.OrdinalIgnoreCase))) yield break;
        IReadOnlyList<string> languages = Languages(project, files);
        yield return languages.Count == 1
            ? $"c-header-language:{languages[0]}"
            : "c-header-language:unresolved-c-or-cpp";
    }
}
