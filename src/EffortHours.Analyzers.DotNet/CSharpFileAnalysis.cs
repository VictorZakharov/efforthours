using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.DotNet;

internal sealed record CSharpFileAnalysis(
    CSharpStructureMetrics Structure,
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed record CSharpStructureMetrics(
    int Files,
    int Types,
    int PublicTypes,
    int Methods,
    int PublicMethods,
    int AsyncMethods,
    int BranchPoints);
