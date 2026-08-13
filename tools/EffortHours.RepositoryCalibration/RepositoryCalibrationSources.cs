using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace EffortHours.RepositoryCalibration;

internal static partial class RepositoryCalibrationReproducer
{
    private static async Task<GitTreeResponse> LoadAndVerifyTreeAsync(
        string ghPath,
        SamplingFamily family,
        CancellationToken cancellationToken)
    {
        string commitJson = await ExternalProcess.RunAsync(
            ghPath,
            ["api", $"repos/{family.RepositoryName}/git/commits/{family.SourceSnapshot.CommitSha}"],
            cancellationToken).ConfigureAwait(false);
        GitCommitResponse commit = JsonSerializer.Deserialize<GitCommitResponse>(commitJson, JsonOptions)
            ?? throw new InvalidDataException($"GitHub returned no commit for {family.RepositoryName}.");
        if (!string.Equals(commit.Tree.Sha, family.SourceSnapshot.GitTreeSha1, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Pinned commit/tree mismatch for {family.RepositoryName}.");
        }

        string treeJson = await ExternalProcess.RunAsync(
            ghPath,
            ["api", $"repos/{family.RepositoryName}/git/trees/{family.SourceSnapshot.GitTreeSha1}?recursive=1"],
            cancellationToken).ConfigureAwait(false);
        GitTreeResponse tree = JsonSerializer.Deserialize<GitTreeResponse>(treeJson, JsonOptions)
            ?? throw new InvalidDataException($"GitHub returned no tree for {family.RepositoryName}.");
        if (tree.Truncated || !family.SourceSnapshot.TreeListingComplete)
        {
            throw new InvalidDataException($"Recursive Git tree is incomplete for {family.RepositoryName}.");
        }

        if (!string.Equals(tree.Sha, family.SourceSnapshot.GitTreeSha1, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Recursive Git tree identity mismatch for {family.RepositoryName}.");
        }

        return tree;
    }

    private static async Task<byte[]> LoadExactBlobAsync(
        string ghPath,
        string repositoryName,
        GitTreeEntry expected,
        CancellationToken cancellationToken)
    {
        string json = await ExternalProcess.RunAsync(
            ghPath,
            ["api", $"repos/{repositoryName}/git/blobs/{expected.Sha}"],
            cancellationToken).ConfigureAwait(false);
        GitBlobResponse blob = JsonSerializer.Deserialize<GitBlobResponse>(json, JsonOptions)
            ?? throw new InvalidDataException($"GitHub returned no blob for '{expected.Path}'.");
        if (!string.Equals(blob.Sha, expected.Sha, StringComparison.Ordinal) ||
            !string.Equals(blob.Encoding, "base64", StringComparison.OrdinalIgnoreCase) ||
            blob.Size != expected.Size)
        {
            throw new InvalidDataException($"GitHub blob metadata mismatch for '{expected.Path}'.");
        }

        return Convert.FromBase64String(blob.Content);
    }

    private static async Task DownloadArchiveAsync(
        string archiveUrl,
        string archivePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(archivePath))
        {
            return;
        }

        string partialPath = archivePath + ".partial";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        using HttpResponseMessage response = await HttpClient.GetAsync(
            archiveUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (FileStream output = new(
                         partialPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         128 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        File.Move(partialPath, archivePath);
    }

    private static async Task VerifyLicenseAsync(
        string snapshotPath,
        SamplingFamily family,
        IReadOnlyList<GitTreeEntry> tree,
        CancellationToken cancellationToken)
    {
        GitTreeEntry? licenseEntry = tree.SingleOrDefault(item =>
            item.Type == "blob" && string.Equals(item.Path, family.License.Path, StringComparison.Ordinal));
        if (licenseEntry is null ||
            !string.Equals(licenseEntry.Sha, family.License.GitBlobSha1, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"License blob mismatch for {family.RepositoryName}.");
        }

        string licensePath = ResolveContainedPath(snapshotPath, family.License.Path);
        string contentHash = await Sha256FileAsync(licensePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(contentHash, family.License.ContentSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"License content mismatch for {family.RepositoryName}.");
        }
    }

    private static string ResolveContainedPath(string root, string relative)
    {
        string rootPath = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(
            Path.Combine(rootPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, comparison))
        {
            throw new InvalidDataException($"Path '{relative}' escapes the snapshot root.");
        }

        return candidate;
    }

    private static async Task<string> Sha256FileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private static string Sha256(ReadOnlySpan<byte> value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant()}";

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(20),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("EffortHours-RepositoryCalibration", "0.1.0"));
        return client;
    }
}
