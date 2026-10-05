using System.Security.Cryptography;
using System.Text;

namespace EffortHours.Change;

internal sealed partial class GitSnapshotFileSystem
{
    public string? RepositoryTraversalIdentity
    {
        get
        {
            // Links/submodules retain ordinary traversal. Executable regular-file
            // modes have the same traversal attributes as ordinary blobs.
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes("git-traversal/1\n" + RepositoryPathSetIdentity));
            foreach (ChangeSnapshotFile file in _inventory.FilesByPath.Values)
            {
                if (file.Mode is not ("100644" or "100755")) return null;
                if (Path.GetFileName(file.Path) is not (".gitignore" or ".efforthoursignore")) continue;
                hash.AppendData(Encoding.UTF8.GetBytes("\n" + file.Path + "\0" + file.ObjectId));
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }
}
