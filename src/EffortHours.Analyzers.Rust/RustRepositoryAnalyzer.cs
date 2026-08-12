using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Rust;

public sealed class RustRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public RustRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public RustRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "rust";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("rust", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        RustTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedRustSource)];
        if (sourceFiles.Length == 0 && !allFiles.Any(IsCargoManifest))
            return new RepositoryAnalysisContribution();

        CargoReadResult cargo = await new CargoManifestReader(reader)
            .ReadAsync(allFiles, sourceFiles.Length > 0, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. cargo.Diagnostics];
        List<RustFileAnalysis> analyses = [];
        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RustTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            CargoPackageModel owner = FindOwner(sourceFile.Scope, cargo.Packages);
            RustSyntaxAnalysis syntax = RustSyntaxAnalyzer.Analyze(read.Text!, sourceFile.Scope, owner);
            analyses.Add(new RustFileAnalysis(sourceFile, owner, syntax));
            if (syntax.Confidence == "low")
                diagnostics.Add(RustEvidence.Diagnostic(
                    "FB8802",
                    DiagnosticSeverity.Warning,
                    $"Rust file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
        }

        CargoPackageModel[] packages = [.. cargo.Packages.Select(package => package with
        {
            Role = Role(package, analyses.Where(file => file.Package.Directory == package.Directory)),
        })];
        analyses = [.. analyses.Select(file => file with
        {
            Package = packages.Single(package => package.Directory == file.Package.Directory),
        })];

        List<EvidenceFact> facts = [];
        foreach (CargoPackageModel package in packages.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            RustFileAnalysis[] owned = [.. analyses.Where(file => file.Package.Directory == package.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            facts.Add(RustFactFactory.Package(package, owned));
            facts.AddRange(RustFactFactory.Dependencies(package));
            EvidenceFact? build = RustFactFactory.BuildConfiguration(package);
            if (build is not null) facts.Add(build);
            EvidenceFact? delivery = RustFactFactory.Delivery(package);
            if (delivery is not null) facts.Add(delivery);
            if (owned.Length == 0) continue;
            RustFileAnalysis[] production = [.. owned.Where(IsProduction)];
            if (production.Length > 0) facts.Add(RustFactFactory.SourceStructure(package, production));
            facts.AddRange(owned.SelectMany(RustFactFactory.FileSemantics));
        }

        facts.AddRange(CreateProjectReferences(packages, analyses));
        AddBoundaryDiagnostics(packages, analyses, diagnostics);
        diagnostics.Add(RustEvidence.Diagnostic(
            "FB8800",
            DiagnosticSeverity.Information,
            "The Rust analyzer used bounded static Cargo TOML, target-path, token, use, attribute, and call-shape analysis only; it did not invoke Cargo, rustc, build scripts, procedural macros, feature resolution, dependency resolution, borrow checking, tests, examples, or benchmarks, and emitted no source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<CargoPackageModel> packages,
        IReadOnlyList<RustFileAnalysis> analyses)
    {
        foreach (CargoPackageModel source in packages)
        {
            RustFileAnalysis[] sourceFiles = [.. analyses.Where(file => file.Package.Directory == source.Directory)];
            foreach (CargoPackageModel target in packages.Where(target => target.Directory != source.Directory))
            {
                CargoDependency[] localDependencies = [.. source.Dependencies.Where(dependency =>
                    dependency.PathDirectory == target.Directory)];
                string[] crateNames =
                [
                    target.Name.Replace('-', '_'),
                    .. localDependencies.Select(dependency => dependency.Name.Replace('-', '_')),
                ];
                RustFileAnalysis[] imports = [.. sourceFiles.Where(file =>
                    file.Syntax.ImportedCrates.Any(crateNames.Contains))];
                bool manifestReference = localDependencies.Length > 0;
                bool workspaceReference = source.WorkspaceMembers.Contains(target.Directory, StringComparer.Ordinal);
                if (manifestReference || workspaceReference)
                    yield return RustFactFactory.ProjectReference(
                        source, target, imports, manifestReference, workspaceReference);
            }
        }
    }

    private static void AddBoundaryDiagnostics(
        IReadOnlyList<CargoPackageModel> packages,
        IReadOnlyList<RustFileAnalysis> analyses,
        List<Diagnostic> diagnostics)
    {
        int unresolved = packages.Sum(package => package.UnresolvedValues);
        if (unresolved > 0)
            diagnostics.Add(RustEvidence.Diagnostic(
                "FB8804",
                DiagnosticSeverity.Information,
                $"{unresolved} inherited, dynamic, malformed, external, or out-of-scope Cargo value(s) were not resolved; literal in-scope metadata remains available."));
        int macros = analyses.Sum(file => file.Syntax.Metrics.MacroDefinitions +
            file.Syntax.Metrics.MacroInvocations + file.Syntax.Metrics.AttributeMacros);
        int features = packages.Sum(package => package.Features) +
            analyses.Sum(file => file.Syntax.Metrics.FeatureGates);
        if (macros + features > 0)
            diagnostics.Add(RustEvidence.Diagnostic(
                "FB8805",
                DiagnosticSeverity.Information,
                $"{macros} macro signal(s) and {features} feature/cfg signal(s) were inventoried without expansion or configuration resolution; expanded structure was not guessed."));
        int buildScripts = packages.Sum(package => package.BuildScripts);
        int generatedBindings = analyses.Sum(file => file.Syntax.Metrics.GeneratedBindingSignals);
        if (buildScripts + generatedBindings > 0)
            diagnostics.Add(RustEvidence.Diagnostic(
                "FB8806",
                DiagnosticSeverity.Information,
                $"{buildScripts} build script(s) and {generatedBindings} generated-binding/include signal(s) remain static uncertainty; scripts and generators were not executed and generated bodies were not inferred."));
    }

    private static string Role(CargoPackageModel package, IEnumerable<RustFileAnalysis> files)
    {
        RustSourceMetrics[] metrics = [.. files.Where(IsProduction)
            .Select(file => file.Syntax.Metrics)];
        if (metrics.Any(item => item.ApiSurfaces > 0)) return "server";
        if (metrics.Any(item => item.BackgroundUsages > 0)) return "worker";
        if (metrics.Any(item => item.CliCommands > 0)) return "cli";
        return package.Role;
    }

    private static CargoPackageModel FindOwner(
        string path,
        IReadOnlyList<CargoPackageModel> packages) => packages
        .Where(package => RustPath.IsWithin(path, package.Directory))
        .OrderByDescending(package => package.Directory.Length)
        .ThenBy(package => package.Directory, StringComparer.Ordinal)
        .First();

    private static bool IsMaintainedRustSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File && fact.Tags.Contains("language:rust", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test" or "role:build-configuration") &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsCargoManifest(EvidenceFact fact) => fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:vendored" or "content:binary");

    private static bool IsTest(RustFileAnalysis file) => file.Syntax.Metrics.IsTestTarget ||
        file.File.Tags.Contains("classification:test", StringComparer.Ordinal);

    private static bool IsProduction(RustFileAnalysis file) =>
        !IsTest(file) && !file.Syntax.Metrics.BuildScript;

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
