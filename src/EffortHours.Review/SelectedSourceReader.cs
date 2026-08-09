using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

internal static class SelectedSourceReader
{
    public static async Task<SelectedSourceReadResult> ReadAsync(
        HostReviewSourceContext context,
        RepositoryEvidence evidence,
        string selector,
        int startLine,
        int lineCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evidence);
        string requestedPath = NormalizeSelector(selector);
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        EvidenceFact fact = evidence.Facts.SingleOrDefault(candidate =>
            candidate.Kind == EvidenceKinds.File &&
            candidate.Locations.Any(location =>
                pathComparer.Equals(NormalizeEvidencePath(location.Path), requestedPath)))
            ?? throw new KeyNotFoundException(
                $"Selected source '{requestedPath}' is not an admitted scanner file.");
        string relativePath = fact.Locations
            .Select(location => NormalizeEvidencePath(location.Path))
            .First(path => pathComparer.Equals(path, requestedPath));

        IRepositoryFileSystem fileSystem = context.FileSystem;
        string rootPath = fileSystem.GetFullPath(context.RepositoryRoot);
        string fullPath = fileSystem.GetFullPath(Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(rootPath, fullPath);
        EnsureNoLinks(fileSystem, rootPath, fullPath);

        if (!fileSystem.FileExists(fullPath))
        {
            throw new FileNotFoundException(
                $"Selected source '{relativePath}' no longer exists.",
                relativePath);
        }

        FileAttributes attributes = fileSystem.GetAttributes(fullPath);
        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new InvalidOperationException(
                $"Selected source '{relativePath}' is not a regular file.");
        }

        RepositoryFileMetadata metadata = fileSystem.GetFileMetadata(fullPath);
        if (!metadata.Exists || metadata.Length > HostReviewProtocol.MaximumSourceBytes)
        {
            throw new InvalidOperationException(
                $"Selected source '{relativePath}' exceeds the {HostReviewProtocol.MaximumSourceBytes}-byte safety ceiling.");
        }

        byte[] bytes = await ReadBoundedAsync(
            fileSystem,
            fullPath,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNoLinks(fileSystem, rootPath, fullPath);

        string digest = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        string expectedDigest = fact.Tags.SingleOrDefault(tag =>
            tag.StartsWith("sha256:", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Selected source fact '{fact.Id}' does not record a content digest.");
        if (!string.Equals(digest, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Selected source '{relativePath}' changed after repository evidence was collected.");
        }

        string text = DecodeUtf8(relativePath, bytes);
        string[] lines = SplitLines(text);
        if (startLine > lines.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startLine),
                $"Selected source '{relativePath}' has only {lines.Length} line(s).");
        }

        int availableLineCount = Math.Min(lineCount, lines.Length - startLine + 1);
        int selectedCharacterCount = 0;
        for (int index = 0; index < availableLineCount; index++)
        {
            selectedCharacterCount += lines[startLine - 1 + index].Length;
        }

        List<HostReviewSourceLine> outputLines = [];
        int returnedCharacters = 0;
        for (int index = 0; index < availableLineCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int remaining = HostReviewProtocol.MaximumSourceCharacters - returnedCharacters;
            if (remaining <= 0)
            {
                break;
            }

            string line = lines[startLine - 1 + index];
            int length = Math.Min(line.Length, remaining);
            outputLines.Add(new HostReviewSourceLine
            {
                Line = startLine + index,
                Text = line[..length],
                Truncated = length < line.Length,
            });
            returnedCharacters += length;
            if (length < line.Length)
            {
                break;
            }
        }

        int omittedLines = availableLineCount - outputLines.Count;
        int omittedCharacters = selectedCharacterCount - returnedCharacters;
        return new SelectedSourceReadResult(
            fact,
            new HostReviewSourceExcerpt
            {
                Path = relativePath,
                EvidenceId = fact.Id,
                FileDigest = digest,
                TotalLines = lines.Length,
                RequestedStartLine = startLine,
                RequestedLineCount = lineCount,
                Lines = outputLines,
                ContentTruncated = omittedLines > 0 || omittedCharacters > 0,
            },
            omittedLines,
            omittedCharacters);
    }

    public static string NormalizeSelector(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        string normalized = selector.Replace('\\', '/');
        string platformPath = normalized.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(platformPath) || normalized.StartsWith('/') ||
            normalized.EndsWith('/'))
        {
            throw new ArgumentException(
                "Selected source must be a repository-relative file path.",
                nameof(selector));
        }

        return normalized;
    }

    private static string DecodeUtf8(string relativePath, byte[] bytes)
    {
        if (bytes.AsSpan().Contains((byte)0))
        {
            throw new InvalidOperationException(
                $"Selected source '{relativePath}' appears to be binary.");
        }

        try
        {
            string text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"Selected source '{relativePath}' is not valid UTF-8 text.",
                exception);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        IRepositoryFileSystem fileSystem,
        string fullPath,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[HostReviewProtocol.MaximumSourceBytes + 1];
        int count = 0;
        await using Stream stream = fileSystem.OpenRead(fullPath, bufferSize: 16 * 1024);
        while (count < buffer.Length)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(count, buffer.Length - count),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            count += read;
        }

        if (count > HostReviewProtocol.MaximumSourceBytes)
        {
            throw new InvalidOperationException(
                $"Selected source changed beyond the {HostReviewProtocol.MaximumSourceBytes}-byte safety ceiling.");
        }

        return buffer[..count];
    }

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        string normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        return normalized.EndsWith('\n') ? lines[..^1] : lines;
    }

    private static void EnsureContained(string rootPath, string fullPath)
    {
        string relative = Path.GetRelativePath(rootPath, fullPath);
        if (relative == "." || Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Selected source path escapes the analyzed repository root.");
        }
    }

    private static void EnsureNoLinks(
        IRepositoryFileSystem fileSystem,
        string rootPath,
        string fullPath)
    {
        if ((fileSystem.GetAttributes(rootPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Selected-source access refuses a linked repository root.");
        }

        string relative = Path.GetRelativePath(rootPath, fullPath);
        string current = rootPath;
        foreach (string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((fileSystem.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Selected-source access refuses filesystem links and reparse points.");
            }
        }
    }

    private static string NormalizeEvidencePath(string path) => path.Replace('\\', '/');
}

internal sealed record SelectedSourceReadResult(
    EvidenceFact Fact,
    HostReviewSourceExcerpt Excerpt,
    int OmittedLines,
    int OmittedCharacters);
