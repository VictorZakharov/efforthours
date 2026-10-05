using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    // Change's exact local proof uses the same default inspection/classification
    // and fact construction as its ordinary complete-context scanner.
    internal static EvidenceFact InspectImmutableFileFact(string relativePath,
        byte[] bytes, CancellationToken cancellationToken)
    {
        FileInspection inspection = FileInspection.Create(relativePath, bytes,
            new RepositoryScanOptions(), cancellationToken);
        return CreateFileFact(new ScannedFile(relativePath, inspection,
            FileClassifier.Classify(relativePath, inspection), inspection.Bytes, 0));
    }
}
