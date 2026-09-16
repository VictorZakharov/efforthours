using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    private static List<EvidenceFact> ApplyReviewedVendorManifest(
        ScanState state, CancellationToken cancellationToken)
    {
        ReviewedVendorManifest? manifest = state.Options.VendorManifest;
        if (manifest is null) return [];
        string digest = ReviewedVendorManifestValidation.ComputeDigest(manifest);
        Dictionary<string, int> files = state.Files.Select((file, index) => (file.RelativePath, index))
            .ToDictionary(item => item.RelativePath, item => item.index, StringComparer.Ordinal);
        List<EvidenceFact> decisions = [];
        foreach (ReviewedVendorEntry entry in manifest.Files.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!files.TryGetValue(entry.Path, out int index))
                throw new InvalidDataException("A reviewed vendor file is missing, excluded, unreadable, or a filesystem link; review the manifest against the selected scope.");
            ScannedFile file = state.Files[index];
            if (file.Inspection.Sha256 != entry.Sha256)
                throw new InvalidDataException("A reviewed vendor content hash no longer matches; review local modifications before excluding the file.");
            file = file with
            {
                Classification = file.Classification with { Role = "vendored", IsVendored = true, IsTest = false, IsComponentManifest = false },
                FileFact = null,
            };
            EvidenceFact fact = CreateFileFact(file);
            state.Files[index] = file with
            {
                FileFact = fact with
                {
                    Tags = NormalizeTags([.. fact.Tags, "ownership:reviewed-vendor", "ownership-manifest:" + digest]),
                }
            };
            decisions.Add(new EvidenceFact
            {
                Id = "ownership:reviewed-vendor:" + entry.Path,
                Kind = "ownership-decision",
                Scope = entry.Path,
                Summary = "A hash-verified, caller-reviewed third-party body is excluded from maintained implementation; owned adaptations and integration remain separate.",
                Provenance = new EvidenceProvenance
                {
                    SourceKind = EvidenceSourceKind.DeclaredAssumed,
                    Analyzer = AnalyzerName,
                    AnalyzerVersion = AnalyzerVersion,
                    Method = ReviewedVendorManifest.Protocol + "; exact path and SHA-256 verification; ownership is caller-reviewed, not independently verified",
                },
                Locations = [new EvidenceLocation { Path = entry.Path }],
                Tags = ["classification:vendored", "ownership:reviewed-vendor", "ownership-manifest:" + digest, "sha256:" + entry.Sha256],
            });
        }
        return decisions;
    }
}
