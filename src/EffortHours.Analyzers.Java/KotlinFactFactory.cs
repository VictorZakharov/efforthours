using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal static class KotlinFactFactory
{
    public static EvidenceFact Project(
        JavaProjectModel project,
        IReadOnlyList<KotlinFileAnalysis> files)
    {
        bool declared = project.ManifestPaths.Count > 0;
        IReadOnlyList<EvidenceLocation> locations = declared
            ? [.. project.ManifestPaths.Select(path => KotlinEvidence.Location(path))]
            : [.. files.Take(1).Select(file => KotlinEvidence.Location(file.File.Scope))];
        return KotlinEvidence.Fact(
            $"kotlin:project:{KotlinEvidence.IdToken(project.Directory)}",
            EvidenceKinds.EcosystemPackage,
            project.Directory,
            declared
                ? $"Static Kotlin/JVM project metadata for '{project.Name}'."
                : "Implicit repository-level Kotlin/JVM source scope without Maven or Gradle metadata.",
            declared ? EvidenceSourceKind.Observed : EvidenceSourceKind.Inferred,
            declared
                ? "common-scanner-admitted Maven and Gradle metadata parsed without build execution"
                : "fallback scope derived from scanner-admitted maintained Kotlin source",
            locations,
            [
                KotlinEvidence.Measurement("source-files", files.Count, "files"),
                KotlinEvidence.Measurement("packages", files.Select(file => file.Syntax.Metrics.PackageName)
                    .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Count(), "packages"),
                KotlinEvidence.Measurement("dependencies", project.Dependencies.Count, "packages"),
                KotlinEvidence.Measurement("local-projects", project.LocalProjectDirectories.Count, "projects"),
            ],
            ProjectTags(project, files, declared));
    }

    public static IEnumerable<EvidenceFact> Packages(
        JavaProjectModel project,
        IReadOnlyList<KotlinFileAnalysis> files) => files
        .GroupBy(file => file.Syntax.Metrics.PackageName.Length > 0
            ? file.Syntax.Metrics.PackageName
            : "(default)", StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => KotlinEvidence.Fact(
            $"kotlin:package:{KotlinEvidence.IdToken(project.Directory)}:{KotlinEvidence.IdToken(group.Key)}",
            EvidenceKinds.EcosystemPackage,
            project.Directory,
            $"Static Kotlin package '{group.Key}' in JVM project '{project.Name}'.",
            EvidenceSourceKind.Observed,
            "package declarations grouped from scanner-admitted Kotlin source",
            group.Select(file => KotlinEvidence.Location(file.File.Scope)),
            [
                KotlinEvidence.Measurement("source-files", group.Count(), "files"),
                KotlinEvidence.Measurement("test-files", group.Count(file => IsTest(file.File)), "files"),
            ],
            ["ecosystem:kotlin", "jvm-language:kotlin", "kotlin-scope:package", "scope:inventory"]));

    public static IEnumerable<EvidenceFact> Dependencies(JavaProjectModel project) =>
        project.Dependencies.Select(dependency => KotlinEvidence.Fact(
            $"kotlin:dependency:{KotlinEvidence.IdToken(project.Directory)}:{KotlinEvidence.IdToken(dependency)}",
            EvidenceKinds.PackageReference,
            project.Directory,
            $"Kotlin/JVM package dependency '{dependency}'.",
            EvidenceSourceKind.Observed,
            "static Maven or Gradle dependency declaration",
            project.ManifestPaths.Select(path => KotlinEvidence.Location(path)),
            tags: ["ecosystem:kotlin", "jvm-language:kotlin", $"dependency:{dependency}"]));

    public static EvidenceFact? BuildConfiguration(JavaProjectModel project)
    {
        if (project.ManifestPaths.Count == 0) return null;
        return KotlinEvidence.Fact(
            $"kotlin:build:{KotlinEvidence.IdToken(project.Directory)}",
            EvidenceKinds.BuildConfiguration,
            project.Directory,
            $"Static Maven and Gradle build configuration for Kotlin/JVM project '{project.Name}'.",
            EvidenceSourceKind.Observed,
            "literal Maven XML and conservative Gradle token-pattern metadata",
            project.ManifestPaths.Select(path => KotlinEvidence.Location(path)),
            [
                KotlinEvidence.Measurement("build-systems", project.BuildSystems.Count, "systems"),
                KotlinEvidence.Measurement("plugins", project.Plugins.Count, "plugins"),
                KotlinEvidence.Measurement("maven-profiles", project.MavenProfiles, "profiles"),
                KotlinEvidence.Measurement("code-generators", project.AnnotationProcessors, "processors"),
                KotlinEvidence.Measurement("unresolved-values", project.UnresolvedValues, "values"),
            ],
            [
                "ecosystem:kotlin", "jvm-language:kotlin", "metadata:static-only", "jvm:not-invoked",
                "maven:not-invoked", "gradle:not-invoked", "compiler-plugins:not-invoked",
                project.UnresolvedValues > 0 ? "build-model:partially-unresolved" : "build-model:literal",
            ]);
    }

    public static EvidenceFact SourceStructure(
        JavaProjectModel project,
        IReadOnlyList<KotlinFileAnalysis> files)
    {
        KotlinSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return KotlinEvidence.Fact(
            $"kotlin:source:{KotlinEvidence.IdToken(project.Directory)}",
            EvidenceKinds.SourceStructure,
            project.Directory,
            $"Bounded token structure for Kotlin/JVM project '{project.Name}'.",
            EvidenceSourceKind.Measured,
            "bounded managed Kotlin tokenization and declaration analysis",
            files.Select(file => KotlinEvidence.Location(file.File.Scope)),
            [
                KotlinEvidence.Measurement("files", files.Count, "files"),
                KotlinEvidence.Measurement("scripts", Sum(metrics, item => item.IsScript ? 1 : 0), "files"),
                KotlinEvidence.Measurement("packages", metrics.Select(item => item.PackageName)
                    .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Count(), "packages"),
                KotlinEvidence.Measurement("functions", Sum(metrics, item => item.Functions), "symbols"),
                KotlinEvidence.Measurement("methods", Sum(metrics, item => item.Methods), "symbols"),
                KotlinEvidence.Measurement("types", Sum(metrics, item => item.Types), "symbols"),
                KotlinEvidence.Measurement("interfaces", Sum(metrics, item => item.Interfaces), "symbols"),
                KotlinEvidence.Measurement("objects", Sum(metrics, item => item.Objects), "symbols"),
                KotlinEvidence.Measurement("data-classes", Sum(metrics, item => item.DataClasses), "symbols"),
                KotlinEvidence.Measurement("sealed-types", Sum(metrics, item => item.SealedTypes), "symbols"),
                KotlinEvidence.Measurement("extension-functions", Sum(metrics, item => item.ExtensionFunctions), "symbols"),
                KotlinEvidence.Measurement("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                KotlinEvidence.Measurement("generic-declarations", Sum(metrics, item => item.GenericDeclarations), "symbols"),
                KotlinEvidence.Measurement("suspend-functions", Sum(metrics, item => item.SuspendFunctions), "symbols"),
                KotlinEvidence.Measurement("async-units", Sum(metrics, item => item.AsyncUnits), "symbols"),
                KotlinEvidence.Measurement("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                KotlinEvidence.Measurement("exception-paths", Sum(metrics, item => item.ExceptionPaths), "paths"),
                KotlinEvidence.Measurement("nullability-usages", Sum(metrics, item => item.NullabilityUsages), "usages"),
                KotlinEvidence.Measurement("coroutine-usages", Sum(metrics, item => item.CoroutineUsages), "usages"),
                KotlinEvidence.Measurement("flow-usages", Sum(metrics, item => item.FlowUsages), "usages"),
                KotlinEvidence.Measurement("imports", Sum(metrics, item => item.Imports), "imports"),
                KotlinEvidence.Measurement("internal-imports", Sum(metrics, item => item.InternalImports), "imports"),
                KotlinEvidence.Measurement("annotations", Sum(metrics, item => item.Annotations), "annotations"),
            ],
            [
                "ecosystem:kotlin", "jvm-language:kotlin", "syntax:token-backed",
                "parser:bounded-managed-kotlin-tokenizer", $"parser-confidence:{Confidence(files)}",
                "source-excerpts:not-emitted",
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(KotlinFileAnalysis file)
    {
        KotlinSourceMetrics metrics = file.Syntax.Metrics;
        string path = file.File.Scope;
        string token = KotlinEvidence.IdToken(path);
        EvidenceLocation[] location = [KotlinEvidence.Location(path)];
        bool testFile = IsTest(file.File);
        string[] commonTags =
        [
            "ecosystem:kotlin", "jvm-language:kotlin", "syntax:token-backed",
            $"parser-confidence:{file.Syntax.Confidence}", "source-excerpts:not-emitted",
            metrics.IsScript ? "source-kind:kotlin-script" : "source-kind:kotlin-source",
        ];

        if (!testFile && metrics.EntryPoints + metrics.CliCommands > 0)
            yield return Semantic("entry", EvidenceKinds.EntryPoint, "application or command entry",
                "main declarations, script boundary, or import-qualified CLI calls", "entry-points",
                Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces");
        if (!testFile && metrics.ApiEndpoints + metrics.ApiTypes > 0)
            yield return Pair("api", EvidenceKinds.ApiSurface, "API",
                "qualified Ktor, Spring MVC, or Jakarta REST evidence",
                "endpoints", metrics.ApiEndpoints, "controllers", metrics.ApiTypes);
        if (!testFile && metrics.AndroidComponents + metrics.UiSurfaces > 0)
            yield return Pair("ui", EvidenceKinds.UserInterface, "Android user-interface",
                "qualified Android component, lifecycle, or Compose evidence",
                "components", metrics.AndroidComponents, "ui-surfaces", metrics.UiSurfaces);
        if (!testFile && metrics.DataCalls + metrics.DataModels + metrics.Migrations > 0)
            yield return KotlinEvidence.Fact($"kotlin:data:{token}", EvidenceKinds.DataAccess,
                file.Project.Directory, $"Import-qualified Kotlin persistence surface in '{path}'.",
                EvidenceSourceKind.Inferred, "qualified persistence annotations, types, and calls", location,
                [
                    KotlinEvidence.Measurement("db-sets", metrics.DataModels, "models"),
                    KotlinEvidence.Measurement("migrations", metrics.Migrations, "migrations"),
                    KotlinEvidence.Measurement("data-calls", metrics.DataCalls, "calls"),
                ], [.. commonTags, .. TechnologyTags(metrics)]);
        if (!testFile && metrics.IntegrationCalls > 0)
            yield return Semantic("integration", EvidenceKinds.Integration, "external integration",
                "qualified HTTP, RPC, cloud, or messaging usage", "integration-calls",
                metrics.IntegrationCalls, "calls");
        if (!testFile && metrics.SecurityUsages > 0)
            yield return Semantic("security", EvidenceKinds.SecurityConfiguration, "security",
                "qualified security annotations, types, and calls", "security-usages",
                metrics.SecurityUsages, "usages");
        if (!testFile && metrics.BackgroundUsages > 0)
            yield return Semantic("background", EvidenceKinds.BackgroundWork, "background-work",
                "qualified scheduling, worker, messaging, or coroutine usage", "background-usages",
                metrics.BackgroundUsages, "usages");
        if (!testFile && metrics.ValidationRules > 0)
            yield return Semantic("validation", EvidenceKinds.Validation, "validation",
                "qualified Jakarta validation annotations and calls", "validation-rules",
                metrics.ValidationRules, "rules");
        if (testFile || metrics.TestCases > 0)
        {
            string testType = TestType(path, metrics);
            yield return KotlinEvidence.Fact($"kotlin:test:{token}", EvidenceKinds.EcosystemTest,
                file.Project.Directory, $"Static Kotlin {testType.Replace('-', ' ')} test evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "test path, import-qualified annotations, assertions, parameterization, and mocks", location,
                [
                    KotlinEvidence.Measurement("test-cases", Math.Max(1, metrics.TestCases), "cases"),
                    KotlinEvidence.Measurement("parameterized-cases", metrics.ParameterizedCases, "cases"),
                    KotlinEvidence.Measurement("assertions", metrics.Assertions, "assertions"),
                    KotlinEvidence.Measurement("mock-usages", metrics.MockUsages, "usages"),
                ], [.. commonTags, $"test-type:{testType}", .. TechnologyTags(metrics)]);
        }

        EvidenceFact Semantic(
            string id, string kind, string description, string method,
            string measurement, int value, string unit) => KotlinEvidence.Fact(
                $"kotlin:{id}:{token}", kind, file.Project.Directory,
                $"Import-qualified Kotlin {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [KotlinEvidence.Measurement(measurement, value, unit)],
                [.. commonTags, .. TechnologyTags(metrics)]);

        EvidenceFact Pair(
            string id, string kind, string description, string method,
            string firstName, int first, string secondName, int second) => KotlinEvidence.Fact(
                $"kotlin:{id}:{token}", kind, file.Project.Directory,
                $"Import-qualified Kotlin {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [
                    KotlinEvidence.Measurement(firstName, first, "surfaces"),
                    KotlinEvidence.Measurement(secondName, second, "surfaces"),
                ], [.. commonTags, .. TechnologyTags(metrics)]);
    }

    public static EvidenceFact ProjectReference(
        JavaProjectModel source,
        JavaProjectModel target,
        IReadOnlyList<KotlinFileAnalysis> imports,
        bool buildReference)
    {
        List<EvidenceLocation> locations = [.. imports.Select(file => KotlinEvidence.Location(file.File.Scope))];
        if (buildReference)
            locations.AddRange(source.ManifestPaths.Select(path => KotlinEvidence.Location(path)));
        return KotlinEvidence.Fact(
            $"kotlin:reference:{KotlinEvidence.IdToken(source.Directory)}:{KotlinEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            $"Kotlin/JVM project '{source.Name}' references local project '{target.Name}'.",
            EvidenceSourceKind.Inferred,
            "literal reactor/project dependency or Kotlin package import matched another discovered JVM project",
            locations,
            [KotlinEvidence.Measurement("reference-edges", 1, "edges")],
            [
                "ecosystem:kotlin", "jvm-language:kotlin", $"target-scope:{target.Directory}",
                buildReference ? "reference-kind:build" : "reference-kind:import",
            ]);
    }

    private static IEnumerable<string> ProjectTags(
        JavaProjectModel project,
        IReadOnlyList<KotlinFileAnalysis> files,
        bool declared)
    {
        yield return "ecosystem:java";
        yield return "ecosystem:kotlin";
        yield return "jvm-language:kotlin";
        yield return $"package-role:{project.Role}";
        yield return "metadata:static-only";
        yield return "jvm:not-invoked";
        yield return "scope:analyzed";
        yield return declared ? "kotlin-project:declared" : "kotlin-project:implicit-fallback";
        foreach (string system in project.BuildSystems) yield return $"build-system:{system}";
        if (files.Any(file => !IsTest(file.File) && file.Syntax.Metrics.EntryPoints > 0))
            yield return "package:cli-bin";
        if (files.Any(file => !IsTest(file.File) && file.Syntax.Metrics.PublicSymbols > 0))
            yield return "package:library-exports";
    }

    private static IEnumerable<string> TechnologyTags(KotlinSourceMetrics metrics) =>
        metrics.Technologies.Order(StringComparer.Ordinal).Select(item => $"technology:{item}");

    private static bool IsTest(EvidenceFact file) =>
        file.Tags.Contains("classification:test", StringComparer.Ordinal);

    private static int Sum(IEnumerable<KotlinSourceMetrics> metrics, Func<KotlinSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<KotlinFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static string TestType(string path, KotlinSourceMetrics metrics)
    {
        if (metrics.EndToEndTests > 0) return "end-to-end";
        if (metrics.ComponentTests > 0) return "component";
        if (metrics.IntegrationTests > 0) return "integration";
        string lower = path.ToLowerInvariant();
        if (lower.Contains("androidtest", StringComparison.Ordinal)) return "component";
        return "unit";
    }
}
