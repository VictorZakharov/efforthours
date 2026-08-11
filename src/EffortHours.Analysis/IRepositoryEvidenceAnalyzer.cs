using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public interface IRepositoryEvidenceAnalyzer
{
    public string Ecosystem { get; }

    public IReadOnlyList<string> Ecosystems => [Ecosystem];

    public bool AppliesToAllRepositories => false;

    public IReadOnlyList<LanguageAnalysisSupport> LanguageSupport => [];

    public Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default);
}

public sealed record LanguageAnalysisSupport(string Language, string Depth)
{
    public const string ParserBacked = "parser-backed";

    public const string TokenBacked = "token-backed";

    public const string Structural = "structural";
}

public sealed record RepositoryAnalysisContribution
{
    public IReadOnlyList<EvidenceFact> Facts { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}
