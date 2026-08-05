using System.Text;
using System.Text.Json;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Analysis;

internal static class RepositoryScanCacheStore
{
    public static async Task<RepositoryScanCache?> LoadAsync(
        string path,
        string repositoryKey,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            RepositoryScanCache cache = ContractJson.Deserialize<RepositoryScanCache>(json);
            return cache.AnalyzerVersion == RepositoryScanner.AnalyzerVersion &&
                   cache.RepositoryKey == repositoryKey &&
                   ContractValidation.Validate(cache).Count == 0
                ? cache
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static async Task SaveAsync(
        string path,
        RepositoryScanCache cache,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(cache);

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The cache path has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                ContractJson.Serialize(cache) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A failed best-effort cleanup must not hide the cache write result.
            }
        }
    }
}
