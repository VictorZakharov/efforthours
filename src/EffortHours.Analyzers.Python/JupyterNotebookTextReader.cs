using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal sealed class JupyterNotebookTextReader(
    IRepositoryFileSystem fileSystem,
    string rootPath)
{
    public const long MaximumBytes = 8 * 1024 * 1024;

    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(fileSystem.GetFullPath(rootPath));

    public async Task<PythonTextReadResult> ReadAsync(
        EvidenceFact fileFact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileFact);
        string path = fileFact.Scope;
        string? expectedSha256 = PythonEvidence.TagValue(fileFact.Tags, "sha256:");
        decimal bytes = fileFact.Measurements
            .Where(measurement => measurement.Name == "bytes")
            .Sum(measurement => measurement.Value);
        if (expectedSha256 is null || bytes > MaximumBytes)
            return Failure(path, "Notebook input is missing a scanner digest or exceeds the eight-megabyte limit.");

        string fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            path.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(_rootPath, fullPath);
        if (relative == ".." || Path.IsPathRooted(relative) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            return Failure(path, "Notebook input resolves outside repository scope.");

        byte[] content;
        try
        {
            content = await _fileSystem.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(path, "Notebook input could not be read.");
        }

        if (content.LongLength > MaximumBytes ||
            !Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
                .Equals(expectedSha256, StringComparison.Ordinal))
            return Failure(path, "Notebook input exceeded its bound or changed after common scanning.");

        try
        {
            int offset = content.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(content, offset, content.Length - offset);
            return text.AsSpan().Contains('\0')
                ? Failure(path, "Notebook input contains binary null data.")
                : new PythonTextReadResult(text, null);
        }
        catch (DecoderFallbackException)
        {
            return Failure(path, "Notebook input is not valid UTF-8 text.");
        }
    }

    private static PythonTextReadResult Failure(string path, string message) => new(
        null,
        PythonEvidence.Diagnostic("FB7011", DiagnosticSeverity.Warning, message + " Analysis was skipped.", path));
}
