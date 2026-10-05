using EffortHours.Analysis;
using EffortHours.Core;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    // Preserve small exact local-analysis lineage without serializing all history.
    // This hint changes scheduling only. Ordinary analysis verifies all evidence.
    private static async Task<bool> PreferOrderedEvidenceLineageAsync(
        IChangeSnapshot before, IChangeSnapshot after, CancellationToken cancellationToken)
    {
        if (before is not GitSnapshotFileSystem gitBefore ||
            after is not GitSnapshotFileSystem gitAfter ||
            !gitAfter.TryGetChangedPathsFrom(before.ObjectId, out IReadOnlyList<string> paths) ||
            paths.Count is < 1 or > 8) return false;
        foreach (string path in paths)
        {
            if (Path.GetExtension(path).ToLowerInvariant() is not (".cs" or ".ts" or ".tsx" or ".sql") ||
                !gitBefore.FilesByPath.TryGetValue(path, out ChangeSnapshotFile? oldFile) ||
                !gitAfter.FilesByPath.TryGetValue(path, out ChangeSnapshotFile? newFile) ||
                oldFile.Mode is not ("100644" or "100755") || oldFile.Mode != newFile.Mode) return false;
            string oldPath = Path.Combine(before.RootPath, path.Replace('/', Path.DirectorySeparatorChar));
            string newPath = Path.Combine(after.RootPath, path.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                long oldLength = before.FileSystem.GetFileMetadata(oldPath).Length;
                if (oldLength > 64 * 1024 || oldLength != after.FileSystem.GetFileMetadata(newPath).Length)
                    return false;
                byte[] oldBytes = await before.FileSystem.ReadAllBytesAsync(oldPath, cancellationToken).ConfigureAwait(false);
                byte[] newBytes = await after.FileSystem.ReadAllBytesAsync(newPath, cancellationToken).ConfigureAwait(false);
                using IDisposable lease = await RepositoryAnalysisConcurrency.AcquireFileAnalysisAsync(
                    RepositoryAnalysisWorkKind.SemanticFileAnalysis, cancellationToken).ConfigureAwait(false);
                bool equivalent = Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase)
                    ? RepositoryEvidenceIncrementalDeriver.IsSmallEquivalentCSharpEdit(oldBytes, newBytes, path, cancellationToken)
                    : RepositoryEvidenceIncrementalDeriver.IsSmallNumberedSourceEdit(oldBytes, newBytes);
                if (!equivalent) return false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
        return true;
    }
}
