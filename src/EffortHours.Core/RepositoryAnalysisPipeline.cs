using EffortHours.Analysis;
using EffortHours.Analyzers.DotNet;
using EffortHours.Analyzers.JavaScript;
using EffortHours.Contracts.V1;

namespace EffortHours.Core;

public sealed class RepositoryAnalysisPipeline : IRepositoryScanner
{
    private readonly IReadOnlyList<IRepositoryEvidenceAnalyzer> _analyzers;
    private readonly IRepositoryScanner _commonScanner;

    public RepositoryAnalysisPipeline()
        : this(
            new RepositoryScanner(),
            [new DotNetRepositoryAnalyzer(), new JavaScriptRepositoryAnalyzer()])
    {
    }

    public RepositoryAnalysisPipeline(
        IRepositoryFileSystem fileSystem,
        IRepositoryScanCacheStore? cacheStore = null)
        : this(
            new RepositoryScanner(fileSystem, cacheStore),
            [new DotNetRepositoryAnalyzer(fileSystem), new JavaScriptRepositoryAnalyzer(fileSystem)])
    {
    }

    public RepositoryAnalysisPipeline(
        IRepositoryScanner commonScanner,
        IReadOnlyList<IRepositoryEvidenceAnalyzer> analyzers)
    {
        _commonScanner = commonScanner ?? throw new ArgumentNullException(nameof(commonScanner));
        _analyzers = analyzers ?? throw new ArgumentNullException(nameof(analyzers));
    }

    public async Task<RepositoryEvidence> ScanAsync(
        string repositoryPath,
        RepositoryScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        RepositoryEvidence evidence = await _commonScanner.ScanAsync(
            repositoryPath,
            options,
            cancellationToken).ConfigureAwait(false);
        List<EvidenceFact> facts = [.. evidence.Facts];
        List<Diagnostic> diagnostics = [.. evidence.Diagnostics];

        foreach (IRepositoryEvidenceAnalyzer analyzer in _analyzers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!analyzer.Ecosystems.Any(ecosystem =>
                evidence.Repository.Ecosystems.Contains(ecosystem, StringComparer.Ordinal)))
            {
                continue;
            }

            RepositoryAnalysisContribution contribution = await analyzer.AnalyzeAsync(
                repositoryPath,
                evidence,
                cancellationToken).ConfigureAwait(false);
            facts.AddRange(contribution.Facts);
            diagnostics.AddRange(contribution.Diagnostics);
        }

        string? duplicateFactId = facts
            .GroupBy(fact => fact.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key;
        if (duplicateFactId is not null)
        {
            throw new InvalidOperationException($"Repository analyzers produced duplicate fact ID '{duplicateFactId}'.");
        }

        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CompareDiagnostics);
        return evidence with
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
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
        int pathComparison = StringComparer.Ordinal.Compare(leftPath, rightPath);
        return pathComparison != 0
            ? pathComparison
            : StringComparer.Ordinal.Compare(left.Message, right.Message);
    }
}
