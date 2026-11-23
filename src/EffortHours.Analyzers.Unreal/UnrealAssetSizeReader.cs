using System.Globalization;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Unreal;

/// <summary>
/// Resolves the serialized size of an Unreal package without loading it. Binary bodies use the
/// common scanner's byte length; Git LFS pointer text uses its declared <c>size</c>. Package bodies
/// are never parsed, deserialized, or executed.
/// </summary>
internal sealed class UnrealAssetSizeReader(
    IRepositoryFileSystem fileSystem,
    string rootPath)
{
    public const int MaximumPointerBytes = 1024;
    public const long MaximumDeclaredBytes = 1L << 40;

    private const string PointerVersion = "version https://git-lfs.github.com/spec/v1";

    private readonly UnrealFileReader _reader = new(fileSystem, rootPath);

    public async Task<UnrealAssetSize> ReadAsync(EvidenceFact fileFact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileFact);
        string path = fileFact.Scope;
        string? sha256 = UnrealEvidence.TagValue(fileFact.Tags, "sha256:");
        decimal bytes = fileFact.Measurements.Where(item => item.Name == "bytes").Sum(item => item.Value);
        if (sha256 is null)
        {
            return Failure(path, $"Unreal asset '{path}' has no common-scanner content digest and was not valued.");
        }

        if (fileFact.Tags.Contains("content:binary", StringComparer.Ordinal))
        {
            return new UnrealAssetSize((long)bytes, sha256, IsPointer: false, null);
        }

        if (bytes > MaximumPointerBytes)
        {
            return Failure(path, $"Unreal asset '{path}' is neither a binary package nor a bounded Git LFS pointer and was not valued.");
        }

        UnrealFileRead read = await _reader.ReadVerifiedAsync(fileFact, MaximumPointerBytes, cancellationToken)
            .ConfigureAwait(false);
        if (read.Content is null)
        {
            return Failure(path, $"Unreal asset '{path}' {read.Failure} and was not valued.");
        }

        return TryParsePointer(read.Content, out long size, out string? oid)
            ? new UnrealAssetSize(size, oid!, IsPointer: true, null)
            : Failure(path, $"Unreal asset '{path}' is text but not a valid Git LFS pointer and was not valued.");
    }

    /// <summary>Parses the Git LFS v1 pointer format: version, then sorted <c>oid sha256:</c> and <c>size</c> keys.</summary>
    public static bool TryParsePointer(byte[] content, out long size, out string? oid)
    {
        size = 0;
        oid = null;
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 3 || lines[0] != PointerVersion)
        {
            return false;
        }

        bool hasSize = false;
        foreach (string line in lines.Skip(1).Where(line => line.Length > 0))
        {
            if (line.StartsWith("oid sha256:", StringComparison.Ordinal))
            {
                string value = line["oid sha256:".Length..];
                if (oid is not null || value.Length != 64 || !value.All(Uri.IsHexDigit))
                {
                    return false;
                }

                oid = value.ToLowerInvariant();
            }
            else if (line.StartsWith("size ", StringComparison.Ordinal))
            {
                if (hasSize || !long.TryParse(line["size ".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out size) ||
                    size > MaximumDeclaredBytes)
                {
                    return false;
                }

                hasSize = true;
            }
            else if (!line.StartsWith("ext-", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return hasSize && oid is not null;
    }

    private static UnrealAssetSize Failure(string path, string message) => new(
        0,
        string.Empty,
        IsPointer: false,
        UnrealEvidence.Diagnostic("FB8301", DiagnosticSeverity.Warning, message, path));
}

/// <summary>
/// One resolved package size. <c>ContentSha256</c> is the content digest in both forms: the file
/// digest for binary bodies and the LFS object ID (also SHA-256 of the body) for pointers.
/// </summary>
internal sealed record UnrealAssetSize(long Bytes, string ContentSha256, bool IsPointer, Diagnostic? Diagnostic);
