using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public interface IRepositoryEvidenceAnalyzer
{
    public string Ecosystem { get; }

    public IReadOnlyList<string> Ecosystems => [Ecosystem];

    public Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryAnalysisContribution
{
    public IReadOnlyList<EvidenceFact> Facts { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}
