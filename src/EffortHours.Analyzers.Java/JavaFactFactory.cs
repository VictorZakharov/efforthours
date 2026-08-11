using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal static class JavaFactFactory
{
    public static EvidenceFact Project(
        JavaProjectModel project,
        IReadOnlyList<JavaFileAnalysis> files)
    {
        bool declared = project.ManifestPaths.Count > 0;
        IReadOnlyList<EvidenceLocation> locations = declared
            ? [.. project.ManifestPaths.Select(path => JavaEvidence.Location(path))]
            : [.. files.Take(1).Select(file => JavaEvidence.Location(file.File.Scope))];
        return JavaEvidence.Fact(
            $"java:project:{JavaEvidence.IdToken(project.Directory)}",
            EvidenceKinds.EcosystemPackage,
            project.Directory,
            declared
                ? $"Static Java project metadata for '{project.Name}'."
                : "Implicit repository-level Java source scope without Maven or Gradle metadata.",
            declared ? EvidenceSourceKind.Observed : EvidenceSourceKind.Inferred,
            declared
                ? "common-scanner-admitted Maven and Gradle metadata parsed without build execution"
                : "fallback scope derived from scanner-admitted maintained Java source",
            locations,
            [
                JavaEvidence.Measurement("source-files", files.Count, "files"),
                JavaEvidence.Measurement("packages", files.Select(file => file.Syntax.Metrics.PackageName)
                    .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Count(), "packages"),
                JavaEvidence.Measurement("dependencies", project.Dependencies.Count, "packages"),
                JavaEvidence.Measurement("local-projects", project.LocalProjectDirectories.Count, "projects"),
            ],
            ProjectTags(project, files, declared));
    }

    public static IEnumerable<EvidenceFact> Packages(
        JavaProjectModel project,
        IReadOnlyList<JavaFileAnalysis> files) => files
        .GroupBy(file => file.Syntax.Metrics.PackageName.Length > 0
            ? file.Syntax.Metrics.PackageName
            : "(default)", StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => JavaEvidence.Fact(
            $"java:package:{JavaEvidence.IdToken(project.Directory)}:{JavaEvidence.IdToken(group.Key)}",
            EvidenceKinds.EcosystemPackage,
            project.Directory,
            $"Static Java package '{group.Key}' in project '{project.Name}'.",
            EvidenceSourceKind.Observed,
            "package declarations grouped from scanner-admitted Java source",
            group.Select(file => JavaEvidence.Location(file.File.Scope)),
            [
                JavaEvidence.Measurement("source-files", group.Count(), "files"),
                JavaEvidence.Measurement("test-files", group.Count(file => IsTest(file.File)), "files"),
            ],
            ["ecosystem:java", "java-scope:package", "scope:inventory"]));

    public static IEnumerable<EvidenceFact> Dependencies(JavaProjectModel project) =>
        project.Dependencies.Select(dependency => JavaEvidence.Fact(
            $"java:dependency:{JavaEvidence.IdToken(project.Directory)}:{JavaEvidence.IdToken(dependency)}",
            EvidenceKinds.PackageReference,
            project.Directory,
            $"Java package dependency '{dependency}'.",
            EvidenceSourceKind.Observed,
            "static Maven or Gradle dependency declaration",
            project.ManifestPaths.Select(path => JavaEvidence.Location(path)),
            tags: ["ecosystem:java", $"dependency:{dependency}"]));

    public static EvidenceFact SourceStructure(
        JavaProjectModel project,
        IReadOnlyList<JavaFileAnalysis> files)
    {
        JavaSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return JavaEvidence.Fact(
            $"java:source:{JavaEvidence.IdToken(project.Directory)}",
            EvidenceKinds.SourceStructure,
            project.Directory,
            $"Bounded token structure for Java project '{project.Name}'.",
            EvidenceSourceKind.Measured,
            "bounded managed Java tokenization and declaration analysis",
            files.Select(file => JavaEvidence.Location(file.File.Scope)),
            [
                JavaEvidence.Measurement("files", files.Count, "files"),
                JavaEvidence.Measurement("packages", metrics.Select(item => item.PackageName)
                    .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).Count(), "packages"),
                JavaEvidence.Measurement("functions", 0, "symbols"),
                JavaEvidence.Measurement("methods", Sum(metrics, item => item.Methods), "symbols"),
                JavaEvidence.Measurement("types", Sum(metrics, item => item.Types), "symbols"),
                JavaEvidence.Measurement("interfaces", Sum(metrics, item => item.Interfaces), "symbols"),
                JavaEvidence.Measurement("records", Sum(metrics, item => item.Records), "symbols"),
                JavaEvidence.Measurement("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                JavaEvidence.Measurement("generic-declarations", Sum(metrics, item => item.GenericDeclarations), "symbols"),
                JavaEvidence.Measurement("async-units", Sum(metrics, item => item.AsyncUnits), "symbols"),
                JavaEvidence.Measurement("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                JavaEvidence.Measurement("exception-paths", Sum(metrics, item => item.ExceptionPaths), "paths"),
                JavaEvidence.Measurement("concurrency-usages", Sum(metrics, item => item.ConcurrencyUsages), "operations"),
                JavaEvidence.Measurement("imports", Sum(metrics, item => item.Imports), "imports"),
                JavaEvidence.Measurement("internal-imports", Sum(metrics, item => item.InternalImports), "imports"),
                JavaEvidence.Measurement("annotations", Sum(metrics, item => item.Annotations), "annotations"),
                JavaEvidence.Measurement("module-requires", Sum(metrics, item => item.ModuleRequires), "directives"),
                JavaEvidence.Measurement("module-exports", Sum(metrics, item => item.ModuleExports), "directives"),
                JavaEvidence.Measurement("module-services", Sum(metrics, item => item.ModuleServices), "directives"),
            ],
            [
                "ecosystem:java", "syntax:token-backed", "parser:bounded-managed-tokenizer",
                $"parser-confidence:{Confidence(files)}", "source-excerpts:not-emitted",
            ]);
    }

    public static EvidenceFact? BuildConfiguration(JavaProjectModel project)
    {
        if (project.ManifestPaths.Count == 0) return null;
        return JavaEvidence.Fact(
            $"java:build:{JavaEvidence.IdToken(project.Directory)}",
            EvidenceKinds.BuildConfiguration,
            project.Directory,
            $"Static Maven and Gradle build configuration for Java project '{project.Name}'.",
            EvidenceSourceKind.Observed,
            "literal Maven XML and conservative Gradle token-pattern metadata",
            project.ManifestPaths.Select(path => JavaEvidence.Location(path)),
            [
                JavaEvidence.Measurement("build-systems", project.BuildSystems.Count, "systems"),
                JavaEvidence.Measurement("plugins", project.Plugins.Count, "plugins"),
                JavaEvidence.Measurement("maven-profiles", project.MavenProfiles, "profiles"),
                JavaEvidence.Measurement("annotation-processors", project.AnnotationProcessors, "processors"),
                JavaEvidence.Measurement("unresolved-values", project.UnresolvedValues, "values"),
            ],
            [
                "ecosystem:java", "metadata:static-only", "jvm:not-invoked",
                "maven:not-invoked", "gradle:not-invoked", "wrapper:not-invoked",
                project.UnresolvedValues > 0 ? "build-model:partially-unresolved" : "build-model:literal",
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(JavaFileAnalysis file)
    {
        JavaSourceMetrics metrics = file.Syntax.Metrics;
        string path = file.File.Scope;
        string token = JavaEvidence.IdToken(path);
        EvidenceLocation[] location = [JavaEvidence.Location(path)];
        bool testFile = IsTest(file.File);
        string[] commonTags =
        [
            "ecosystem:java", "syntax:token-backed",
            $"parser-confidence:{file.Syntax.Confidence}", "source-excerpts:not-emitted",
        ];

        if (!testFile && metrics.EntryPoints + metrics.CliCommands > 0)
            yield return Semantic("entry", EvidenceKinds.EntryPoint, "application or command entry",
                "main declarations or import-qualified CLI annotations and calls", "entry-points",
                Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces");
        if (!testFile && metrics.ApiEndpoints + metrics.ApiTypes > 0)
            yield return JavaEvidence.Fact($"java:api:{token}", EvidenceKinds.ApiSurface,
                file.Project.Directory, $"Import-qualified Java API surface in '{path}'.",
                EvidenceSourceKind.Inferred, "qualified Spring MVC or Jakarta REST annotations",
                location,
                [JavaEvidence.Measurement("endpoints", metrics.ApiEndpoints, "routes"), JavaEvidence.Measurement("controllers", metrics.ApiTypes, "types")],
                [.. commonTags, .. TechnologyTags(metrics)]);
        if (!testFile && metrics.DataCalls + metrics.DataModels + metrics.Migrations > 0)
            yield return JavaEvidence.Fact($"java:data:{token}", EvidenceKinds.DataAccess,
                file.Project.Directory, $"Import-qualified Java persistence surface in '{path}'.",
                EvidenceSourceKind.Inferred, "qualified persistence annotations, types, and calls",
                location,
                [JavaEvidence.Measurement("db-sets", metrics.DataModels, "models"), JavaEvidence.Measurement("migrations", metrics.Migrations, "migrations"), JavaEvidence.Measurement("data-calls", metrics.DataCalls, "calls")],
                [.. commonTags, .. TechnologyTags(metrics)]);
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
                "qualified scheduling, batch, messaging, or concurrency usage", "background-usages",
                metrics.BackgroundUsages, "usages");
        if (!testFile && metrics.ValidationRules > 0)
            yield return Semantic("validation", EvidenceKinds.Validation, "validation",
                "qualified Jakarta validation annotations and calls", "validation-rules",
                metrics.ValidationRules, "rules");

        if (testFile || metrics.TestCases > 0)
        {
            string testType = TestType(path, metrics);
            yield return JavaEvidence.Fact($"java:test:{token}", EvidenceKinds.EcosystemTest,
                file.Project.Directory, $"Static Java {testType.Replace('-', ' ')} test evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "test path, import-qualified annotations, assertions, parameterization, and mocks",
                location,
                [
                    JavaEvidence.Measurement("test-cases", Math.Max(1, metrics.TestCases), "cases"),
                    JavaEvidence.Measurement("parameterized-cases", metrics.ParameterizedCases, "cases"),
                    JavaEvidence.Measurement("assertions", metrics.Assertions, "assertions"),
                    JavaEvidence.Measurement("mock-usages", metrics.MockUsages, "usages"),
                ],
                [.. commonTags, $"test-type:{testType}", .. TechnologyTags(metrics)]);
        }

        EvidenceFact Semantic(
            string id,
            string kind,
            string description,
            string method,
            string measurement,
            int value,
            string unit) => JavaEvidence.Fact(
                $"java:{id}:{token}", kind, file.Project.Directory,
                $"Import-qualified Java {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [JavaEvidence.Measurement(measurement, value, unit)],
                [.. commonTags, .. TechnologyTags(metrics)]);
    }

    public static EvidenceFact ProjectReference(
        JavaProjectModel source,
        JavaProjectModel target,
        IReadOnlyList<JavaFileAnalysis> imports,
        bool buildReference)
    {
        List<EvidenceLocation> locations = [.. imports.Select(file => JavaEvidence.Location(file.File.Scope))];
        if (buildReference) locations.AddRange(source.ManifestPaths.Select(path => JavaEvidence.Location(path)));
        return JavaEvidence.Fact(
            $"java:reference:{JavaEvidence.IdToken(source.Directory)}:{JavaEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            $"Java project '{source.Name}' references local project '{target.Name}'.",
            EvidenceSourceKind.Inferred,
            "literal reactor/project dependency or package import matched another discovered Java project",
            locations,
            [JavaEvidence.Measurement("reference-edges", 1, "edges")],
            ["ecosystem:java", $"target-scope:{target.Directory}", buildReference ? "reference-kind:build" : "reference-kind:import"]);
    }

    private static IEnumerable<string> ProjectTags(
        JavaProjectModel project,
        IReadOnlyList<JavaFileAnalysis> files,
        bool declared)
    {
        yield return "ecosystem:java";
        yield return $"package-role:{project.Role}";
        yield return "metadata:static-only";
        yield return "jvm:not-invoked";
        yield return "scope:analyzed";
        yield return declared ? "java-project:declared" : "java-project:implicit-fallback";
        foreach (string system in project.BuildSystems) yield return $"build-system:{system}";
        if (project.Packaging is not null) yield return $"packaging:{project.Packaging}";
        if (files.Any(file => !IsTest(file.File) && file.Syntax.Metrics.EntryPoints > 0))
            yield return "package:cli-bin";
        if (files.Any(file => !IsTest(file.File) && file.Syntax.Metrics.PublicSymbols > 0))
            yield return "package:library-exports";
    }

    private static IEnumerable<string> TechnologyTags(JavaSourceMetrics metrics) =>
        metrics.Technologies.Order(StringComparer.Ordinal).Select(item => $"technology:{item}");

    private static bool IsTest(EvidenceFact file) =>
        file.Tags.Contains("classification:test", StringComparer.Ordinal);

    private static int Sum(IEnumerable<JavaSourceMetrics> metrics, Func<JavaSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<JavaFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static string TestType(string path, JavaSourceMetrics metrics)
    {
        if (metrics.EndToEndTests > 0) return "end-to-end";
        if (metrics.ComponentTests > 0) return "component";
        if (metrics.IntegrationTests > 0) return "integration";
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal)) return "end-to-end";
        if (lower.Contains("component", StringComparison.Ordinal)) return "component";
        if (lower.Contains("integration", StringComparison.Ordinal) ||
            Path.GetFileName(path).EndsWith("IT.java", StringComparison.Ordinal)) return "integration";
        return "unit";
    }
}
