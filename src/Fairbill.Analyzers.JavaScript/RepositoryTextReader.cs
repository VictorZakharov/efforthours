using System.Security.Cryptography;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Contracts.V1;

namespace Fairbill.Analyzers.JavaScript;

internal sealed class RepositoryTextReader(
    IRepositoryFileSystem fileSystem,
    string rootPath)
{
    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(
        fileSystem.GetFullPath(rootPath));

    public async Task<RepositoryTextReadResult> ReadAsync(
        EvidenceFact fileFact,
        long maximumBytes,
        string diagnosticCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileFact);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        string path = fileFact.Scope;
        string? expectedSha256 = JavaScriptEvidence.FindTagValue(fileFact.Tags, "sha256:");
        if (expectedSha256 is null)
        {
            return Failure(
                diagnosticCode,
                path,
                $"File '{path}' has no common-scanner content digest and was skipped.");
        }

        EvidenceMeasurement? byteMeasurement = fileFact.Measurements.FirstOrDefault(
            measurement => measurement.Name == "bytes");
        if (byteMeasurement?.Value > maximumBytes)
        {
            return Failure(
                diagnosticCode,
                path,
                $"File '{path}' exceeds the static analyzer's {maximumBytes} byte limit and was skipped.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            path.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(fullPath))
        {
            return Failure(
                diagnosticCode,
                path,
                $"File '{path}' resolves outside the repository scope and was skipped.");
        }

        byte[] bytes;
        try
        {
            bytes = await _fileSystem.ReadAllBytesAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(
                diagnosticCode,
                path,
                $"File '{path}' could not be read and was skipped.");
        }

        if (bytes.LongLength > maximumBytes)
        {
            return Failure(
                diagnosticCode,
                path,
                $"File '{path}' exceeds the static analyzer's {maximumBytes} byte limit and was skipped.");
        }

        string actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!actualSha256.Equals(expectedSha256, StringComparison.Ordinal))
        {
            return Failure(
                diagnosticCode,
                path,
                $"File '{path}' changed after common scanning; semantic evidence was skipped.");
        }

        using MemoryStream stream = new(bytes, writable: false);
        using StreamReader reader = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true);
        string text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return new RepositoryTextReadResult(text, null);
    }

    private bool IsWithinRoot(string path)
    {
        string relative = Path.GetRelativePath(_rootPath, path);
        return relative != ".." &&
            !Path.IsPathRooted(relative) &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static RepositoryTextReadResult Failure(
        string code,
        string path,
        string message) => new(
            null,
            JavaScriptEvidence.Diagnostic(code, DiagnosticSeverity.Warning, message, path));
}

internal sealed record RepositoryTextReadResult(string? Text, Diagnostic? Diagnostic);
