using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Docker;

public sealed class DockerRepositoryAnalyzer : IRepositoryEvidenceAnalyzer
{
    private readonly IRepositoryFileSystem _fileSystem;

    public DockerRepositoryAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public DockerRepositoryAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "docker";

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);
        EvidenceFact[] admitted = [.. evidence.Facts
            .Where(IsAdmittedContainerFile)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        if (admitted.Length == 0) return new RepositoryAnalysisContribution();

        DockerTextReader reader = new(_fileSystem, _fileSystem.GetFullPath(repositoryPath));
        List<DockerAnalyzedFile> analyzed = [];
        List<Diagnostic> diagnostics = [];
        int skipped = 0;
        foreach (EvidenceFact file in admitted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DockerTextReadResult read = await reader.ReadAsync(file, cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                skipped++;
                continue;
            }

            DockerArtifactKind kind = ArtifactKind(file.Scope);
            analyzed.Add(kind switch
            {
                DockerArtifactKind.Dockerfile => new DockerAnalyzedFile(
                    file, kind, DockerfileAnalyzer.Analyze(read.Text!), null, null, false),
                DockerArtifactKind.Compose => new DockerAnalyzedFile(
                    file, kind, null, ComposeAnalyzer.Analyze(read.Text!), null, false),
                DockerArtifactKind.DockerIgnore => new DockerAnalyzedFile(
                    file, kind, null, null, DockerIgnoreAnalyzer.Analyze(read.Text!), false),
                _ => throw new InvalidOperationException($"Unsupported Docker artifact kind '{kind}'."),
            });
        }

        ApplyDuplicateNormalization(analyzed);
        HashSet<string> dockerfilePaths = analyzed
            .Where(file => file.Kind == DockerArtifactKind.Dockerfile)
            .Select(file => file.File.Scope)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ComposeReferenceResolution> resolutions = analyzed
            .Where(file => file.Kind == DockerArtifactKind.Compose)
            .ToDictionary(
                file => file.File.Scope,
                file => DockerBuildReferenceResolver.Resolve(
                    file.File.Scope,
                    file.Compose!.BuildReferences,
                    dockerfilePaths),
                StringComparer.Ordinal);

        List<EvidenceFact> facts = [];
        foreach (DockerAnalyzedFile file in analyzed)
        {
            facts.Add(file.Kind switch
            {
                DockerArtifactKind.Dockerfile => DockerFactFactory.Dockerfile(file),
                DockerArtifactKind.Compose => DockerFactFactory.Compose(file, resolutions[file.File.Scope]),
                DockerArtifactKind.DockerIgnore => DockerFactFactory.DockerIgnore(file),
                _ => throw new InvalidOperationException($"Unsupported Docker artifact kind '{file.Kind}'."),
            });
            if (file.ExactDuplicate) facts.Add(DockerFactFactory.DuplicateExclusion(file));
            AddDiagnostics(file, resolutions.GetValueOrDefault(file.File.Scope), diagnostics);
        }

        foreach (ResolvedDockerfileReference reference in resolutions.Values
            .SelectMany(resolution => resolution.Resolved)
            .Distinct()
            .OrderBy(reference => reference.SourcePath, StringComparer.Ordinal)
            .ThenBy(reference => reference.TargetPath, StringComparer.Ordinal))
            facts.Add(DockerFactFactory.Reference(reference));

        facts.Add(DockerFactFactory.Repository(analyzed, [.. resolutions.Values], admitted.Length, skipped));
        diagnostics.Add(DockerEvidence.Diagnostic(
            "FB8900",
            DiagnosticSeverity.Information,
            "The Docker analyzer used bounded static Dockerfile, Compose, and .dockerignore analysis only; it did not invoke Docker or Compose, pull images, build containers, load includes, resolve interpolation or secrets, execute target code, access a network, or emit configured values/source excerpts."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return new RepositoryAnalysisContribution { Facts = facts, Diagnostics = diagnostics };
    }

    private static void AddDiagnostics(
        DockerAnalyzedFile file,
        ComposeReferenceResolution? resolution,
        List<Diagnostic> diagnostics)
    {
        string confidence = file.Kind switch
        {
            DockerArtifactKind.Dockerfile => file.Dockerfile!.Confidence,
            DockerArtifactKind.Compose => file.Compose!.Confidence,
            _ => file.DockerIgnore!.UnresolvedRules > 0 ? "medium" : "high",
        };
        if (confidence == "low")
            diagnostics.Add(DockerEvidence.Diagnostic(
                "FB8902", DiagnosticSeverity.Warning,
                $"Container configuration '{file.File.Scope}' reached a parser safeguard; recognized evidence is incomplete and confidence is low.",
                file.File.Scope));
        else if (confidence == "medium")
            diagnostics.Add(DockerEvidence.Diagnostic(
                "FB8903", DiagnosticSeverity.Information,
                $"Container configuration '{file.File.Scope}' contains dynamic or bounded unsupported structure; recognized static evidence remains visible with wider uncertainty.",
                file.File.Scope));

        if (resolution is not null && resolution.Missing + resolution.Unresolved > 0)
            diagnostics.Add(DockerEvidence.Diagnostic(
                "FB8904", DiagnosticSeverity.Information,
                $"Compose file '{file.File.Scope}' has {resolution.Missing + resolution.Unresolved} local build reference(s) that could not be resolved to a scanner-admitted repository Dockerfile.",
                file.File.Scope));
        if (file.Compose is { Secrets: > 0 } or { SecuritySettings: > 0 } ||
            file.Dockerfile is { SecretOrSshMounts: > 0 })
            diagnostics.Add(DockerEvidence.Diagnostic(
                "FB8905", DiagnosticSeverity.Information,
                $"Container configuration '{file.File.Scope}' contains secret or security-sensitive configuration structure; names and values were not emitted or validated.",
                file.File.Scope));
        if (file.ExactDuplicate)
            diagnostics.Add(DockerEvidence.Diagnostic(
                "FB8906", DiagnosticSeverity.Information,
                $"Container configuration '{file.File.Scope}' is byte-identical to a canonical artifact of the same kind; semantic body effort is valued once.",
                file.File.Scope));
    }

    private static void ApplyDuplicateNormalization(List<DockerAnalyzedFile> files)
    {
        Dictionary<string, string> canonical = files
            .Select(file => new { File = file, Key = DuplicateKey(file) })
            .Where(item => item.Key is not null)
            .GroupBy(item => item.Key!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.File.File.Scope).Min(StringComparer.Ordinal)!,
                StringComparer.Ordinal);
        for (int index = 0; index < files.Count; index++)
        {
            DockerAnalyzedFile file = files[index];
            string? key = DuplicateKey(file);
            files[index] = file with
            {
                ExactDuplicate = key is not null &&
                    !file.File.Scope.Equals(canonical[key], StringComparison.Ordinal),
            };
        }
    }

    private static string? DuplicateKey(DockerAnalyzedFile file)
    {
        string? digest = DockerEvidence.TagValue(file.File.Tags, "sha256:");
        return digest is null ? null : $"{file.Kind}|{digest}";
    }

    private static DockerArtifactKind ArtifactKind(string path)
    {
        string name = Path.GetFileName(path);
        if (name.Equals(".dockerignore", StringComparison.OrdinalIgnoreCase))
            return DockerArtifactKind.DockerIgnore;
        if (name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            return DockerArtifactKind.Compose;
        return DockerArtifactKind.Dockerfile;
    }

    private static bool IsAdmittedContainerFile(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("role:container-configuration", StringComparer.Ordinal) &&
        fact.Tags.Contains("ecosystem:docker", StringComparer.Ordinal) &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");

    private static int CompareDiagnostics(Diagnostic left, Diagnostic right)
    {
        int code = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (code != 0) return code;
        string leftPath = left.Locations.Count == 0 ? string.Empty : left.Locations[0].Path;
        string rightPath = right.Locations.Count == 0 ? string.Empty : right.Locations[0].Path;
        return StringComparer.Ordinal.Compare(leftPath, rightPath);
    }
}
