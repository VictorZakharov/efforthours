using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class RemoteRepositoryEvidence
{
    public static RepositoryEvidence Normalize(
        RepositoryEvidence scanned,
        string repositoryName,
        Diagnostic acquisitionDiagnostic)
    {
        ArgumentNullException.ThrowIfNull(scanned);
        EvidenceFact gitExclusion = new()
        {
            Id = "excluded:.git",
            Kind = EvidenceKinds.ExcludedContent,
            Scope = ".git",
            Summary = "Excluded directory '.git'.",
            Provenance = new EvidenceProvenance
            {
                SourceKind = EvidenceSourceKind.Observed,
                Analyzer = RepositoryScanner.AnalyzerName,
                AnalyzerVersion = RepositoryScanner.AnalyzerVersion,
                Method = "scope and filesystem safety rules",
            },
            Locations = [new EvidenceLocation { Path = ".git" }],
            Tags = ["reason:version-control-metadata", "entry:directory"],
        };
        EvidenceFact[] facts =
        [
            .. scanned.Facts.Select(fact => fact.Id == "inventory:repository"
                ? fact with
                {
                    Measurements =
                    [
                        .. fact.Measurements.Select(measurement =>
                            measurement.Name == "excluded-entries"
                                ? measurement with { Value = measurement.Value + 1m }
                                : measurement),
                    ],
                }
                : fact),
            gitExclusion,
        ];
        Array.Sort(facts, (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));

        return scanned with
        {
            Repository = scanned.Repository with { Name = repositoryName },
            Facts = facts,
            Diagnostics = [.. scanned.Diagnostics, acquisitionDiagnostic],
        };
    }
}
