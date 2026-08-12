using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Terraform;

public sealed class TerraformRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public TerraformRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public TerraformRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "terraform";

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport { get; } =
    [
        new("terraform", LanguageAnalysisSupport.TokenBacked),
        new("hcl", LanguageAnalysisSupport.TokenBacked),
    ];

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        TerraformTextReader reader = new(_fileSystem, rootPath);
        EvidenceFact[] admittedFiles = [.. evidence.Facts
            .Where(IsTerraformOrHclFile)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        List<(EvidenceFact File, TerraformFileAnalysis Analysis, bool ExactDuplicate)> analyzed = [];
        List<Diagnostic> diagnostics = [];
        int excluded = 0;
        int skipped = 0;

        foreach (EvidenceFact file in admittedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMaintained(file))
            {
                excluded++;
                continue;
            }

            TerraformTextReadResult read = await reader.ReadAsync(file, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                skipped++;
                continue;
            }

            TerraformArtifactAssessment artifact = TerraformArtifactClassifier.Classify(file.Scope);
            TerraformFileAnalysis analysis = TerraformSemanticAnalyzer.Analyze(read.Text!, artifact);
            analyzed.Add((file, analysis, false));
            AddFileDiagnostics(file, analysis, diagnostics);
        }

        excluded += ApplyDuplicateNormalization(analyzed);

        IReadOnlyList<TerraformModuleModel> modules = TerraformModuleGraph.Build(analyzed);
        List<EvidenceFact> facts = BuildFacts(admittedFiles, analyzed, modules, excluded, skipped, diagnostics);
        diagnostics.Add(TerraformEvidence.Diagnostic(
            "FB8600",
            DiagnosticSeverity.Information,
            "The Terraform/HCL analyzer used bounded static token and structural analysis only; it did not run Terraform, load providers or schemas, fetch modules, contact backends or networks, evaluate interpolation or policy, inspect state or plans, or emit source values/excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static List<EvidenceFact> BuildFacts(
        IReadOnlyList<EvidenceFact> admittedFiles,
        List<(EvidenceFact File, TerraformFileAnalysis Analysis, bool ExactDuplicate)> analyzed,
        IReadOnlyList<TerraformModuleModel> modules,
        int excluded,
        int skipped,
        List<Diagnostic> diagnostics)
    {
        List<EvidenceFact> facts = [];
        Dictionary<string, TerraformAnalyzedFile> modeled = modules
            .SelectMany(module => module.Files)
            .ToDictionary(file => file.File.Scope, StringComparer.Ordinal);
        foreach ((EvidenceFact file, TerraformFileAnalysis analysis, bool duplicate) in analyzed)
        {
            TerraformAnalyzedFile modeledFile = modeled[file.Scope];
            facts.Add(TerraformFactFactory.Artifact(file, analysis, modeledFile.ModuleDirectory, duplicate));
            if (duplicate)
            {
                string canonical = analyzed
                    .Where(item => !item.ExactDuplicate &&
                        DuplicateKey(item.File, item.Analysis.Artifact.Role) ==
                        DuplicateKey(file, analysis.Artifact.Role))
                    .Select(item => item.File.Scope)
                    .Min(StringComparer.Ordinal)!;
                facts.Add(TerraformFactFactory.DuplicateExclusion(modeledFile, canonical));
            }
        }

        foreach (TerraformModuleModel module in modules)
        {
            facts.Add(TerraformFactFactory.Module(module));
            if (module.CanonicalFiles.Any(file =>
                file.Analysis.Artifact.SupportsTerraformSemantics &&
                !file.Analysis.Artifact.IsTest &&
                !file.Analysis.Artifact.IsCliConfiguration &&
                file.Analysis.Document.Blocks.Count + file.Analysis.Document.Attributes.Count > 0))
            {
                facts.Add(TerraformFactFactory.Infrastructure(module));
            }

            facts.AddRange(TerraformFactFactory.SemanticFacts(module));
        }

        foreach (TerraformLocalModuleReference reference in TerraformModuleGraph.ResolveLocalReferences(modules))
        {
            facts.Add(TerraformFactFactory.ProjectReference(reference));
            if (reference.OutsideRepository || reference.MissingTarget || reference.TargetDirectory is null)
            {
                diagnostics.Add(TerraformEvidence.Diagnostic(
                    "FB8604",
                    DiagnosticSeverity.Information,
                    $"Terraform file '{reference.SourcePath}' has a local module source that could not be resolved to a discovered repository module.",
                    reference.SourcePath));
            }
        }

        facts.Add(TerraformFactFactory.Repository(
            admittedFiles,
            modules,
            analyzed.Count,
            excluded,
            skipped));
        return facts;
    }

    private static void AddFileDiagnostics(
        EvidenceFact file,
        TerraformFileAnalysis analysis,
        List<Diagnostic> diagnostics)
    {
        HclDocumentAnalysis document = analysis.Document;
        if (document.ParserConfidence == "low")
        {
            diagnostics.Add(TerraformEvidence.Diagnostic(
                "FB8602",
                DiagnosticSeverity.Warning,
                $"Terraform/HCL file '{file.Scope}' reached a parser safeguard or is structurally incomplete; recognized evidence is incomplete and confidence is low.",
                file.Scope));
        }
        else if (document.UnknownConstructs > 0 || !analysis.Artifact.SupportsTerraformSemantics ||
            analysis.Metrics.ModuleSources.Any(source => source.Dynamic))
        {
            diagnostics.Add(TerraformEvidence.Diagnostic(
                "FB8603",
                DiagnosticSeverity.Information,
                $"Terraform/HCL file '{file.Scope}' contains generic, dynamic, or unrecognized structure; it remains visible without guessed runtime semantics.",
                file.Scope));
        }

        if (analysis.Metrics.CredentialLikeAttributes > 0)
        {
            diagnostics.Add(TerraformEvidence.Diagnostic(
                "FB8605",
                DiagnosticSeverity.Information,
                $"Terraform/HCL file '{file.Scope}' contains credential-like configuration names; values were not emitted or validated.",
                file.Scope));
        }
    }

    private static bool IsTerraformOrHclFile(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File && fact.Tags.Any(tag => tag is
            "language:terraform" or "language:hcl");

    private static bool IsMaintained(EvidenceFact fact) => !fact.Tags.Any(tag => tag is
        "classification:generated" or "classification:minified" or
        "classification:vendored" or "content:binary" or
        "classification:dependency-lock");

    private static Dictionary<string, string> CanonicalPathByDuplicateKey(
        IEnumerable<EvidenceFact> files) => files
        .Where(IsMaintained)
        .Select(file => new
        {
            File = file,
            Key = DuplicateKey(file, TerraformArtifactClassifier.Classify(file.Scope).Role),
        })
        .Where(item => item.Key is not null)
        .GroupBy(item => item.Key!, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Select(item => item.File.Scope).Min(StringComparer.Ordinal)!,
            StringComparer.Ordinal);

    private static int ApplyDuplicateNormalization(
        List<(EvidenceFact File, TerraformFileAnalysis Analysis, bool ExactDuplicate)> analyzed)
    {
        Dictionary<string, string> canonicalByKey = CanonicalPathByDuplicateKey(
            analyzed.Select(item => item.File));
        int duplicates = 0;
        for (int index = 0; index < analyzed.Count; index++)
        {
            (EvidenceFact file, TerraformFileAnalysis analysis, _) = analyzed[index];
            string? key = DuplicateKey(file, analysis.Artifact.Role);
            bool duplicate = key is not null &&
                !StringComparer.Ordinal.Equals(canonicalByKey[key], file.Scope);
            analyzed[index] = (file, analysis, duplicate);
            if (duplicate) duplicates++;
        }

        return duplicates;
    }

    private static string? DuplicateKey(EvidenceFact file, string role)
    {
        string? digest = TerraformEvidence.TagValue(file.Tags, "sha256:");
        return digest is null ? null : $"{role}|{digest}";
    }

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
