using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.GDScript;

public sealed class GDScriptRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public GDScriptRepositoryAnalyzer() : this(PhysicalRepositoryFileSystem.Instance) { }

    public GDScriptRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "gdscript";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
        [new("gdscript", LanguageAnalysisSupport.TokenBacked)];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();
        GDScriptTextReader reader = new(_fileSystem, _fileSystem.GetFullPath(repositoryPath));
        EvidenceFact[] files = [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.File && IsMaintained(fact))
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        SortedDictionary<string, string?> manifests = new(StringComparer.Ordinal);
        List<Diagnostic> diagnostics = [];
        foreach (EvidenceFact manifest in files.Where(file =>
            Path.GetFileName(file.Scope).Equals("project.godot", StringComparison.OrdinalIgnoreCase)))
        {
            GDScriptTextReadResult read = await reader.ReadAsync(manifest, cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null) diagnostics.Add(read.Diagnostic);
            else manifests[DirectoryOf(manifest.Scope)] = manifest.Scope;
        }
        if (!manifests.ContainsKey(".")) manifests.Add(".", null);
        Dictionary<string, List<GDScriptFileAnalysis>> analyses = manifests.Keys
            .ToDictionary(directory => directory, _ => new List<GDScriptFileAnalysis>(), StringComparer.Ordinal);
        foreach (EvidenceFact file in files.Where(file =>
            file.Tags.Contains("language:gdscript", StringComparer.Ordinal) &&
            file.Tags.Any(tag => tag is "role:source" or "role:test")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GDScriptTextReadResult read = await reader.ReadAsync(file, cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }
            string directory = manifests.Keys.Where(candidate => candidate == "." ||
                file.Scope.StartsWith(candidate + "/", StringComparison.Ordinal))
                .MaxBy(candidate => candidate.Length)!;
            GDScriptSyntaxAnalysis syntax = GDScriptSyntaxAnalyzer.Analyze(
                read.Text!, file.Tags.Contains("classification:test", StringComparer.Ordinal), cancellationToken);
            analyses[directory].Add(new GDScriptFileAnalysis(file, syntax));
            if (syntax.Confidence == "low") diagnostics.Add(GDScriptEvidence.Diagnostic(
                "FB8102", DiagnosticSeverity.Warning,
                "GDScript token, literal, indentation, or delimiter safeguards were reached; recognized evidence is incomplete.", file.Scope));
        }
        List<EvidenceFact> facts = [];
        foreach ((string directory, string? manifest) in manifests)
        {
            List<GDScriptFileAnalysis> owned = analyses[directory];
            if (owned.Count == 0 && manifest is null) continue;
            facts.Add(GDScriptFactFactory.Package(directory, manifest, owned));
            GDScriptFileAnalysis[] production = [.. owned.Where(file => !file.IsTest)
                .DistinctBy(file => GDScriptEvidence.TagValue(file.File.Tags, "sha256:"), StringComparer.Ordinal)];
            if (production.Length > 0) facts.Add(GDScriptFactFactory.Structure(directory, production));
            facts.AddRange(owned.Where(file => file.IsTest).Select(file => GDScriptFactFactory.Test(directory, file)));
        }
        diagnostics.Add(GDScriptEvidence.Diagnostic("FB8100", DiagnosticSeverity.Information,
            "GDScript analysis used static tokens and project directories only; Godot, editor tools, scenes, resources, plugins, and tests were not executed or resolved. Source excerpts are not emitted."));
        return new RepositoryAnalysisContribution
        {
            Facts = [.. facts.OrderBy(fact => fact.Id, StringComparer.Ordinal)],
            Diagnostics = [.. diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Locations.Count == 0 ? string.Empty : diagnostic.Locations[0].Path, StringComparer.Ordinal)],
        };
    }

    private static bool IsMaintained(EvidenceFact fact) => !fact.Tags.Any(tag => tag is
        "classification:generated" or "classification:minified" or "classification:vendored" or "content:binary");

    private static string DirectoryOf(string path) => path.Contains('/', StringComparison.Ordinal)
        ? path[..path.LastIndexOf('/')]
        : ".";
}
