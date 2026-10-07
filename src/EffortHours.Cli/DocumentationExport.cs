namespace EffortHours.Cli;

internal static class DocumentationExport
{
    public static async Task ExportAsync(string destination, CancellationToken cancellationToken)
    {
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        string? parent = Path.GetDirectoryName(target);
        if (parent is null || !Directory.Exists(parent))
        {
            throw new IOException("The documentation destination must have an existing parent directory.");
        }

        if (Directory.Exists(target) || File.Exists(target))
        {
            throw new IOException("The documentation destination already exists; choose a new directory.");
        }

        IReadOnlyList<DocumentationAsset> assets = CliDocumentationCatalog.Assets;
        string staging = Path.Combine(parent, ".eh-docs-" + Guid.NewGuid().ToString("N"));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(staging);
            foreach (DocumentationAsset asset in assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.Combine(staging, asset.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using Stream source = CliDocumentationCatalog.Open(asset);
                await using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous);
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, target);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                try
                {
                    Directory.Delete(staging, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Preserve the original failure or cancellation if staging cleanup is unavailable.
                }
            }
        }
    }
}
