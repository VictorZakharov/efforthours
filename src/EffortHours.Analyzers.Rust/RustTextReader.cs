using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Rust;

internal sealed class RustTextReader(IRepositoryFileSystem fileSystem, string rootPath)
{
    public const long MaximumBytes = 8 * 1024 * 1024;

    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(
        fileSystem.GetFullPath(rootPath));

    public async Task<RustTextReadResult> ReadAsync(
        EvidenceFact fileFact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileFact);
        string path = fileFact.Scope;
        string? expectedSha256 = RustEvidence.TagValue(fileFact.Tags, "sha256:");
        if (expectedSha256 is null)
            return Failure(path, $"Rust input '{path}' has no common-scanner content digest and was skipped.");

        decimal bytes = fileFact.Measurements
            .Where(measurement => measurement.Name == "bytes")
            .Sum(measurement => measurement.Value);
        if (bytes > MaximumBytes)
            return Failure(path, $"Rust input '{path}' exceeds the eight-megabyte analysis limit and was skipped.");

        string fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            path.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath))
            return Failure(path, $"Rust input '{path}' resolves outside repository scope and was skipped.");

        byte[] content;
        try
        {
            content = await _fileSystem.ReadAllBytesAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(path, $"Rust input '{path}' could not be read and was skipped.");
        }

        if (content.LongLength > MaximumBytes)
            return Failure(path, $"Rust input '{path}' exceeds the eight-megabyte analysis limit and was skipped.");

        string actualSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!actualSha256.Equals(expectedSha256, StringComparison.Ordinal))
            return Failure(path, $"Rust input '{path}' changed after common scanning; semantic evidence was skipped.");

        try
        {
            using MemoryStream stream = new(content, writable: false);
            using StreamReader reader = new(
                stream,
                new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: true);
            string text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return text.AsSpan().Contains('\0')
                ? Failure(path, $"Rust input '{path}' contains binary null data and was skipped.")
                : new RustTextReadResult(text, null);
        }
        catch (DecoderFallbackException)
        {
            return Failure(path, $"Rust input '{path}' is not valid supported text and was skipped.");
        }
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(_rootPath, path);
        return relative != ".." && !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static RustTextReadResult Failure(string path, string message) => new(
        null,
        RustEvidence.Diagnostic("FB8801", DiagnosticSeverity.Warning, message, path));
}

internal sealed record RustTextReadResult(string? Text, Diagnostic? Diagnostic);
