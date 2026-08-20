using System.Security.Cryptography;
using EffortHours.Analysis;
using EffortHours.Analyzers.DotNet;
using EffortHours.Contracts.V1;

namespace EffortHours.Core;

internal static class RepositoryEvidenceIncrementalDeriver
{
    public static async Task<RepositoryEvidence?> TryDeriveAsync(
        RepositoryEvidence previous,
        IRepositoryFileSystem currentFileSystem,
        string currentRootPath,
        IReadOnlyList<string> changedScopePaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(currentFileSystem);
        ArgumentNullException.ThrowIfNull(changedScopePaths);
        if (changedScopePaths.Count == 0)
        {
            return WithoutScopeDiagnostic(previous);
        }

        if (changedScopePaths.Count != 1 ||
            !Path.GetExtension(changedScopePaths[0]).Equals(
                ".cs",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string relativePath = changedScopePaths[0];
        EvidenceFact? previousFileFact = previous.Facts.FirstOrDefault(fact =>
            fact.Kind == EvidenceKinds.File &&
            string.Equals(fact.Id, $"file:{relativePath}", StringComparison.Ordinal));
        if (previousFileFact is null || !IsMaintainedCSharp(previousFileFact) ||
            !TryGetSha256(previousFileFact, out string previousSha256) ||
            !TryGetByteLength(previousFileFact, out long previousLength) ||
            previous.Facts.Count(fact => HasSha256(fact, previousSha256)) != 1)
        {
            return null;
        }

        string fullPath = currentFileSystem.GetFullPath(Path.Combine(
            currentRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!currentFileSystem.FileExists(fullPath))
        {
            return null;
        }

        RepositoryFileMetadata metadata = currentFileSystem.GetFileMetadata(fullPath);
        if (metadata.Length != previousLength)
        {
            return null;
        }

        byte[] bytes = await currentFileSystem.ReadAllBytesAsync(
            fullPath,
            cancellationToken).ConfigureAwait(false);
        if (bytes.LongLength != previousLength)
        {
            return null;
        }

        string currentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (previous.Facts.Any(fact =>
            !string.Equals(fact.Id, previousFileFact.Id, StringComparison.Ordinal) &&
            HasSha256(fact, currentSha256)))
        {
            return null;
        }

        if (!await CSharpEvidenceLineage.TryAdvanceEvidenceAsync(
            currentFileSystem,
            fullPath,
            relativePath,
            bytes,
            cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        EvidenceFact currentFileFact = previousFileFact with
        {
            Tags =
            [
                .. previousFileFact.Tags
                    .Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))
                    .Append($"sha256:{currentSha256}")
                    .Order(StringComparer.Ordinal),
            ],
        };
        return WithoutScopeDiagnostic(previous) with
        {
            Facts =
            [
                .. previous.Facts.Select(fact =>
                    string.Equals(fact.Id, previousFileFact.Id, StringComparison.Ordinal)
                        ? currentFileFact
                        : fact),
            ],
        };
    }

    private static RepositoryEvidence WithoutScopeDiagnostic(RepositoryEvidence evidence) =>
        evidence with
        {
            Diagnostics =
            [
                .. evidence.Diagnostics.Where(diagnostic =>
                    !string.Equals(diagnostic.Code, "FB5205", StringComparison.Ordinal)),
            ],
        };

    private static bool IsMaintainedCSharp(EvidenceFact fact) =>
        fact.Tags.Contains("language:csharp", StringComparer.Ordinal) &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or
            "classification:minified" or
            "classification:vendored" or
            "content:binary");

    private static bool TryGetSha256(EvidenceFact fact, out string sha256)
    {
        string? tag = fact.Tags.FirstOrDefault(tag =>
            tag.StartsWith("sha256:", StringComparison.Ordinal));
        if (tag is null)
        {
            sha256 = string.Empty;
            return false;
        }

        sha256 = tag[7..];
        return true;
    }

    private static bool HasSha256(EvidenceFact fact, string sha256) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains($"sha256:{sha256}", StringComparer.Ordinal);

    private static bool TryGetByteLength(EvidenceFact fact, out long length)
    {
        EvidenceMeasurement? measurement = fact.Measurements.FirstOrDefault(value =>
            value.Name == "bytes" && value.Unit == "bytes");
        if (measurement is null || measurement.Value < 0 ||
            measurement.Value > long.MaxValue || measurement.Value != decimal.Truncate(measurement.Value))
        {
            length = 0;
            return false;
        }

        length = decimal.ToInt64(measurement.Value);
        return true;
    }
}
