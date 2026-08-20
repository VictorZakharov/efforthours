using EffortHours.Analysis;

namespace EffortHours.Change;

internal sealed partial class GitSnapshotFileSystem
{
    internal bool TryGetFirstParentAnalysis(
        out string parentInventoryDigest,
        out IReadOnlyList<string> changedPaths)
    {
        if (_inventory.FirstParentInventory is { } parent &&
            _inventory.ChangedPathsFromFirstParent is { } knownPaths)
        {
            parentInventoryDigest = parent.SourceDigest;
            changedPaths = knownPaths;
            return true;
        }

        parentInventoryDigest = string.Empty;
        changedPaths = [];
        return false;
    }

    internal bool TryCreateFirstParentSnapshot(out GitSnapshotFileSystem parentSnapshot)
    {
        if (_inventory.FirstParentInventory is { } parent &&
            _inventory.FirstParentObjectId is { } parentObjectId)
        {
            parentSnapshot = new GitSnapshotFileSystem(
                _repositoryPath,
                parentObjectId,
                parent,
                _sharedObjectReader,
                _sharedMetadataReader,
                _analysisArtifactCache,
                _versionedAnalysisCache);
            return true;
        }

        parentSnapshot = null!;
        return false;
    }

    public bool TryGetPreviousFileVersion(
        string path,
        out RepositoryFileVersion previousVersion)
    {
        string fullPath = Path.GetFullPath(path);
        if (!TryGetRelativePath(fullPath, out string relativePath) ||
            _inventory.FirstParentInventory is not { } parent ||
            !parent.FilesByPath.TryGetValue(relativePath, out ChangeSnapshotFile? file) ||
            file.IsLink ||
            file.IsSubmodule)
        {
            previousVersion = default;
            return false;
        }

        previousVersion = new RepositoryFileVersion(file.ObjectId);
        return true;
    }

    private bool TryGetRelativePath(string fullPath, out string relativePath)
    {
        if (!IsWithinRoot(fullPath))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = Path.GetRelativePath(RootPath, fullPath).Replace('\\', '/');
        return true;
    }
}
