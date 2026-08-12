using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Php;

internal static class PhpFactFactory
{
    public static EvidenceFact Package(
        PhpPackageModel package,
        IReadOnlyList<PhpFileAnalysis> files)
    {
        bool declared = package.ManifestPaths.Count > 0;
        IReadOnlyList<EvidenceLocation> locations = declared
            ? [.. package.ManifestPaths.Select(path => PhpEvidence.Location(path))]
            : [.. files.Take(1).Select(file => PhpEvidence.Location(file.File.Scope))];
        return PhpEvidence.Fact(
            $"php:package:{PhpEvidence.IdToken(package.Directory)}",
            EvidenceKinds.EcosystemPackage,
            package.Directory,
            declared
                ? $"Static Composer package metadata for '{package.Name}'."
                : "Implicit repository-level PHP source scope without Composer metadata.",
            declared ? EvidenceSourceKind.Observed : EvidenceSourceKind.Inferred,
            declared
                ? "common-scanner-admitted Composer JSON parsed without Composer execution"
                : "fallback scope derived from scanner-admitted maintained PHP source",
            locations,
            [
                PhpEvidence.Measurement("source-files", files.Count, "files"),
                PhpEvidence.Measurement("dependencies", package.Dependencies.Count, "packages"),
                PhpEvidence.Measurement("autoload-mappings", package.AutoloadMappings, "mappings"),
                PhpEvidence.Measurement("local-projects", package.PathRepositoryDirectories.Count, "projects"),
            ],
            PackageTags(package, files, declared));
    }

    public static IEnumerable<EvidenceFact> Dependencies(PhpPackageModel package) =>
        package.Dependencies.Select(dependency => PhpEvidence.Fact(
            $"php:dependency:{PhpEvidence.IdToken(package.Directory)}:{PhpEvidence.IdToken(dependency.Kind)}:{PhpEvidence.IdToken(dependency.Name)}",
            EvidenceKinds.PackageReference,
            package.Directory,
            $"Composer {dependency.Kind} dependency '{dependency.Name}'.",
            EvidenceSourceKind.Observed,
            "static Composer dependency declaration",
            package.ManifestPaths.Select(path => PhpEvidence.Location(path)),
            tags:
            [
                "ecosystem:php",
                $"dependency:{dependency.Name}",
                $"dependency-kind:{dependency.Kind}",
            ]));

    public static EvidenceFact? BuildConfiguration(PhpPackageModel package)
    {
        if (package.ManifestPaths.Count == 0) return null;
        return PhpEvidence.Fact(
            $"php:build:{PhpEvidence.IdToken(package.Directory)}",
            EvidenceKinds.BuildConfiguration,
            package.Directory,
            $"Static Composer autoload, script, binary, and repository configuration for '{package.Name}'.",
            EvidenceSourceKind.Observed,
            "literal Composer JSON metadata without script execution or dependency resolution",
            package.ManifestPaths.Select(path => PhpEvidence.Location(path)),
            [
                PhpEvidence.Measurement("autoload-mappings", package.AutoloadMappings, "mappings"),
                PhpEvidence.Measurement("autoload-files", package.AutoloadFiles, "files"),
                PhpEvidence.Measurement("scripts", package.ScriptNames.Count, "scripts"),
                PhpEvidence.Measurement("bin-entries", package.BinEntries, "entries"),
                PhpEvidence.Measurement("path-repositories", package.PathRepositoryDirectories.Count, "repositories"),
                PhpEvidence.Measurement("external-repositories", package.ExternalRepositories, "repositories"),
                PhpEvidence.Measurement("unresolved-paths", package.UnresolvedPaths, "paths"),
            ],
            [
                "ecosystem:php", "metadata:static-only", "composer:not-invoked",
                package.UnresolvedPaths > 0 ? "composer-model:partially-unresolved" : "composer-model:literal",
            ]);
    }

    public static EvidenceFact SourceStructure(
        PhpPackageModel package,
        IReadOnlyList<PhpFileAnalysis> files)
    {
        PhpSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return PhpEvidence.Fact(
            $"php:source:{PhpEvidence.IdToken(package.Directory)}",
            EvidenceKinds.SourceStructure,
            package.Directory,
            $"Bounded token structure for PHP package '{package.Name}'.",
            EvidenceSourceKind.Measured,
            "bounded managed PHP tokenization and declaration analysis",
            files.Select(file => PhpEvidence.Location(file.File.Scope)),
            [
                PhpEvidence.Measurement("files", files.Count, "files"),
                PhpEvidence.Measurement("namespaces", metrics.Select(item => item.Namespace)
                    .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Count(), "namespaces"),
                PhpEvidence.Measurement("functions", Sum(metrics, item => item.Functions), "symbols"),
                PhpEvidence.Measurement("methods", Sum(metrics, item => item.Methods), "symbols"),
                PhpEvidence.Measurement("types", Sum(metrics, item => item.Types), "symbols"),
                PhpEvidence.Measurement("interfaces", Sum(metrics, item => item.Interfaces), "symbols"),
                PhpEvidence.Measurement("traits", Sum(metrics, item => item.Traits), "symbols"),
                PhpEvidence.Measurement("enums", Sum(metrics, item => item.Enums), "symbols"),
                PhpEvidence.Measurement("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                PhpEvidence.Measurement("async-units", Sum(metrics, item => item.AsyncUnits), "symbols"),
                PhpEvidence.Measurement("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                PhpEvidence.Measurement("exception-paths", Sum(metrics, item => item.ExceptionPaths), "paths"),
                PhpEvidence.Measurement("imports", Sum(metrics, item => item.Imports), "imports"),
                PhpEvidence.Measurement("internal-imports", Sum(metrics, item => item.InternalImports), "imports"),
                PhpEvidence.Measurement("attributes", Sum(metrics, item => item.Attributes), "attributes"),
                PhpEvidence.Measurement("documentation-comments", Sum(metrics, item => item.DocumentationComments), "comments"),
                PhpEvidence.Measurement("dynamic-includes", Sum(metrics, item => item.DynamicIncludes), "usages"),
                PhpEvidence.Measurement("magic-methods", Sum(metrics, item => item.MagicMethods), "methods"),
                PhpEvidence.Measurement("reflection-usages", Sum(metrics, item => item.ReflectionUsages), "usages"),
            ],
            [
                "ecosystem:php", "syntax:token-backed", "parser:bounded-managed-tokenizer",
                $"parser-confidence:{Confidence(files)}", "source-excerpts:not-emitted",
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(PhpFileAnalysis file)
    {
        PhpSourceMetrics metrics = file.Syntax.Metrics;
        string path = file.File.Scope;
        string token = PhpEvidence.IdToken(path);
        EvidenceLocation[] location = [PhpEvidence.Location(path)];
        bool testFile = IsTest(file.File);
        string[] commonTags =
        [
            "ecosystem:php", "syntax:token-backed",
            $"parser-confidence:{file.Syntax.Confidence}", "source-excerpts:not-emitted",
        ];

        if (!testFile && metrics.EntryPoints + metrics.CliCommands > 0)
            yield return Semantic("entry", EvidenceKinds.EntryPoint, "application or command entry",
                "entry paths, Composer bins, or import-qualified console types and calls",
                "entry-points", Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces");
        if (!testFile && metrics.ApiEndpoints + metrics.ApiTypes > 0)
            yield return PhpEvidence.Fact($"php:api:{token}", EvidenceKinds.ApiSurface,
                file.Package.Directory, $"Import-qualified PHP API surface in '{path}'.",
                EvidenceSourceKind.Inferred, "qualified Laravel routing or Symfony route attributes",
                location,
                [PhpEvidence.Measurement("endpoints", metrics.ApiEndpoints, "routes"), PhpEvidence.Measurement("controllers", metrics.ApiTypes, "types")],
                [.. commonTags, .. TechnologyTags(metrics)]);
        if (!testFile && metrics.DataCalls + metrics.DataModels + metrics.Migrations > 0)
            yield return PhpEvidence.Fact($"php:data:{token}", EvidenceKinds.DataAccess,
                file.Package.Directory, $"Import-qualified PHP persistence surface in '{path}'.",
                EvidenceSourceKind.Inferred, "qualified Doctrine, Eloquent, database facade, and migration usage",
                location,
                [PhpEvidence.Measurement("db-sets", metrics.DataModels, "models"), PhpEvidence.Measurement("migrations", metrics.Migrations, "migrations"), PhpEvidence.Measurement("data-calls", metrics.DataCalls, "calls")],
                [.. commonTags, .. TechnologyTags(metrics)]);
        if (!testFile && metrics.IntegrationCalls > 0)
            yield return Semantic("integration", EvidenceKinds.Integration, "external integration",
                "qualified HTTP, cloud, cache, or messaging usage", "integration-calls",
                metrics.IntegrationCalls, "calls");
        if (!testFile && metrics.SecurityUsages > 0)
            yield return Semantic("security", EvidenceKinds.SecurityConfiguration, "security",
                "qualified security types and calls or unambiguous password primitives", "security-usages",
                metrics.SecurityUsages, "usages");
        if (!testFile && metrics.BackgroundUsages > 0)
            yield return Semantic("background", EvidenceKinds.BackgroundWork, "background-work",
                "qualified queues, messaging, jobs, or asynchronous usage", "background-usages",
                metrics.BackgroundUsages, "usages");
        if (!testFile && metrics.ValidationRules > 0)
            yield return Semantic("validation", EvidenceKinds.Validation, "validation",
                "qualified Symfony or Laravel validation usage", "validation-rules",
                metrics.ValidationRules, "rules");
        if (!testFile && file.Template.Represented)
            yield return PhpEvidence.Fact($"php:ui:{token}", EvidenceKinds.UserInterface,
                file.Package.Directory, $"Bounded maintained PHP template semantics in '{path}'.",
                EvidenceSourceKind.Measured,
                "static Blade/PHP template structure, binding, control-flow, and form counting",
                location,
                [
                    PhpEvidence.Measurement("components", file.Template.Components, "components"),
                    PhpEvidence.Measurement("forms", file.Template.Forms, "forms"),
                    PhpEvidence.Measurement("template-structure-units", file.Template.StructureUnits, "units"),
                    PhpEvidence.Measurement("template-binding-units", file.Template.BindingUnits, "units"),
                    PhpEvidence.Measurement("template-control-flow-units", file.Template.ControlFlowUnits, "units"),
                ],
                [.. commonTags, "ui-asset:php-template", "template-runtime:not-executed"]);

        if (testFile || metrics.TestCases > 0)
        {
            string testType = TestType(path, metrics);
            yield return PhpEvidence.Fact($"php:test:{token}", EvidenceKinds.EcosystemTest,
                file.Package.Directory, $"Static PHP {testType.Replace('-', ' ')} test evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "test path, import-qualified PHPUnit/Pest declarations, assertions, data providers, and mocks",
                location,
                [
                    PhpEvidence.Measurement("test-cases", Math.Max(1, metrics.TestCases), "cases"),
                    PhpEvidence.Measurement("parameterized-cases", metrics.ParameterizedCases, "cases"),
                    PhpEvidence.Measurement("assertions", metrics.Assertions, "assertions"),
                    PhpEvidence.Measurement("mock-usages", metrics.MockUsages, "usages"),
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
            string unit) => PhpEvidence.Fact(
                $"php:{id}:{token}", kind, file.Package.Directory,
                $"Import-qualified PHP {description} surface in '{path}'.",
                EvidenceSourceKind.Inferred, method, location,
                [PhpEvidence.Measurement(measurement, value, unit)],
                [.. commonTags, .. TechnologyTags(metrics)]);
    }

    public static EvidenceFact ProjectReference(
        PhpPackageModel source,
        PhpPackageModel target,
        IReadOnlyList<PhpFileAnalysis> imports,
        bool buildReference)
    {
        List<EvidenceLocation> locations = [.. imports.Select(file => PhpEvidence.Location(file.File.Scope))];
        if (buildReference) locations.AddRange(source.ManifestPaths.Select(path => PhpEvidence.Location(path)));
        return PhpEvidence.Fact(
            $"php:reference:{PhpEvidence.IdToken(source.Directory)}:{PhpEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            $"Composer package '{source.Name}' references local package '{target.Name}'.",
            EvidenceSourceKind.Inferred,
            "literal Composer path/dependency metadata or namespace import matched another discovered package",
            locations,
            [PhpEvidence.Measurement("reference-edges", 1, "edges")],
            ["ecosystem:php", $"target-scope:{target.Directory}", buildReference ? "reference-kind:build" : "reference-kind:import"]);
    }

    private static IEnumerable<string> PackageTags(
        PhpPackageModel package,
        IReadOnlyList<PhpFileAnalysis> files,
        bool declared)
    {
        yield return "ecosystem:php";
        yield return $"package-role:{package.Role}";
        yield return "metadata:static-only";
        yield return "composer:not-invoked";
        yield return "scope:analyzed";
        yield return declared ? "composer-package:declared" : "composer-package:implicit-fallback";
        if (package.PackageType is not null) yield return $"package-type:{package.PackageType}";
        if (package.BinEntries > 0) yield return "package:cli-bin";
        if (files.Any(file => !IsTest(file.File) && file.Syntax.Metrics.PublicSymbols > 0))
            yield return "package:library-exports";
        foreach (string technology in package.Dependencies
            .Select(item => PhpTechnologyCatalog.FromDependency(item.Name))
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
            yield return $"technology:{technology}";
    }

    private static IEnumerable<string> TechnologyTags(PhpSourceMetrics metrics) =>
        metrics.Technologies.Order(StringComparer.Ordinal).Select(item => $"technology:{item}");

    private static bool IsTest(EvidenceFact file) =>
        file.Tags.Contains("classification:test", StringComparer.Ordinal);

    private static int Sum(IEnumerable<PhpSourceMetrics> metrics, Func<PhpSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<PhpFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static string TestType(string path, PhpSourceMetrics metrics)
    {
        if (metrics.EndToEndTests > 0) return "end-to-end";
        if (metrics.IntegrationTests + metrics.FeatureTests > 0) return "integration";
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal)) return "end-to-end";
        if (lower.Contains("integration", StringComparison.Ordinal) ||
            lower.Contains("feature", StringComparison.Ordinal)) return "integration";
        return "unit";
    }
}
