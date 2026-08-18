using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.DotNet;

public sealed class DotNetRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public DotNetRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public DotNetRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "dotnet";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
    [
        new("csharp", LanguageAnalysisSupport.ParserBacked),
        new("razor", LanguageAnalysisSupport.Structural),
    ];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);

        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        DotNetProjectReadResult projectResult = await new DotNetProjectReader(
            _fileSystem,
            rootPath).ReadAsync(evidence, cancellationToken).ConfigureAwait(false);
        List<EvidenceFact> facts = [];
        List<Diagnostic> diagnostics = [.. projectResult.Diagnostics];
        facts.Add(CreateRepositoryFact(projectResult));
        facts.AddRange(projectResult.Solutions.Select(CreateSolutionFact));
        foreach (DotNetProjectModel project in projectResult.Projects)
        {
            facts.Add(CreateProjectFact(project));
            facts.AddRange(project.Packages.Select(package => CreatePackageFact(project, package)));
            facts.AddRange(project.ProjectReferences.Select(reference =>
                CreateProjectReferenceFact(project, reference)));
            AddProjectDiagnostics(project, diagnostics);
        }

        Dictionary<string, List<CSharpStructureMetrics>> structureByScope =
            new(StringComparer.Ordinal);
        IReadOnlyList<DotNetFileAnalysisEntry> fileAnalyses =
            await DotNetFileAnalysisBatch.AnalyzeAsync(
                _fileSystem,
                rootPath,
                evidence,
                projectResult.Projects,
                cancellationToken).ConfigureAwait(false);
        foreach (DotNetFileAnalysisEntry analysis in fileAnalyses)
        {
            facts.AddRange(analysis.Facts);
            diagnostics.AddRange(analysis.Diagnostics);
            if (analysis.Structure is { } structure)
            {
                if (!structureByScope.TryGetValue(
                    analysis.ProjectScope,
                    out List<CSharpStructureMetrics>? structures))
                {
                    structures = [];
                    structureByScope.Add(analysis.ProjectScope, structures);
                }

                structures.Add(structure);
            }
        }

        foreach ((string scope, List<CSharpStructureMetrics> structures) in structureByScope
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            facts.Add(CreateStructureFact(scope, structures));
        }

        diagnostics.Add(DotNetEvidence.Diagnostic(
            "FB3000",
            DiagnosticSeverity.Information,
            "The .NET analyzer parsed project XML and source syntax statically; it did not evaluate MSBuild, restore packages, compile, or execute target code."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private static EvidenceFact CreateRepositoryFact(DotNetProjectReadResult result) =>
        DotNetEvidence.Fact(
            "dotnet:repository",
            EvidenceKinds.DotNetProject,
            ".",
            "Static .NET solution and project inventory.",
            EvidenceSourceKind.Observed,
            "safe XML and solution-text parsing without MSBuild evaluation",
            result.Projects.Select(project => DotNetEvidence.Location(project.Path)),
            [
                DotNetEvidence.Measurement("solutions", result.Solutions.Count, "solutions"),
                DotNetEvidence.Measurement("projects", result.Projects.Count, "projects"),
                DotNetEvidence.Measurement("test-projects", result.Projects.Count(project => project.Role == "test"), "projects"),
                DotNetEvidence.Measurement("package-references", result.Projects.Sum(project => project.Packages.Count), "references"),
                DotNetEvidence.Measurement("project-references", result.Projects.Sum(project => project.ProjectReferences.Count), "references"),
            ],
            result.Projects.Select(project => $"project-role:{project.Role}"));

    private static EvidenceFact CreateSolutionFact(DotNetSolutionModel solution) =>
        DotNetEvidence.Fact(
            $"dotnet:solution:{solution.Path}",
            EvidenceKinds.DotNetSolution,
            solution.Path,
            $".NET solution '{solution.Path}' declares {solution.DeclaredProjectPaths.Count} project(s).",
            EvidenceSourceKind.Observed,
            "static .sln or .slnx project-path parsing",
            [DotNetEvidence.Location(solution.Path)],
            [
                DotNetEvidence.Measurement("declared-projects", solution.DeclaredProjectPaths.Count, "projects"),
                DotNetEvidence.Measurement("resolved-projects", solution.ResolvedProjectPaths.Count, "projects"),
            ]);

    private static EvidenceFact CreateProjectFact(DotNetProjectModel project)
    {
        List<string> tags = [$"project-role:{project.Role}"];
        tags.AddRange(project.TargetFrameworks.Select(framework => $"target-framework:{framework}"));
        tags.AddRange(project.FrameworkReferences.Select(reference => $"framework-reference:{reference}"));
        AddTag(tags, "sdk", project.Sdk);
        AddTag(tags, "output-type", project.OutputType);
        AddTag(tags, "language-version", project.LanguageVersion);
        AddTag(tags, "nullable", project.Nullable);
        AddTag(tags, "runtime", project.RuntimeIdentifier);
        if (project.IsPackable)
        {
            tags.Add("packable:declared-true");
        }

        if (project.PublishAot)
        {
            tags.Add("publish-aot:declared-true");
        }

        if (project.UsesWpf)
        {
            tags.Add("ui:wpf");
        }

        if (project.UsesWindowsForms)
        {
            tags.Add("ui:windows-forms");
        }

        return DotNetEvidence.Fact(
            $"dotnet:project:{project.Path}",
            EvidenceKinds.DotNetProject,
            project.Path,
            $".NET {project.Role} project '{project.Name}'.",
            EvidenceSourceKind.Inferred,
            "static SDK, property, framework, dependency, and path classification",
            [DotNetEvidence.Location(project.Path)],
            [
                DotNetEvidence.Measurement("target-frameworks", project.TargetFrameworks.Count, "frameworks"),
                DotNetEvidence.Measurement("packages", project.Packages.Count, "references"),
                DotNetEvidence.Measurement("project-references", project.ProjectReferences.Count, "references"),
                DotNetEvidence.Measurement("framework-references", project.FrameworkReferences.Count, "references"),
                DotNetEvidence.Measurement("unresolved-msbuild-values", project.UnresolvedValueCount, "values"),
            ],
            tags);
    }

    private static EvidenceFact CreatePackageFact(
        DotNetProjectModel project,
        DotNetPackageReference package)
    {
        string family = ClassifyPackage(package.Id);
        bool hasVariants = project.Packages.Count(candidate => candidate.Id.Equals(
            package.Id,
            StringComparison.OrdinalIgnoreCase)) > 1;
        string variantSuffix = hasVariants
            ? $":variant-{StableToken(package.Version ?? "unversioned")}"
            : string.Empty;
        List<string> tags =
        [
            $"package:{package.Id}",
            $"package-family:{family}",
        ];
        AddTag(tags, "version", package.Version);
        return DotNetEvidence.Fact(
            $"dotnet:package:{project.Path}:{package.Id.ToLowerInvariant()}{variantSuffix}",
            EvidenceKinds.PackageReference,
            project.Path,
            $"Project '{project.Name}' references package '{package.Id}'.",
            EvidenceSourceKind.DeclaredAssumed,
            "static PackageReference and central PackageVersion parsing",
            [DotNetEvidence.Location(project.Path)],
            [DotNetEvidence.Measurement("references", 1, "references")],
            tags);
    }

    private static EvidenceFact CreateProjectReferenceFact(
        DotNetProjectModel project,
        DotNetProjectReference reference)
    {
        string identity = reference.ResolvedPath ??
            $"outside-scope-{StableToken(reference.DeclaredPath)}";
        return DotNetEvidence.Fact(
            $"dotnet:project-reference:{project.Path}:{identity}",
            EvidenceKinds.ProjectReference,
            project.Path,
            $"Project '{project.Name}' declares a project reference.",
            EvidenceSourceKind.DeclaredAssumed,
            "static ProjectReference path resolution",
            [DotNetEvidence.Location(project.Path)],
            [DotNetEvidence.Measurement("references", 1, "references")],
            [
                reference.Exists ? "reference:resolved" : "reference:unresolved",
                reference.OutsideScope ? "scope:outside" : "scope:inside",
                $"target:{identity}",
            ]);
    }

    private static EvidenceFact CreateStructureFact(
        string scope,
        IEnumerable<CSharpStructureMetrics> structures)
    {
        CSharpStructureMetrics[] values = [.. structures];
        int files = values.Sum(value => value.Files);
        IReadOnlyList<EvidenceMeasurement> structuralMeasurements =
            CallableStructuralMeasurements.Build(
                values.SelectMany(value => value.CallableStructuralMetrics),
                values.Sum(value => value.StructuralDetectedCallables),
                files,
                values.Sum(value => value.StructuralParserBackedFiles));
        return DotNetEvidence.Fact(
            $"dotnet:source-structure:{scope}",
            EvidenceKinds.SourceStructure,
            scope,
            $"Roslyn C# syntax inventory for '{scope}'.",
            EvidenceSourceKind.Measured,
            "Roslyn syntax-tree declaration, control-flow-node, and callable-distribution counts",
            measurements:
            [
                DotNetEvidence.Measurement("files", files, "files"),
                DotNetEvidence.Measurement("types", values.Sum(value => value.Types), "types"),
                DotNetEvidence.Measurement("public-types", values.Sum(value => value.PublicTypes), "types"),
                DotNetEvidence.Measurement("methods", values.Sum(value => value.Methods), "methods"),
                DotNetEvidence.Measurement("public-methods", values.Sum(value => value.PublicMethods), "methods"),
                DotNetEvidence.Measurement("async-methods", values.Sum(value => value.AsyncMethods), "methods"),
                DotNetEvidence.Measurement("branch-points", values.Sum(value => value.BranchPoints), "nodes"),
                .. structuralMeasurements,
            ],
            tags: [StructuralEvidenceVersions.CallableMetricsV1Tag]);
    }

    internal static DotNetProjectModel? FindOwningProject(
        string filePath,
        IReadOnlyList<DotNetProjectModel> projects) =>
        projects
            .Where(project => project.Directory.Length == 0 ||
                filePath.StartsWith(project.Directory + "/", StringComparison.Ordinal))
            .OrderByDescending(project => project.Directory.Length)
            .ThenBy(project => project.Path, StringComparer.Ordinal)
            .FirstOrDefault();

    private static string ClassifyPackage(string packageId)
    {
        if (packageId.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase))
        {
            return "testing";
        }

        if (packageId.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Dapper", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Mongo", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            return "data";
        }

        if (packageId.Contains("Authentication", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Identity", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("OpenId", StringComparison.OrdinalIgnoreCase))
        {
            return "security";
        }

        if (packageId.Contains("MassTransit", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("RabbitMQ", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Kafka", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("ServiceBus", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Grpc", StringComparison.OrdinalIgnoreCase))
        {
            return "integration";
        }

        if (packageId.Contains("Serilog", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("NLog", StringComparison.OrdinalIgnoreCase))
        {
            return "observability";
        }

        if (packageId.Contains("FluentValidation", StringComparison.OrdinalIgnoreCase))
        {
            return "validation";
        }

        if (packageId.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Swashbuckle", StringComparison.OrdinalIgnoreCase))
        {
            return "web";
        }

        if (packageId.Contains("Avalonia", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Maui", StringComparison.OrdinalIgnoreCase) ||
            packageId.Contains("Blazor", StringComparison.OrdinalIgnoreCase))
        {
            return "ui";
        }

        return "general";
    }

    private static void AddProjectDiagnostics(
        DotNetProjectModel project,
        List<Diagnostic> diagnostics)
    {
        if (project.UnresolvedValueCount > 0)
        {
            diagnostics.Add(DotNetEvidence.Diagnostic(
                "FB3005",
                DiagnosticSeverity.Information,
                $"Project '{project.Path}' contains {project.UnresolvedValueCount} unresolved MSBuild value(s); static evidence may be incomplete.",
                project.Path));
        }

        foreach (DotNetProjectReference reference in project.ProjectReferences)
        {
            if (reference.OutsideScope)
            {
                diagnostics.Add(DotNetEvidence.Diagnostic(
                    "FB3004",
                    DiagnosticSeverity.Warning,
                    $"Project '{project.Path}' references a project outside the analyzed scope.",
                    project.Path));
            }
            else if (!reference.Exists)
            {
                diagnostics.Add(DotNetEvidence.Diagnostic(
                    "FB3003",
                    DiagnosticSeverity.Warning,
                    $"Project '{project.Path}' has an unresolved project reference '{reference.DeclaredPath}'.",
                    project.Path));
            }
        }
    }

    private static string? FindTagValue(IEnumerable<string> tags, string prefix) =>
        tags.FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    private static void AddTag(List<string> tags, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add($"{name}:{value}");
        }
    }

    private static string StableToken(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int codeComparison = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (codeComparison != 0)
        {
            return codeComparison;
        }

        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
