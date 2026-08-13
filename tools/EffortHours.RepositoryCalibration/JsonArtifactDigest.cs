using System.Security.Cryptography;
using System.Text;

namespace EffortHours.RepositoryCalibration;

internal static class JsonArtifactDigest
{
    public const string Policy = "sha256:utf8-lf-normalized-text";

    public static async Task<string> ComputeFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Compute(content);
    }

    public static string Compute(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}
