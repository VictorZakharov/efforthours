using System.Text;

namespace EffortHours.Change;

/// <summary>
/// Optional fetch hints, never a selector or proof of object availability. Callers hold
/// the repository acquisition lock and verify local commits before advertising them.
/// </summary>
internal static class GitHubFetchNegotiationCache
{
    internal const int MaximumTips = 32;
    internal const int MaximumBytes = 8 * 1024;
    internal const string Protocol = "github-fetch-negotiation/1.0.0";
    private static readonly UTF8Encoding Encoding = new(false, true);

    internal static string CachePath(string repositoryPath) => repositoryPath + ".fetch-tips";

    public static async Task<IReadOnlyList<string>> ReadAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using FileStream stream = new(
                CachePath(repositoryPath),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: MaximumBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > MaximumBytes)
            {
                return [];
            }

            byte[] bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return Parse(Encoding.GetString(bytes));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            // Losing an optimization must not prevent a complete ordinary fetch.
            return [];
        }
    }

    public static async Task WriteAsync(
        string repositoryPath,
        IEnumerable<string> verifiedObjectIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] tips = [.. verifiedObjectIds.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Take(MaximumTips)];
        if (tips.Length == 0 || tips.Any(tip => !IsObjectId(tip)))
        {
            return;
        }

        string path = CachePath(repositoryPath);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                Protocol + "\n" + string.Join('\n', tips),
                Encoding,
                cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Verified objects remain usable even when their optional hints cannot be saved.
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    internal static IReadOnlyList<string> Parse(string content)
    {
        if (content.Length > MaximumBytes)
        {
            return [];
        }

        string[] lines = content.Split('\n');
        if (lines.Length is < 2 or > MaximumTips + 1 || lines[0] != Protocol ||
            lines.Skip(1).Any(tip => !IsObjectId(tip)))
        {
            return [];
        }

        return [.. lines.Skip(1).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    internal static bool IsObjectId(string? value) => value is { Length: 40 or 64 } &&
        value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');
}
