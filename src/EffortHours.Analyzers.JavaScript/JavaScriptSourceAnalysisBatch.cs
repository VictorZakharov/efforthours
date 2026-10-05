using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptSourceAnalysisBatch
{
    public static async Task<IReadOnlyList<JavaScriptSourceAnalysisEntry>> AnalyzeAsync(
        JavaScriptSourceAnalyzer analyzer,
        RepositoryEvidence evidence,
        IReadOnlyList<JavaScriptPackageModel> packages,
        CancellationToken cancellationToken)
    {
        EvidenceFact[] files = [.. evidence.Facts
            .Where(JavaScriptRepositoryAnalyzer.IsMaintainedSource)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        JavaScriptSourceAnalysisEntry?[] results =
            new JavaScriptSourceAnalysisEntry?[files.Length];
        List<(EvidenceFact fact, int index)> misses = [];
        for (int index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvidenceFact file = files[index];
            JavaScriptPackageModel? package = JavaScriptRepositoryAnalyzer.FindOwningPackage(file.Scope, packages);
            if (analyzer.TryGetCompleted(file, package, out JavaScriptFileAnalysis cached))
                results[index] = new(file.Scope, package?.Scope ?? ".", cached);
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
                JavaScriptPackageModel? package =
                    JavaScriptRepositoryAnalyzer.FindOwningPackage(
                        entry.fact.Scope,
                        packages);
                JavaScriptFileAnalysis analysis = await analyzer.AnalyzeAsync(
                    entry.fact,
                    package,
                    itemCancellationToken).ConfigureAwait(false);
                results[entry.index] = new JavaScriptSourceAnalysisEntry(
                    entry.fact.Scope,
                    package?.Scope ?? ".",
                    analysis);
            }).ConfigureAwait(false);
        return [.. results.Select(result => result!)];
    }
}

internal sealed record JavaScriptSourceAnalysisEntry(
    string Path,
    string PackageScope,
    JavaScriptFileAnalysis Analysis);
