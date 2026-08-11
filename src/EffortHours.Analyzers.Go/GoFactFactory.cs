using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Go;

internal static class GoFactFactory
{
    public static EvidenceFact Module(
        GoModuleModel module,
        IReadOnlyList<GoFileAnalysis> files)
    {
        bool declared = module.ManifestPaths.Count > 0;
        IReadOnlyList<EvidenceLocation> locations = module.ManifestPaths.Count > 0
            ? [.. module.ManifestPaths.Select(path => GoEvidence.Location(path))]
            : [.. files.Take(1).Select(file => GoEvidence.Location(file.File.Scope))];
        return GoEvidence.Fact(
            $"go:module:{GoEvidence.IdToken(module.Directory)}",
            EvidenceKinds.EcosystemPackage,
            module.Directory,
            declared
                ? $"Static Go module metadata for '{module.ModulePath}'."
                : "Implicit repository-level Go source scope without go.mod metadata.",
            declared ? EvidenceSourceKind.Observed : EvidenceSourceKind.Inferred,
            declared
                ? "common-scanner-admitted go.mod metadata parsed without Go toolchain execution"
                : "fallback scope derived from scanner-admitted maintained Go source",
            locations,
            [
                GoEvidence.Measurement("source-files", files.Count, "files"),
                GoEvidence.Measurement("packages", files.Select(file => file.Directory).Distinct(StringComparer.Ordinal).Count(), "packages"),
                GoEvidence.Measurement("dependencies", module.Dependencies.Count, "modules"),
                GoEvidence.Measurement("local-replacements", module.LocalReplacements.Count, "replacements"),
            ],
            ModuleTags(module, files, declared));
    }

    public static IEnumerable<EvidenceFact> Packages(
        GoModuleModel module,
        IReadOnlyList<GoFileAnalysis> files) => files
        .GroupBy(file => file.Directory, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => GoEvidence.Fact(
            $"go:package:{GoEvidence.IdToken(group.Key)}",
            EvidenceKinds.EcosystemPackage,
            group.Key,
            $"Static Go package '{PackageName(group)}' in module '{module.ModulePath}'.",
            EvidenceSourceKind.Observed,
            "package declarations grouped by scanner-admitted directory",
            group.Select(file => GoEvidence.Location(file.File.Scope)),
            [
                GoEvidence.Measurement("source-files", group.Count(), "files"),
                GoEvidence.Measurement("test-files", group.Count(file => IsTest(file.File)), "files"),
            ],
            ["ecosystem:go", "go-scope:package", "scope:inventory"]));

    public static IEnumerable<EvidenceFact> Dependencies(GoModuleModel module) =>
        module.Dependencies.Select(dependency => GoEvidence.Fact(
            $"go:dependency:{GoEvidence.IdToken(module.Directory)}:{GoEvidence.IdToken(dependency)}",
            EvidenceKinds.PackageReference,
            module.Directory,
            $"Go module dependency '{dependency}'.",
            EvidenceSourceKind.Observed,
            "static go.mod require or replace directive",
            module.ManifestPaths.Select(path => GoEvidence.Location(path)),
            tags: ["ecosystem:go", $"dependency:{dependency}"]));

    public static EvidenceFact Workspace(GoWorkspaceModel workspace) => GoEvidence.Fact(
        $"go:workspace:{GoEvidence.IdToken(workspace.Path)}",
        EvidenceKinds.BuildConfiguration,
        GoPath.Directory(workspace.Path),
        "Static Go workspace module selection.",
        EvidenceSourceKind.Observed,
        "literal go.work use and local replace directives",
        [GoEvidence.Location(workspace.Path)],
        [
            GoEvidence.Measurement("workspace-modules", workspace.ModuleDirectories.Count, "modules"),
            GoEvidence.Measurement("local-replacements", workspace.LocalReplacements.Count, "replacements"),
        ],
        ["ecosystem:go", "go-workspace:static-only", "go-toolchain:not-invoked"]);

    public static EvidenceFact SourceStructure(
        GoModuleModel module,
        IReadOnlyList<GoFileAnalysis> files)
    {
        GoSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return GoEvidence.Fact(
            $"go:source:{GoEvidence.IdToken(module.Directory)}",
            EvidenceKinds.SourceStructure,
            module.Directory,
            $"Bounded token structure for Go module '{module.ModulePath}'.",
            EvidenceSourceKind.Measured,
            "bounded managed Go tokenization and declaration analysis",
            files.Select(file => GoEvidence.Location(file.File.Scope)),
            [
                GoEvidence.Measurement("files", files.Count, "files"),
                GoEvidence.Measurement("packages", files.Select(file => file.Directory).Distinct(StringComparer.Ordinal).Count(), "packages"),
                GoEvidence.Measurement("functions", Sum(metrics, item => item.Functions), "symbols"),
                GoEvidence.Measurement("methods", Sum(metrics, item => item.Methods), "symbols"),
                GoEvidence.Measurement("types", Sum(metrics, item => item.Types), "symbols"),
                GoEvidence.Measurement("interfaces", Sum(metrics, item => item.Interfaces), "symbols"),
                GoEvidence.Measurement("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                GoEvidence.Measurement("generic-declarations", Sum(metrics, item => item.GenericDeclarations), "symbols"),
                GoEvidence.Measurement("async-units", Sum(metrics, item => item.AsyncUnits), "symbols"),
                GoEvidence.Measurement("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                GoEvidence.Measurement("error-paths", Sum(metrics, item => item.ErrorPaths), "paths"),
                GoEvidence.Measurement("goroutines", Sum(metrics, item => item.Goroutines), "statements"),
                GoEvidence.Measurement("channel-usages", Sum(metrics, item => item.ChannelUsages), "operations"),
                GoEvidence.Measurement("synchronization-usages", Sum(metrics, item => item.SynchronizationUsages), "calls"),
                GoEvidence.Measurement("imports", Sum(metrics, item => item.Imports), "imports"),
                GoEvidence.Measurement("internal-imports", Sum(metrics, item => item.InternalImports), "imports"),
            ],
            [
                "ecosystem:go",
                "syntax:token-backed",
                "parser:bounded-managed-tokenizer",
                $"parser-confidence:{Confidence(files)}",
                "source-excerpts:not-emitted",
            ]);
    }

    public static EvidenceFact? BuildSemantics(
        GoModuleModel module,
        IReadOnlyList<GoFileAnalysis> files)
    {
        GoSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        int buildConstraints = Sum(metrics, item => item.BuildConstraints);
        int platformFiles = Sum(metrics, item => item.PlatformFiles);
        int embeds = Sum(metrics, item => item.EmbedDirectives);
        int generators = Sum(metrics, item => item.CodeGenerationDirectives);
        int cgo = Sum(metrics, item => item.CgoFiles);
        int blankImports = Sum(metrics, item => item.BlankImports);
        if (buildConstraints + platformFiles + embeds + generators + cgo + blankImports == 0) return null;
        return GoEvidence.Fact(
            $"go:build:{GoEvidence.IdToken(module.Directory)}",
            EvidenceKinds.BuildConfiguration,
            module.Directory,
            $"Static Go build-constraint and compile-time integration evidence for '{module.ModulePath}'.",
            EvidenceSourceKind.Measured,
            "compiler directives, platform filename conventions, and import forms",
            files.Where(file => HasBuildSemantics(file.Syntax.Metrics))
                .Select(file => GoEvidence.Location(file.File.Scope)),
            [
                GoEvidence.Measurement("build-constraints", buildConstraints, "constraints"),
                GoEvidence.Measurement("platform-files", platformFiles, "files"),
                GoEvidence.Measurement("embed-directives", embeds, "directives"),
                GoEvidence.Measurement("code-generation-directives", generators, "directives"),
                GoEvidence.Measurement("cgo-files", cgo, "files"),
                GoEvidence.Measurement("blank-imports", blankImports, "imports"),
            ],
            [
                "ecosystem:go",
                "build-constraints:not-evaluated",
                "go-generate:not-executed",
                "go-embed:patterns-not-expanded",
                "cgo:not-compiled",
                "runtime-registration:not-proven",
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(GoFileAnalysis file)
    {
        GoSourceMetrics metrics = file.Syntax.Metrics;
        string path = file.File.Scope;
        string token = GoEvidence.IdToken(path);
        EvidenceLocation[] location = [GoEvidence.Location(path)];
        string[] commonTags =
        [
            "ecosystem:go", "syntax:token-backed",
            $"parser-confidence:{file.Syntax.Confidence}", "source-excerpts:not-emitted",
        ];

        if (metrics.EntryPoints + metrics.CliCommands > 0)
            yield return SemanticFact("entry", EvidenceKinds.EntryPoint,
                "application or command entry", "package-main, main function, or import-qualified CLI evidence",
                "entry-points", Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces",
                TechnologyTags(metrics, "flag", "cobra", "urfave/cli"));
        if (metrics.ApiEndpoints + metrics.ApiTypes > 0)
            yield return GoEvidence.Fact($"go:api:{token}", EvidenceKinds.ApiSurface, file.Module.Directory,
                $"Import-qualified Go API surface in '{path}'.", EvidenceSourceKind.Inferred,
                "qualified standard-library or framework route registration", location,
                [GoEvidence.Measurement("endpoints", metrics.ApiEndpoints, "routes"), GoEvidence.Measurement("controllers", metrics.ApiTypes, "types")],
                [.. commonTags, .. TechnologyTags(metrics, "net/http", "gin", "echo", "chi", "gorilla/mux")]);
        if (metrics.DataCalls + metrics.DataModels + metrics.Migrations > 0)
            yield return GoEvidence.Fact($"go:data:{token}", EvidenceKinds.DataAccess, file.Module.Directory,
                $"Import-qualified Go persistence surface in '{path}'.", EvidenceSourceKind.Inferred,
                "qualified database, ORM, and migration calls", location,
                [GoEvidence.Measurement("db-sets", metrics.DataModels, "models"), GoEvidence.Measurement("migrations", metrics.Migrations, "files"), GoEvidence.Measurement("data-calls", metrics.DataCalls, "calls")],
                [.. commonTags, .. TechnologyTags(metrics, "database/sql", "gorm", "sqlx", "ent", "migration")]);
        if (metrics.IntegrationCalls > 0)
            yield return SemanticFact("integration", EvidenceKinds.Integration, "external integration",
                "qualified HTTP, RPC, cloud, queue, or protocol client calls", "integration-calls",
                metrics.IntegrationCalls, "calls", TechnologyTags(metrics, "net/http", "grpc", "aws", "google-cloud", "azure", "kafka", "nats"));
        if (metrics.SecurityUsages > 0)
            yield return SemanticFact("security", EvidenceKinds.SecurityConfiguration, "security",
                "qualified cryptographic, token, or OAuth library usage", "security-usages",
                metrics.SecurityUsages, "calls", TechnologyTags(metrics, "crypto", "x/crypto", "jwt", "oauth2"));
        if (metrics.BackgroundUsages > 0)
            yield return SemanticFact("background", EvidenceKinds.BackgroundWork, "background-work",
                "qualified scheduler, queue, or workflow SDK usage", "background-usages",
                metrics.BackgroundUsages, "calls", TechnologyTags(metrics, "cron", "asynq", "temporal"));
        if (metrics.ValidationRules > 0)
            yield return SemanticFact("validation", EvidenceKinds.Validation, "validation",
                "qualified validator usage", "validation-rules", metrics.ValidationRules, "rules",
                TechnologyTags(metrics, "validator"));

        if (IsTest(file.File) || metrics.TestCases + metrics.Benchmarks + metrics.Examples + metrics.FuzzTests > 0)
        {
            string testType = TestType(path);
            yield return GoEvidence.Fact($"go:test:{token}", EvidenceKinds.EcosystemTest,
                file.Module.Directory, $"Static Go {testType.Replace('-', ' ')} test evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "_test.go classification, testing declarations, subtests, assertions, examples, fuzz, and benchmark evidence",
                location,
                [
                    GoEvidence.Measurement("test-cases", Math.Max(1, metrics.TestCases), "cases"),
                    GoEvidence.Measurement("benchmarks", metrics.Benchmarks, "cases"),
                    GoEvidence.Measurement("examples", metrics.Examples, "cases"),
                    GoEvidence.Measurement("fuzz-tests", metrics.FuzzTests, "cases"),
                    GoEvidence.Measurement("parameterized-cases", metrics.TableDrivenCases, "cases"),
                    GoEvidence.Measurement("assertions", metrics.Assertions, "assertions"),
                    GoEvidence.Measurement("mock-usages", metrics.MockUsages, "calls"),
                ],
                [.. commonTags, $"test-type:{testType}", .. TechnologyTags(metrics, "testify", "go-cmp", "gomock")]);
        }

        EvidenceFact SemanticFact(
            string id,
            string kind,
            string description,
            string method,
            string measurement,
            int value,
            string unit,
            IEnumerable<string> technologyTags) => GoEvidence.Fact(
                $"go:{id}:{token}", kind, file.Module.Directory,
                $"Import-qualified Go {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [GoEvidence.Measurement(measurement, value, unit)],
                [.. commonTags, .. technologyTags]);
    }

    public static EvidenceFact ProjectReference(
        GoModuleModel source,
        GoModuleModel target,
        IReadOnlyList<GoFileAnalysis> importEvidence,
        bool localReplacement)
    {
        List<EvidenceLocation> locations = [.. importEvidence.Select(file => GoEvidence.Location(file.File.Scope))];
        if (localReplacement) locations.AddRange(source.ManifestPaths.Select(path => GoEvidence.Location(path)));
        return GoEvidence.Fact(
            $"go:reference:{GoEvidence.IdToken(source.Directory)}:{GoEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            $"Go module '{source.ModulePath}' references local module '{target.ModulePath}'.",
            EvidenceSourceKind.Inferred,
            "module import root or static local replacement matched another discovered module",
            locations,
            [GoEvidence.Measurement("reference-edges", 1, "edges")],
            ["ecosystem:go", $"target-scope:{target.Directory}", localReplacement ? "reference-kind:local-replace" : "reference-kind:import"]);
    }

    private static string PackageName(IEnumerable<GoFileAnalysis> files) => files
        .Select(file => file.Syntax.Metrics.PackageName)
        .FirstOrDefault(name => name.Length > 0) ?? "unknown";

    private static bool IsTest(EvidenceFact file) =>
        file.Tags.Contains("classification:test", StringComparer.Ordinal);

    private static int Sum(IEnumerable<GoSourceMetrics> metrics, Func<GoSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<GoFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static bool HasBuildSemantics(GoSourceMetrics metrics) =>
        metrics.BuildConstraints + metrics.PlatformFiles + metrics.EmbedDirectives +
        metrics.CodeGenerationDirectives + metrics.CgoFiles + metrics.BlankImports > 0;

    private static IEnumerable<string> ModuleTags(
        GoModuleModel module,
        IReadOnlyList<GoFileAnalysis> files,
        bool declared)
    {
        yield return "ecosystem:go";
        yield return $"package-role:{module.Role}";
        yield return "metadata:static-only";
        yield return "go-toolchain:not-invoked";
        yield return "scope:analyzed";
        yield return declared ? "go-module:declared" : "go-module:implicit-fallback";
        if (files.Any(file => !IsTest(file.File) &&
            file.Syntax.Metrics.EntryPoints + file.Syntax.Metrics.CliCommands > 0))
            yield return "package:cli-bin";
        if (files.Any(file => !IsTest(file.File) && file.Syntax.Metrics.PublicSymbols > 0))
            yield return "package:library-exports";
    }

    private static IEnumerable<string> TechnologyTags(GoSourceMetrics metrics, params string[] allowed) =>
        metrics.Technologies.Where(allowed.Contains).Order(StringComparer.Ordinal)
            .Select(item => $"technology:{item}");

    private static string TestType(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal) || lower.Contains("end_to_end", StringComparison.Ordinal)) return "end-to-end";
        if (lower.Contains("integration", StringComparison.Ordinal) || lower.Contains("testdata", StringComparison.Ordinal)) return "integration";
        return "unit";
    }
}
