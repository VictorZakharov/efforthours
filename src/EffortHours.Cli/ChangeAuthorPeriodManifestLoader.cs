using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ResolvedChangeAuthorPeriodManifest(
    ChangeAuthorPeriodManifest Manifest,
    string ManifestDigest,
    IReadOnlyDictionary<string, string> RepositoryPaths);

internal static class ChangeAuthorPeriodManifestLoader
{
    private const long MaximumManifestBytes = 1024 * 1024;

    public static async Task<ResolvedChangeAuthorPeriodManifest> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new IOException("The author-period manifest path is invalid.", exception);
        }

        string json = await ReadBoundedAsync(fullPath, cancellationToken).ConfigureAwait(false);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeAuthorPeriodManifest,
            json);
        if (!schema.IsValid)
        {
            throw new JsonException(
                "The author-period manifest does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        ChangeAuthorPeriodManifest manifest = ContractJson.Deserialize<ChangeAuthorPeriodManifest>(json);
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0)
        {
            throw new JsonException(
                "The author-period manifest is semantically invalid: " + string.Join(" ", errors));
        }

        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        Dictionary<string, string> repositoryPaths = new(StringComparer.Ordinal);
        foreach (ChangeAuthorPeriodManifestRepository repository in manifest.Repositories)
        {
            repositoryPaths.Add(repository.Id, ResolveRepositoryPath(repository, directory));
        }

        return new ResolvedChangeAuthorPeriodManifest(
            manifest,
            ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest),
            repositoryPaths);
    }

    private static async Task<string> ReadBoundedAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException("The author-period manifest was not found or is inaccessible.", exception);
        }

        await using FileStream ownedStream = stream;
        long length;
        try
        {
            length = ownedStream.Length;
        }
        catch (IOException exception)
        {
            throw new IOException(
                "The author-period manifest could not be inspected safely.",
                exception);
        }

        if (length > MaximumManifestBytes)
        {
            throw new IOException(
                $"The author-period manifest exceeds the {MaximumManifestBytes}-byte input limit.");
        }

        byte[] buffer = GC.AllocateUninitializedArray<byte>((int)MaximumManifestBytes + 1);
        int total = 0;
        try
        {
            while (total < buffer.Length)
            {
                int read = await ownedStream.ReadAsync(buffer.AsMemory(total), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }
        }
        catch (IOException exception)
        {
            throw new IOException(
                "The author-period manifest could not be read safely.",
                exception);
        }

        if (total > MaximumManifestBytes)
        {
            throw new IOException(
                $"The author-period manifest exceeds the {MaximumManifestBytes}-byte input limit.");
        }

        int offset = total >= 3 && buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf
            ? 3
            : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(buffer, offset, total - offset);
        }
        catch (DecoderFallbackException exception)
        {
            throw new JsonException(
                "The author-period manifest must be valid UTF-8 JSON.",
                exception);
        }
    }

    private static string ResolveRepositoryPath(
        ChangeAuthorPeriodManifestRepository repository,
        string manifestDirectory)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(repository.RepositoryPath, manifestDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new IOException(
                $"Repository '{repository.Id}' has an invalid local path.",
                exception);
        }

        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        if (string.Equals(
            Path.TrimEndingDirectorySeparator(fullPath),
            Path.TrimEndingDirectorySeparator(root),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new IOException(
                $"Repository '{repository.Id}' cannot select a filesystem root.");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Repository '{repository.Id}' was not found or is inaccessible.");
        }

        return fullPath;
    }
}
