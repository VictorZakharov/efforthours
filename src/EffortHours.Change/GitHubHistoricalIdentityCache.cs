using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class GitHubHistoricalIdentityCache
{
    private const int MaximumBytes = 16 * 1024;
    private const string FileName = "eh-historical-identity.json";
    private sealed record Entry(string Protocol, string InputDigest, DateTimeOffset ObservedAt, string[] Emails);

    public static async Task<string[]?> ReadAsync(string repository, string input, CancellationToken token)
    {
        try
        {
            await using FileStream stream = new(Path.Combine(repository, FileName), FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] bytes = new byte[MaximumBytes + 1];
            int count = 0;
            while (count < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(count), token).ConfigureAwait(false);
                if (read == 0) break;
                count += read;
            }
            if (count == 0 || count > MaximumBytes) return null;
            Entry? entry = JsonSerializer.Deserialize<Entry>(bytes.AsSpan(0, count), ContractJson.Options);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return entry is { Protocol: "historical-identity-cache/1.0.0" } && entry.InputDigest == input &&
                entry.ObservedAt <= now && now - entry.ObservedAt <= TimeSpan.FromHours(24) &&
                entry.Emails is { } emails && emails.Length <= ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor &&
                entry.Emails.All(email => !string.IsNullOrWhiteSpace(email) && email.Length <= ChangeAuthorPeriodManifestLimits.MaximumAliasLength)
                ? entry.Emails : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static async Task WriteAsync(string repository, string input, string[] emails, CancellationToken token)
    {
        string path = Path.Combine(repository, FileName);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Entry("historical-identity-cache/1.0.0", input, DateTimeOffset.UtcNow, emails), ContractJson.Options);
            if (bytes.Length > MaximumBytes) return;
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // This private performance cache is optional; immutable Git selection remains authoritative.
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }
}
