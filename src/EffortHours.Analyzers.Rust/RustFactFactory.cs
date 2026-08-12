using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Rust;

internal static class RustFactFactory
{
    public static EvidenceFact Package(
        CargoPackageModel package,
        IReadOnlyList<RustFileAnalysis> files)
    {
        bool declared = package.ManifestPaths.Count > 0;
        return RustEvidence.Fact(
            $"rust:package:{RustEvidence.IdToken(package.Directory)}",
            EvidenceKinds.EcosystemPackage,
            package.Directory,
            declared
                ? package.IsVirtualWorkspace
                    ? "Static virtual Cargo workspace metadata."
                    : $"Static Cargo package metadata for '{package.Name}'."
                : "Implicit repository-level Rust source scope without Cargo metadata.",
            declared ? EvidenceSourceKind.Observed : EvidenceSourceKind.Inferred,
            declared
                ? "common-scanner-admitted Cargo TOML parsed without Cargo execution"
                : "fallback scope derived from scanner-admitted maintained Rust source",
            declared
                ? package.ManifestPaths.Select(path => RustEvidence.Location(path))
                : files.Take(1).Select(file => RustEvidence.Location(file.File.Scope)),
            [
                RustEvidence.Measurement("source-files", files.Count, "files"),
                RustEvidence.Measurement("dependencies", package.Dependencies.Count, "packages"),
                RustEvidence.Measurement("features", package.Features, "features"),
                RustEvidence.Measurement("targets", package.Targets.Count, "targets"),
                RustEvidence.Measurement("local-projects", package.WorkspaceMembers.Count +
                    package.Dependencies.Count(item => item.PathDirectory is not null), "projects"),
            ],
            PackageTags(package, files, declared));
    }

    public static IEnumerable<EvidenceFact> Dependencies(CargoPackageModel package) =>
        package.Dependencies.Select(dependency => RustEvidence.Fact(
            $"rust:dependency:{RustEvidence.IdToken(package.Directory)}:{RustEvidence.IdToken(dependency.Kind)}:{RustEvidence.IdToken(dependency.Name)}",
            EvidenceKinds.PackageReference,
            package.Directory,
            $"Cargo {dependency.Kind} dependency '{dependency.Name}'.",
            EvidenceSourceKind.Observed,
            "static Cargo dependency declaration",
            package.ManifestPaths.Select(path => RustEvidence.Location(path)),
            tags:
            [
                "ecosystem:rust",
                $"dependency:{dependency.Name}",
                $"dependency-kind:{dependency.Kind}",
                dependency.PathDirectory is null ? "dependency-source:registry-or-unresolved" : "dependency-source:local-path",
                dependency.WorkspaceInherited ? "dependency:workspace-inherited" : "dependency:direct",
            ]));

    public static EvidenceFact? BuildConfiguration(CargoPackageModel package)
    {
        if (package.ManifestPaths.Count == 0) return null;
        return RustEvidence.Fact(
            $"rust:build:{RustEvidence.IdToken(package.Directory)}",
            EvidenceKinds.BuildConfiguration,
            package.Directory,
            package.IsWorkspace
                ? $"Static Cargo workspace, target, feature, dependency, and build configuration for '{package.Name}'."
                : $"Static Cargo target, feature, dependency, and build configuration for '{package.Name}'.",
            EvidenceSourceKind.Observed,
            "literal bounded Cargo TOML and conventional target paths without feature resolution or build execution",
            package.ManifestPaths.Select(path => RustEvidence.Location(path)),
            [
                RustEvidence.Measurement("targets", package.Targets.Count, "targets"),
                RustEvidence.Measurement("features", package.Features, "features"),
                RustEvidence.Measurement("build-scripts", package.BuildScripts, "scripts"),
                RustEvidence.Measurement("workspace-members", package.WorkspaceMembers.Count, "packages"),
                RustEvidence.Measurement("crate-types", package.CrateTypes, "types"),
                RustEvidence.Measurement("unresolved-values", package.UnresolvedValues, "values"),
            ],
            [
                "ecosystem:rust", "metadata:static-only", "cargo:not-invoked", "rustc:not-invoked",
                package.IsWorkspace ? "cargo:workspace" : "cargo:package",
                package.Features > 0 ? "feature-expansion:not-resolved" : "feature-expansion:not-declared",
                package.BuildScripts > 0 ? "build-script:not-executed" : "build-script:not-declared",
                package.UnresolvedValues > 0 ? "cargo-model:partially-unresolved" : "cargo-model:literal",
            ]);
    }

    public static EvidenceFact? Delivery(CargoPackageModel package)
    {
        int binaries = package.Targets.Count(target => target.Kind == "bin");
        int examples = package.Targets.Count(target => target.Kind == "example");
        if (binaries + examples + package.CrateTypes == 0) return null;
        return RustEvidence.Fact(
            $"rust:delivery:{RustEvidence.IdToken(package.Directory)}",
            EvidenceKinds.DeliveryAutomation,
            package.Directory,
            $"Static Cargo distributable target surface for '{package.Name}'.",
            EvidenceSourceKind.Observed,
            "Cargo target and crate-type declarations plus conventional target paths",
            package.ManifestPaths.Select(path => RustEvidence.Location(path)),
            [
                RustEvidence.Measurement("files", package.ManifestPaths.Count, "files"),
                RustEvidence.Measurement("binary-targets", binaries, "targets"),
                RustEvidence.Measurement("example-targets", examples, "targets"),
                RustEvidence.Measurement("crate-types", package.CrateTypes, "types"),
            ],
            ["ecosystem:rust", "delivery:cargo-targets", "cargo-publish:not-invoked"]);
    }

    public static EvidenceFact SourceStructure(
        CargoPackageModel package,
        IReadOnlyList<RustFileAnalysis> files)
    {
        RustSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return RustEvidence.Fact(
            $"rust:source:{RustEvidence.IdToken(package.Directory)}",
            EvidenceKinds.SourceStructure,
            package.Directory,
            $"Bounded token structure for Rust package '{package.Name}'.",
            EvidenceSourceKind.Measured,
            "bounded managed Rust tokenization and declaration analysis",
            files.Select(file => RustEvidence.Location(file.File.Scope)),
            [
                Measure("files", files.Count, "files"),
                Measure("modules", Sum(metrics, item => item.Modules), "modules"),
                Measure("imports", Sum(metrics, item => item.Uses), "imports"),
                Measure("internal-imports", Sum(metrics, item => item.InternalImports), "imports"),
                Measure("functions", Sum(metrics, item => item.Functions), "symbols"),
                Measure("methods", Sum(metrics, item => item.Methods), "symbols"),
                Measure("types", Sum(metrics, item => item.Types), "symbols"),
                Measure("structs", Sum(metrics, item => item.Structs), "symbols"),
                Measure("enums", Sum(metrics, item => item.Enums), "symbols"),
                Measure("traits", Sum(metrics, item => item.Traits), "symbols"),
                Measure("impls", Sum(metrics, item => item.Impls), "blocks"),
                Measure("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                Measure("generic-declarations", Sum(metrics, item => item.GenericDeclarations), "declarations"),
                Measure("lifetime-usages", Sum(metrics, item => item.LifetimeUsages), "usages"),
                Measure("async-units", Sum(metrics, item => item.AsyncUnits), "units"),
                Measure("await-points", Sum(metrics, item => item.AwaitPoints), "points"),
                Measure("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                Measure("unsafe-blocks", Sum(metrics, item => item.UnsafeBlocks), "blocks"),
                Measure("error-paths", Sum(metrics, item => item.ErrorPaths), "paths"),
                Measure("macro-definitions", Sum(metrics, item => item.MacroDefinitions), "macros"),
                Measure("macro-invocations", Sum(metrics, item => item.MacroInvocations), "macros"),
                Measure("attribute-macros", Sum(metrics, item => item.AttributeMacros), "attributes"),
                Measure("documentation-comments", Sum(metrics, item => item.DocumentationComments), "comments"),
            ],
            [
                "ecosystem:rust", "syntax:token-backed", "parser:bounded-managed-tokenizer",
                $"parser-confidence:{Confidence(files)}", "source-excerpts:not-emitted",
                "macro-expansion:not-performed", "borrow-checking:not-performed",
                "structure:production-only",
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(RustFileAnalysis file)
    {
        RustSourceMetrics metrics = file.Syntax.Metrics;
        string path = file.File.Scope;
        string token = RustEvidence.IdToken(path);
        EvidenceLocation[] location = [RustEvidence.Location(path)];
        string[] commonTags =
        [
            "ecosystem:rust", "syntax:token-backed",
            $"parser-confidence:{file.Syntax.Confidence}", "source-excerpts:not-emitted",
            .. TechnologyTags(metrics),
        ];
        bool testFile = IsTest(file);
        bool productionFile = !testFile && !file.Syntax.Metrics.BuildScript;

        if (productionFile && metrics.EntryPoints + metrics.CliCommands > 0)
            yield return Semantic("entry", EvidenceKinds.EntryPoint, "entry-point",
                "Cargo targets, main/build functions, and import-qualified CLI surfaces",
                "entry-points", Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces");
        if (productionFile && metrics.ApiSurfaces > 0)
            yield return Semantic("api", EvidenceKinds.ApiSurface, "API",
                "import-qualified Rust server or RPC framework usage", "endpoints", metrics.ApiSurfaces, "surfaces");
        if (productionFile && metrics.DataCalls > 0)
            yield return Semantic("data", EvidenceKinds.DataAccess, "persistence",
                "import-qualified Rust persistence crate usage", "data-calls", metrics.DataCalls, "calls");
        if (productionFile && metrics.IntegrationCalls > 0)
            yield return Semantic("integration", EvidenceKinds.Integration, "external integration",
                "import-qualified Rust HTTP, RPC, cloud, cache, or messaging usage",
                "integration-calls", metrics.IntegrationCalls, "calls");
        if (productionFile && metrics.SecurityUsages > 0)
            yield return Semantic("security", EvidenceKinds.SecurityConfiguration, "security",
                "import-qualified Rust cryptography, TLS, token, OAuth, or secret usage",
                "security-usages", metrics.SecurityUsages, "usages");
        if (productionFile && metrics.BackgroundUsages + metrics.ConcurrencyUsages > 0)
            yield return RustEvidence.Fact(
                $"rust:background:{token}", EvidenceKinds.BackgroundWork, file.Package.Directory,
                $"Import-qualified Rust background or concurrency surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "qualified task, thread, channel, synchronization, worker, or parallel usage",
                location,
                [
                    Measure("background-usages", metrics.BackgroundUsages, "usages"),
                    Measure("concurrency-usages", metrics.ConcurrencyUsages, "usages"),
                ],
                commonTags);
        if (productionFile && metrics.ValidationRules > 0)
            yield return Semantic("validation", EvidenceKinds.Validation, "validation",
                "import-qualified Rust validation crate usage", "validation-rules", metrics.ValidationRules, "rules");
        if (productionFile && metrics.FfiBoundaries > 0)
            yield return RustEvidence.Fact(
                $"rust:ffi:{token}", EvidenceKinds.Integration, file.Package.Directory,
                $"Static Rust foreign-function boundary in '{path}'.",
                EvidenceSourceKind.Inferred,
                "extern ABI, export/link attributes, or import-qualified FFI crate usage",
                location,
                [Measure("integration-calls", metrics.FfiBoundaries, "boundaries")],
                [.. commonTags, "integration-kind:ffi", "generated-bindings:not-expanded"]);

        if (testFile || metrics.TestCases + metrics.Benchmarks + metrics.Examples + metrics.DocumentationTests > 0)
        {
            string testType = TestType(path, metrics);
            yield return RustEvidence.Fact(
                $"rust:test:{token}", EvidenceKinds.EcosystemTest, file.Package.Directory,
                $"Static Rust {testType.Replace('-', ' ')} evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "test attributes, conventional target paths, assertion macros, benchmark/example targets, and documentation fences",
                location,
                [
                    Measure("test-cases", Math.Max(1, metrics.TestCases + metrics.DocumentationTests), "cases"),
                    Measure("parameterized-cases", metrics.ParameterizedCases, "cases"),
                    Measure("assertions", metrics.Assertions, "assertions"),
                    Measure("mock-usages", metrics.MockUsages, "usages"),
                    Measure("benchmarks", metrics.Benchmarks, "benchmarks"),
                    Measure("examples", metrics.Examples, "examples"),
                    Measure("documentation-tests", metrics.DocumentationTests, "tests"),
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
            string unit) => RustEvidence.Fact(
                $"rust:{id}:{token}", kind, file.Package.Directory,
                $"Import-qualified Rust {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [Measure(measurement, value, unit)], commonTags);
    }

    public static EvidenceFact ProjectReference(
        CargoPackageModel source,
        CargoPackageModel target,
        IReadOnlyList<RustFileAnalysis> imports,
        bool manifestReference,
        bool workspaceReference)
    {
        List<EvidenceLocation> locations = [.. imports.Select(file => RustEvidence.Location(file.File.Scope))];
        if (manifestReference || workspaceReference)
            locations.AddRange(source.ManifestPaths.Select(path => RustEvidence.Location(path)));
        return RustEvidence.Fact(
            $"rust:reference:{RustEvidence.IdToken(source.Directory)}:{RustEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            $"Cargo package '{source.Name}' references local package '{target.Name}'.",
            EvidenceSourceKind.Inferred,
            "literal Cargo workspace or local-path dependency metadata, with matching crate uses retained as locations",
            locations,
            [Measure("reference-edges", 1, "edges")],
            [
                "ecosystem:rust", $"target-scope:{target.Directory}",
                workspaceReference ? "reference-kind:workspace-member" :
                    manifestReference ? "reference-kind:build" : "reference-kind:import",
            ]);
    }

    private static IEnumerable<string> PackageTags(
        CargoPackageModel package,
        IReadOnlyList<RustFileAnalysis> files,
        bool declared)
    {
        yield return "ecosystem:rust";
        yield return $"package-role:{package.Role}";
        yield return "metadata:static-only";
        yield return "cargo:not-invoked";
        yield return "rustc:not-invoked";
        yield return "scope:analyzed";
        yield return declared ? package.IsVirtualWorkspace
            ? "cargo-workspace:virtual"
            : "cargo-package:declared"
            : "cargo-package:implicit-fallback";
        if (package.IsProcMacro) yield return "package:procedural-macro";
        if (files.Any(file => IsProduction(file) && file.Syntax.Metrics.PublicSymbols > 0))
            yield return "package:library-exports";
        foreach (string technology in package.Dependencies
            .Select(dependency => RustImportAnalysis.Technology(dependency.PackageName ?? dependency.Name))
            .Where(technology => technology is not null).Select(technology => technology!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            yield return $"technology:{technology}";
    }

    private static IEnumerable<string> TechnologyTags(RustSourceMetrics metrics) =>
        metrics.Technologies.Order(StringComparer.Ordinal).Select(technology => $"technology:{technology}");

    private static bool IsTest(RustFileAnalysis file) => file.Syntax.Metrics.IsTestTarget ||
        file.File.Tags.Contains("classification:test", StringComparer.Ordinal);

    private static bool IsProduction(RustFileAnalysis file) =>
        !IsTest(file) && !file.Syntax.Metrics.BuildScript;

    private static EvidenceMeasurement Measure(string name, decimal value, string unit) =>
        RustEvidence.Measurement(name, value, unit);

    private static int Sum(IEnumerable<RustSourceMetrics> metrics, Func<RustSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<RustFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static string TestType(string path, RustSourceMetrics metrics)
    {
        if (metrics.IntegrationTests > 0 || path.Split('/').Contains("tests", StringComparer.OrdinalIgnoreCase))
            return "integration";
        if (metrics.Benchmarks > 0) return "benchmark";
        if (metrics.Examples > 0) return "example";
        if (metrics.DocumentationTests > 0 && metrics.TestCases == 0) return "documentation";
        return "unit";
    }
}
