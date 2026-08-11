using EffortHours.Analysis;
using EffortHours.Analyzers.DotNet;
using EffortHours.Analyzers.Go;
using EffortHours.Analyzers.Java;
using EffortHours.Analyzers.JavaScript;
using EffortHours.Analyzers.Python;
using EffortHours.Analyzers.Sql;
using EffortHours.Contracts.V1;

namespace EffortHours.Core;

public sealed class RepositoryAnalysisPipeline : IRepositoryScanner
{
    private readonly IReadOnlyList<IRepositoryEvidenceAnalyzer> _analyzers;
    private readonly IRepositoryScanner _commonScanner;

    public RepositoryAnalysisPipeline()
        : this(
            new RepositoryScanner(),
            [
                new DotNetRepositoryAnalyzer(),
                new GoRepositoryAnalyzer(),
                new JavaRepositoryAnalyzer(),
                new JavaScriptRepositoryAnalyzer(),
                new PythonRepositoryAnalyzer(),
                new SqlRepositoryAnalyzer(),
                new CoverageReportAnalyzer(),
            ])
    {
    }

    public RepositoryAnalysisPipeline(
        IRepositoryFileSystem fileSystem,
        IRepositoryScanCacheStore? cacheStore = null)
        : this(
            new RepositoryScanner(fileSystem, cacheStore),
            [
                new DotNetRepositoryAnalyzer(fileSystem),
                new GoRepositoryAnalyzer(fileSystem),
                new JavaRepositoryAnalyzer(fileSystem),
                new JavaScriptRepositoryAnalyzer(fileSystem),
                new PythonRepositoryAnalyzer(fileSystem),
                new SqlRepositoryAnalyzer(fileSystem),
                new CoverageReportAnalyzer(fileSystem),
            ])
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
            if (!analyzer.AppliesToAllRepositories &&
                !analyzer.Ecosystems.Any(ecosystem =>
                    evidence.Repository.Ecosystems.Contains(ecosystem, StringComparer.Ordinal)))
            {
                continue;
            }

            RepositoryEvidence analyzerInput = evidence with
            {
                Facts = facts,
                Diagnostics = diagnostics,
            };
            RepositoryAnalysisContribution contribution = await analyzer.AnalyzeAsync(
                repositoryPath,
                analyzerInput,
                cancellationToken).ConfigureAwait(false);
            facts.AddRange(contribution.Facts);
            diagnostics.AddRange(contribution.Diagnostics);
        }

        ApplyLanguageSupport(facts, diagnostics);

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

    private void ApplyLanguageSupport(
        List<EvidenceFact> facts,
        List<Diagnostic> diagnostics)
    {
        Dictionary<string, string> supported = _analyzers
            .SelectMany(analyzer => analyzer.LanguageSupport)
            .GroupBy(item => item.Language, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => DepthOrder(item.Depth))
                    .First().Depth,
                StringComparer.Ordinal);
        HashSet<string> maintainedLanguages = facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .Where(fact => !fact.Tags.Any(tag => tag is
                "classification:generated" or "classification:minified" or
                "classification:vendored" or "content:binary"))
            .Select(fact => fact.Tags.FirstOrDefault(tag =>
                tag.StartsWith("language:", StringComparison.Ordinal)))
            .Where(tag => tag is not null)
            .Select(tag => tag![9..])
            .ToHashSet(StringComparer.Ordinal);

        for (int index = 0; index < facts.Count; index++)
        {
            EvidenceFact fact = facts[index];
            if (fact.Kind != EvidenceKinds.Language)
            {
                continue;
            }

            string? language = fact.Tags.FirstOrDefault(tag =>
                tag.StartsWith("language:", StringComparison.Ordinal))?[9..];
            if (language is null)
            {
                continue;
            }

            List<string> tags = [.. fact.Tags];
            if (supported.TryGetValue(language, out string? depth))
            {
                tags.Add("analysis-status:analyzed");
                tags.Add($"analysis-depth:{depth}");
            }
            else
            {
                tags.Add("analysis-status:inventory-only");
            }

            facts[index] = fact with
            {
                Tags = [.. tags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            };
        }

        string[] unsupported = [.. maintainedLanguages
            .Where(language => !supported.ContainsKey(language))
            .Order(StringComparer.Ordinal)];
        if (unsupported.Length > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB2002",
                Severity = DiagnosticSeverity.Warning,
                Message = "Maintained source is inventory-only because no semantic analyzer is active for: " +
                    string.Join(", ", unsupported) + ". The estimate may be incomplete.",
            });
        }
    }

    private static int DepthOrder(string depth) => depth switch
    {
        LanguageAnalysisSupport.ParserBacked => 0,
        LanguageAnalysisSupport.TokenBacked => 1,
        LanguageAnalysisSupport.Structural => 2,
        _ => 3,
    };

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
