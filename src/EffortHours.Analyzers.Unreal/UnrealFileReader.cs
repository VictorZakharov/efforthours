using System.Security.Cryptography;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Unreal;

/// <summary>Bounded, digest-verified, repository-contained reads of small Unreal text inputs.</summary>
internal sealed class UnrealFileReader(IRepositoryFileSystem fileSystem, string rootPath)
{
    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(fileSystem.GetFullPath(rootPath));

    public async Task<UnrealFileRead> ReadVerifiedAsync(
        EvidenceFact fileFact,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileFact);
        string? sha256 = UnrealEvidence.TagValue(fileFact.Tags, "sha256:");
        decimal bytes = fileFact.Measurements.Where(item => item.Name == "bytes").Sum(item => item.Value);
        if (sha256 is null)
        {
            return new UnrealFileRead(null, "has no common-scanner content digest");
        }

        if (bytes > maximumBytes)
        {
            return new UnrealFileRead(null, "exceeds the bounded read limit");
        }

        string fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            fileFact.Scope.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath))
        {
            return new UnrealFileRead(null, "resolves outside repository scope");
        }

        byte[] content;
        try
        {
            content = await _fileSystem.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new UnrealFileRead(null, "could not be read");
        }

        return content.LongLength != bytes ||
            !Convert.ToHexString(SHA256.HashData(content)).Equals(sha256, StringComparison.OrdinalIgnoreCase)
            ? new UnrealFileRead(null, "changed after common scanning")
            : new UnrealFileRead(content, null);
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(_rootPath, path);
        return relative != ".." &&
            !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}

internal sealed record UnrealFileRead(byte[]? Content, string? Failure);
