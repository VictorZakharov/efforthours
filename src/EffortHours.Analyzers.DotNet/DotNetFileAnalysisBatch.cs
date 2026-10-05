using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.DotNet;

internal static class DotNetFileAnalysisBatch
{
    public static async Task<IReadOnlyList<DotNetFileAnalysisEntry>> AnalyzeAsync(
        IRepositoryFileSystem fileSystem,
        string rootPath,
        RepositoryEvidence evidence,
        IReadOnlyList<DotNetProjectModel> projects,
        CancellationToken cancellationToken,
        string? immutableContextIdentity = null)
    {
        EvidenceFact[] files = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File &&
                FindTagValue(fact.Tags, "language:") is "csharp" or "razor" &&
                !fact.Tags.Any(tag => tag is "classification:generated" or "classification:minified" or
                    "classification:vendored" or "content:binary"))
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        DotNetFileAnalysisEntry?[] results = new DotNetFileAnalysisEntry?[files.Length];
        CSharpFileAnalyzer csharp = new(fileSystem, rootPath, immutableContextIdentity);
        List<(EvidenceFact fact, int index)> misses = [];
        for (int index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvidenceFact file = files[index];
            DotNetProjectModel? project = DotNetRepositoryAnalyzer.FindOwningProject(file.Scope, projects);
            string scope = project?.Path ?? ".";
            string? digest = FindTagValue(file.Tags, "sha256:");
            bool isTest = project?.Role == "test" || file.Tags.Contains("classification:test", StringComparer.Ordinal);
            if (digest is not null && FindTagValue(file.Tags, "language:") == "csharp" &&
                csharp.TryGetCompleted(file.Scope, digest, scope, isTest, out CSharpFileAnalysis cached))
                results[index] = new(scope, file.Scope, cached.Structure.Files > 0 ? cached.Structure : null,
                    cached.Facts, cached.Diagnostics);
            else misses.Add((file, index));
        }
        await Parallel.ForEachAsync(
            misses,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = RepositoryAnalysisConcurrency.MaximumFileAnalyses,
            },
            async (entry, itemCancellationToken) =>
            {
                results[entry.index] = await AnalyzeFileAsync(
                    fileSystem,
                    rootPath,
                    entry.fact,
                    projects,
                    itemCancellationToken, immutableContextIdentity).ConfigureAwait(false);
            }).ConfigureAwait(false);
        return [.. results.OfType<DotNetFileAnalysisEntry>()];
    }

    private static async Task<DotNetFileAnalysisEntry?> AnalyzeFileAsync(
        IRepositoryFileSystem fileSystem,
        string rootPath,
        EvidenceFact fileFact,
        IReadOnlyList<DotNetProjectModel> projects,
        CancellationToken cancellationToken,
        string? immutableContextIdentity = null)
    {
        string? language = FindTagValue(fileFact.Tags, "language:");
        if (language is not ("csharp" or "razor") ||
            fileFact.Tags.Contains("classification:generated", StringComparer.Ordinal) ||
            fileFact.Tags.Contains("classification:minified", StringComparer.Ordinal) ||
            fileFact.Tags.Contains("classification:vendored", StringComparer.Ordinal) ||
            fileFact.Tags.Contains("content:binary", StringComparer.Ordinal))
        {
            return null;
        }

        string? expectedSha256 = FindTagValue(fileFact.Tags, "sha256:");
        if (expectedSha256 is null)
        {
            return new DotNetFileAnalysisEntry(
                ProjectScope: ".",
                SourcePath: fileFact.Scope,
                Structure: null,
                Facts: [],
                Diagnostics:
                [
                    DotNetEvidence.Diagnostic(
                        "FB3104",
                        DiagnosticSeverity.Warning,
                        $"File '{fileFact.Scope}' has no common-scanner content digest and was skipped.",
                        fileFact.Scope),
                ]);
        }

        DotNetProjectModel? project = DotNetRepositoryAnalyzer.FindOwningProject(
            fileFact.Scope,
            projects);
        string projectScope = project?.Path ?? ".";
        if (language == "csharp")
        {
            CSharpFileAnalysis analysis = await new CSharpFileAnalyzer(fileSystem, rootPath, immutableContextIdentity)
                .AnalyzeAsync(
                    fileFact.Scope,
                    expectedSha256,
                    projectScope,
                    project?.Role == "test" ||
                        fileFact.Tags.Contains("classification:test", StringComparer.Ordinal),
                    cancellationToken).ConfigureAwait(false);
            return new DotNetFileAnalysisEntry(
                projectScope,
                fileFact.Scope,
                analysis.Structure.Files > 0 ? analysis.Structure : null,
                analysis.Facts,
                analysis.Diagnostics);
        }

        RepositoryAnalysisContribution razor = await new RazorFileAnalyzer(fileSystem, rootPath)
            .AnalyzeAsync(
                fileFact.Scope,
                expectedSha256,
                projectScope,
                cancellationToken).ConfigureAwait(false);
        return new DotNetFileAnalysisEntry(
            projectScope,
            fileFact.Scope,
            Structure: null,
            razor.Facts,
            razor.Diagnostics);
    }

    private static string? FindTagValue(IReadOnlyList<string> tags, string prefix) =>
        tags.FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}

internal sealed record DotNetFileAnalysisEntry(
    string ProjectScope,
    string SourcePath,
    CSharpStructureMetrics? Structure,
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<Diagnostic> Diagnostics);
