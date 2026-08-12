using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal static class PythonFactFactory
{
    public static EvidenceFact Package(
        PythonPackageModel package,
        IReadOnlyList<PythonFileAnalysis> files)
    {
        IReadOnlyList<EvidenceLocation> locations = package.ManifestPaths.Count > 0
            ? [.. package.ManifestPaths.Select(path => PythonEvidence.Location(path))]
            : [.. files.Take(1).Select(file => PythonEvidence.Location(file.File.Scope))];
        return PythonEvidence.Fact(
            $"python:package:{PythonEvidence.IdToken(package.Directory)}",
            EvidenceKinds.EcosystemPackage,
            package.Directory,
            $"Static Python package metadata for '{package.Name}'.",
            EvidenceSourceKind.Observed,
            "common-scanner-admitted package metadata parsed without Python execution",
            locations,
            [
                PythonEvidence.Measurement("source-files", files.Count, "files"),
                PythonEvidence.Measurement("dependencies", package.Dependencies.Count, "packages"),
                PythonEvidence.Measurement("scripts", package.Scripts.Count, "entry-points"),
            ],
            PackageTags(package));
    }

    public static IEnumerable<EvidenceFact> Dependencies(PythonPackageModel package) =>
        package.Dependencies.Select(dependency => PythonEvidence.Fact(
            $"python:dependency:{PythonEvidence.IdToken(package.Directory)}:{PythonEvidence.IdToken(dependency)}",
            EvidenceKinds.PackageReference,
            package.Directory,
            $"Python package dependency '{dependency}'.",
            EvidenceSourceKind.Observed,
            "static Python package metadata",
            package.ManifestPaths.Select(path => PythonEvidence.Location(path)),
            tags: ["ecosystem:python", $"dependency:{dependency}"]));

    public static EvidenceFact SourceStructure(
        PythonPackageModel package,
        IReadOnlyList<PythonFileAnalysis> files)
    {
        PythonSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return PythonEvidence.Fact(
            $"python:source:{PythonEvidence.IdToken(package.Directory)}",
            EvidenceKinds.SourceStructure,
            package.Directory,
            $"Bounded token and indentation structure for Python package '{package.Name}'.",
            EvidenceSourceKind.Measured,
            "bounded managed Python tokenization with indentation-aware declaration analysis",
            files.Select(file => PythonEvidence.Location(file.File.Scope)),
            [
                PythonEvidence.Measurement("files", files.Count, "files"),
                PythonEvidence.Measurement("modules", files.Count, "modules"),
                PythonEvidence.Measurement("functions", Sum(metrics, item => item.Functions), "symbols"),
                PythonEvidence.Measurement("methods", Sum(metrics, item => item.Methods), "symbols"),
                PythonEvidence.Measurement("types", Sum(metrics, item => item.Classes), "symbols"),
                PythonEvidence.Measurement("public-symbols", Sum(metrics, item => item.PublicSymbols), "symbols"),
                PythonEvidence.Measurement("async-units", Sum(metrics, item => item.AsyncUnits), "symbols"),
                PythonEvidence.Measurement("branch-points", Sum(metrics, item => item.BranchPoints), "branches"),
                PythonEvidence.Measurement("imports", Sum(metrics, item => item.Imports), "imports"),
                PythonEvidence.Measurement("internal-imports", Sum(metrics, item => item.InternalImports), "imports"),
                PythonEvidence.Measurement("decorators", Sum(metrics, item => item.Decorators), "decorators"),
                PythonEvidence.Measurement("type-annotations", Sum(metrics, item => item.TypeAnnotations), "annotations"),
            ],
            [
                "ecosystem:python",
                "syntax:token-backed",
                "parser:bounded-managed-tokenizer",
                $"parser-confidence:{Confidence(files)}",
                "source-excerpts:not-emitted",
            ]);
    }

    public static IEnumerable<EvidenceFact> FileSemantics(PythonFileAnalysis file)
    {
        PythonSourceMetrics metrics = file.Syntax.Metrics;
        List<string> commonTags =
        [
            "ecosystem:python",
            "syntax:token-backed",
            $"parser-confidence:{file.Syntax.Confidence}",
            "source-excerpts:not-emitted",
        ];
        if (Path.GetExtension(file.File.Scope).Equals(".ipynb", StringComparison.OrdinalIgnoreCase))
        {
            commonTags.Add("format:jupyter-notebook");
            commonTags.Add("outputs:excluded");
        }
        string path = file.File.Scope;
        string token = PythonEvidence.IdToken(path);
        EvidenceLocation[] location = [PythonEvidence.Location(path)];

        if (metrics.EntryPoints + metrics.CliCommands > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:entry:{token}",
                EvidenceKinds.EntryPoint,
                file.Package.Directory,
                $"Python application or command entry surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "name-guard, module entry, or import-qualified CLI framework evidence",
                location,
                [PythonEvidence.Measurement("entry-points", Math.Max(1, metrics.EntryPoints + metrics.CliCommands), "surfaces")],
                [.. commonTags, .. TechnologyTags(metrics, "argparse", "click", "typer")]);
        }

        if (metrics.ApiEndpoints + metrics.ApiTypes > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:api:{token}",
                EvidenceKinds.ApiSurface,
                file.Package.Directory,
                $"Import-qualified Python API surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "framework-qualified decorators, routes, and API base types",
                location,
                [
                    PythonEvidence.Measurement("endpoints", metrics.ApiEndpoints, "routes"),
                    PythonEvidence.Measurement("controllers", metrics.ApiTypes, "types"),
                ],
                [.. commonTags, .. TechnologyTags(metrics, "fastapi", "flask", "django")]);
        }

        if (metrics.DataCalls + metrics.DataModels + metrics.Migrations > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:data:{token}",
                EvidenceKinds.DataAccess,
                file.Package.Directory,
                $"Import-qualified Python persistence surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "framework-qualified ORM and migration syntax",
                location,
                [
                    PythonEvidence.Measurement("db-sets", metrics.DataModels, "models"),
                    PythonEvidence.Measurement("migrations", metrics.Migrations, "files"),
                    PythonEvidence.Measurement("entity-configurations", metrics.DataModels, "types"),
                    PythonEvidence.Measurement("data-calls", metrics.DataCalls, "calls"),
                ],
                [.. commonTags, .. TechnologyTags(metrics, "sqlalchemy", "django", "alembic")]);
        }

        if (metrics.IntegrationCalls > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:integration:{token}",
                EvidenceKinds.Integration,
                file.Package.Directory,
                $"Import-qualified external integration surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "qualified client and cloud SDK calls",
                location,
                [PythonEvidence.Measurement("integration-calls", metrics.IntegrationCalls, "calls")],
                [.. commonTags, .. TechnologyTags(metrics, "requests", "httpx", "boto3", "google", "azure", "openai")]);
        }

        if (metrics.SecurityUsages > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:security:{token}",
                EvidenceKinds.SecurityConfiguration,
                file.Package.Directory,
                $"Import-qualified Python security surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "qualified authentication, credential, and cryptographic library usage",
                location,
                [PythonEvidence.Measurement("security-usages", metrics.SecurityUsages, "calls")],
                [.. commonTags, .. TechnologyTags(metrics, "jose", "passlib", "bcrypt", "fastapi", "django")]);
        }

        if (metrics.BackgroundUsages > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:background:{token}",
                EvidenceKinds.BackgroundWork,
                file.Package.Directory,
                $"Import-qualified Python background-work surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "qualified queue, task, and worker usage",
                location,
                [PythonEvidence.Measurement("background-usages", metrics.BackgroundUsages, "calls")],
                [.. commonTags, .. TechnologyTags(metrics, "celery", "rq")]);
        }

        if (metrics.ValidationRules + metrics.ValidationTypes > 0)
        {
            yield return PythonEvidence.Fact(
                $"python:validation:{token}",
                EvidenceKinds.Validation,
                file.Package.Directory,
                $"Import-qualified Python validation surface in '{path}'.",
                EvidenceSourceKind.Inferred,
                "qualified validation base types, fields, and decorators",
                location,
                [
                    PythonEvidence.Measurement("validator-types", metrics.ValidationTypes, "types"),
                    PythonEvidence.Measurement("validation-rules", metrics.ValidationRules, "rules"),
                ],
                [.. commonTags, .. TechnologyTags(metrics, "pydantic", "marshmallow")]);
        }

        bool classifiedTest = file.File.Tags.Contains("classification:test", StringComparer.Ordinal);
        if (classifiedTest || metrics.TestCases > 0)
        {
            string testType = TestType(path);
            yield return PythonEvidence.Fact(
                $"python:test:{token}",
                EvidenceKinds.EcosystemTest,
                file.Package.Directory,
                $"Static Python {testType.Replace('-', ' ')} test evidence in '{path}'.",
                EvidenceSourceKind.Measured,
                "test path, declaration, assertion, parameterization, and qualified framework evidence",
                location,
                [
                    PythonEvidence.Measurement("test-cases", Math.Max(1, metrics.TestCases), "cases"),
                    PythonEvidence.Measurement("parameterized-cases", metrics.ParameterizedCases, "cases"),
                    PythonEvidence.Measurement("assertions", metrics.Assertions, "assertions"),
                    PythonEvidence.Measurement("mock-usages", metrics.MockUsages, "calls"),
                ],
                [.. commonTags, $"test-type:{testType}", .. TechnologyTags(metrics, "pytest", "unittest")]);
        }
    }

    public static EvidenceFact ProjectReference(
        PythonPackageModel source,
        PythonPackageModel target,
        IReadOnlyList<PythonFileAnalysis> evidence) => PythonEvidence.Fact(
            $"python:reference:{PythonEvidence.IdToken(source.Directory)}:{PythonEvidence.IdToken(target.Directory)}",
            EvidenceKinds.ProjectReference,
            source.Directory,
            $"Python package '{source.Name}' imports local package '{target.Name}'.",
            EvidenceSourceKind.Inferred,
            "qualified import root matched another statically discovered package",
            evidence.Select(file => PythonEvidence.Location(file.File.Scope)),
            [PythonEvidence.Measurement("reference-edges", 1, "edges")],
            ["ecosystem:python", $"target-scope:{target.Directory}"]);

    private static int Sum(IEnumerable<PythonSourceMetrics> metrics, Func<PythonSourceMetrics, int> selector) =>
        metrics.Sum(selector);

    private static string Confidence(IReadOnlyList<PythonFileAnalysis> files) =>
        files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium";

    private static IEnumerable<string> TechnologyTags(
        PythonSourceMetrics metrics,
        params string[] allowed) => metrics.Technologies
        .Where(allowed.Contains)
        .Order(StringComparer.Ordinal)
        .Select(item => $"technology:{item}");

    private static IEnumerable<string> PackageTags(PythonPackageModel package)
    {
        yield return "ecosystem:python";
        yield return $"package-role:{package.Role}";
        yield return "metadata:static-only";
        yield return "python-execution:not-performed";
        yield return "scope:analyzed";
        if (package.ManifestPaths.Any(path =>
            Path.GetFileName(path).Equals("setup.py", StringComparison.OrdinalIgnoreCase)))
        {
            yield return "setup-py:not-executed";
        }
    }

    private static string TestType(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal) ||
            lower.Contains("end_to_end", StringComparison.Ordinal)) return "end-to-end";
        if (lower.Contains("integration", StringComparison.Ordinal)) return "integration";
        return "unit";
    }
}
