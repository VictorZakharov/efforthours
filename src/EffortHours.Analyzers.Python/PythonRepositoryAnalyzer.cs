using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

public sealed class PythonRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public PythonRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public PythonRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "python";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("python", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        PythonTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] allFiles = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact[] sourceFiles = [.. allFiles.Where(IsMaintainedPythonSource)];
        PythonProjectReadResult project = await new PythonProjectReader(reader)
            .ReadAsync(allFiles, cancellationToken).ConfigureAwait(false);
        List<Diagnostic> diagnostics = [.. project.Diagnostics];
        List<PythonFileAnalysis> analyses = [];

        foreach (EvidenceFact sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PythonTextReadResult read = await reader.ReadAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            PythonPackageModel owner = FindOwner(sourceFile.Scope, project.Packages);
            PythonSyntaxAnalysis syntax = PythonSyntaxAnalyzer.Analyze(read.Text!, sourceFile.Scope);
            analyses.Add(new PythonFileAnalysis(sourceFile, owner, syntax));
            if (syntax.Confidence == "low")
            {
                diagnostics.Add(PythonEvidence.Diagnostic(
                    "FB7002",
                    DiagnosticSeverity.Warning,
                    $"Python file '{sourceFile.Scope}' reached a tokenizer or structure safeguard; recognized evidence is incomplete and confidence is low.",
                    sourceFile.Scope));
            }
        }

        List<EvidenceFact> facts = [];
        foreach (PythonPackageModel package in project.Packages.OrderBy(item => item.Directory, StringComparer.Ordinal))
        {
            PythonFileAnalysis[] owned = [.. analyses
                .Where(file => file.Package.Directory == package.Directory)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            facts.Add(PythonFactFactory.Package(package, owned));
            facts.AddRange(PythonFactFactory.Dependencies(package));
            if (owned.Length > 0)
            {
                facts.Add(PythonFactFactory.SourceStructure(package, owned));
                facts.AddRange(owned.SelectMany(PythonFactFactory.FileSemantics));
            }
        }

        facts.AddRange(CreateProjectReferences(project.Packages, analyses));
        diagnostics.Add(PythonEvidence.Diagnostic(
            "FB7000",
            DiagnosticSeverity.Information,
            "The Python analyzer used bounded static metadata, token, and indentation analysis only; it did not invoke Python, import modules, resolve an environment, install dependencies, execute setup.py, or emit source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private static IEnumerable<EvidenceFact> CreateProjectReferences(
        IReadOnlyList<PythonPackageModel> packages,
        IReadOnlyList<PythonFileAnalysis> analyses)
    {
        foreach (PythonPackageModel source in packages)
        {
            PythonFileAnalysis[] sourceFiles = [.. analyses.Where(file => file.Package == source)];
            foreach (PythonPackageModel target in packages.Where(target => target != source))
            {
                string importRoot = target.Name.Replace('-', '_');
                PythonFileAnalysis[] matching = [.. sourceFiles.Where(file =>
                    file.Syntax.Metrics.ImportsSeen.Any(imported =>
                        imported.TrimStart('.').Equals(importRoot, StringComparison.Ordinal) ||
                        imported.TrimStart('.').StartsWith(importRoot + ".", StringComparison.Ordinal)))];
                if (matching.Length > 0)
                {
                    yield return PythonFactFactory.ProjectReference(source, target, matching);
                }
            }
        }
    }

    private static PythonPackageModel FindOwner(
        string path,
        IReadOnlyList<PythonPackageModel> packages) => packages
        .Where(package => IsWithin(path, package.Directory))
        .OrderByDescending(package => package.Directory.Length)
        .ThenBy(package => package.Directory, StringComparer.Ordinal)
        .First();

    private static bool IsMaintainedPythonSource(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:python", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static bool IsWithin(string path, string directory) =>
        directory == "." || path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
