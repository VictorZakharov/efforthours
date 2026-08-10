using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record ChangeSnapshotInventory(
    string ObjectId,
    IReadOnlyList<ChangeSnapshotFile> Files);

internal static class ChangeSnapshotInventoryBuilder
{
    public static ChangeSnapshotInventory Build(RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(evidence);
        if (semanticErrors.Count > 0)
        {
            throw new ArgumentException(
                "Repository evidence is semantically invalid: " + string.Join(" ", semanticErrors),
                nameof(evidence));
        }

        List<ChangeSnapshotFile> files = [];
        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (EvidenceFact fact in evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File ||
                fact.Id.StartsWith("file:", StringComparison.Ordinal))
            .OrderBy(fact => fact.Id, StringComparer.Ordinal))
        {
            if (fact.Kind != EvidenceKinds.File ||
                !fact.Id.StartsWith("file:", StringComparison.Ordinal))
            {
                throw InvalidEvidence(
                    $"File evidence fact '{fact.Id}' must use the 'file:<relative-path>' identity form.");
            }

            string path = fact.Id[5..];
            ValidateRelativePath(path);
            if (!string.Equals(fact.Scope, path, StringComparison.Ordinal))
            {
                throw InvalidEvidence(
                    $"File evidence fact '{fact.Id}' must use the same relative path for its scope.");
            }

            if (!paths.Add(path))
            {
                throw InvalidEvidence($"File path '{path}' is duplicated in repository evidence.");
            }

            string fileObjectId = ReadSha256(fact);
            long length = ReadLength(fact);
            files.Add(new ChangeSnapshotFile
            {
                Path = path,
                ObjectId = fileObjectId,
                Length = length,
                Mode = "100644",
            });
        }

        files.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        string objectId = ComputeSourceDigest(files);
        string? declaredObjectId = evidence.Repository.SourceDigest;
        if (!IsSha256Digest(declaredObjectId) ||
            !string.Equals(declaredObjectId, objectId, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidEvidence(
                "repository.sourceDigest must be the scanner-compatible SHA-256 digest of " +
                "the ordered file paths and file hashes.");
        }

        return new ChangeSnapshotInventory(objectId, files);
    }

    internal static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path[0] is '/' or '\\' ||
            path.Contains('\\') ||
            path.Contains('\0') ||
            path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw InvalidEvidence(
                $"File evidence path '{path}' must be a canonical slash-separated relative path.");
        }
    }

    private static string ReadSha256(EvidenceFact fact)
    {
        string[] tags = [.. fact.Tags.Where(tag => tag.StartsWith("sha256:", StringComparison.Ordinal))];
        if (tags.Length != 1 || !IsSha256Digest(tags[0]))
        {
            throw InvalidEvidence(
                $"File evidence fact '{fact.Id}' must contain exactly one valid sha256 tag.");
        }

        return tags[0][7..].ToLowerInvariant();
    }

    private static long ReadLength(EvidenceFact fact)
    {
        EvidenceMeasurement[] measurements =
        [
            .. fact.Measurements.Where(measurement =>
                string.Equals(measurement.Name, "bytes", StringComparison.Ordinal)),
        ];
        if (measurements.Length != 1 ||
            !string.Equals(measurements[0].Unit, "bytes", StringComparison.Ordinal) ||
            measurements[0].Value < 0m ||
            measurements[0].Value > long.MaxValue ||
            measurements[0].Value != decimal.Truncate(measurements[0].Value))
        {
            throw InvalidEvidence(
                $"File evidence fact '{fact.Id}' must contain one non-negative integral bytes measurement.");
        }

        return decimal.ToInt64(measurements[0].Value);
    }

    private static string ComputeSourceDigest(IReadOnlyList<ChangeSnapshotFile> files)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (ChangeSnapshotFile file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Path));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(file.ObjectId));
            hash.AppendData([(byte)'\n']);
        }

        return $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    private static bool IsSha256Digest(string? value)
    {
        if (value is not { Length: 71 } ||
            !value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (char character in value.AsSpan(7))
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static ArgumentException InvalidEvidence(string message) =>
        new(message);
}
