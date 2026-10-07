namespace EffortHours.Cli;

internal static class DocumentationCommand
{
    private const string Help = """
        Usage:
          eh docs
          eh docs list
          eh docs show <topic>
          eh docs export <new-directory>
          eh --docs [list|show <topic>|export <new-directory>]

        Read documentation bundled with this installed version, entirely offline.
        list shows topic names; show prints the complete topic as Markdown to stdout.
        export explicitly writes all bundled docs and synthetic historical-refresh
        examples into a new directory. Its parent must exist; existing destinations
        are never overwritten. Relative links and example file layouts are retained.
        This does not run examples, analyze repositories or update time entries.

        Start with 'eh docs show getting-started', or run 'eh examples' for recipes.
        EHE remains experimental and uncalibrated.
        """;

    public static async Task<int> ExecuteAsync(string[] args, TextWriter stdout, TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h" or "help")
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (args.Length == 0 || args is ["list"])
        {
            await stdout.WriteLineAsync("Documentation bundled with this installed version:").ConfigureAwait(false);
            foreach (DocumentationAsset asset in CliDocumentationCatalog.Topics.OrderBy(asset => asset.Topic, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await stdout.WriteLineAsync($"  {asset.Topic} - {asset.Title}").ConfigureAwait(false);
            }

            await stdout.WriteLineAsync("Run 'eh docs show <topic>' to read; 'eh docs export <new-directory>' to save docs and examples offline.")
                .ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (args is ["show", var topic])
        {
            DocumentationAsset? asset = CliDocumentationCatalog.Topics.FirstOrDefault(
                asset => string.Equals(asset.Topic, topic, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                await stderr.WriteLineAsync($"Unknown documentation topic '{topic}'. Run 'eh docs list'.").ConfigureAwait(false);
                return CliExitCodes.UsageError;
            }

            string text = await CliDocumentationCatalog.ReadAsync(asset, cancellationToken).ConfigureAwait(false);
            await stdout.WriteAsync(text.AsMemory(), cancellationToken).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (args is ["export", var destination])
        {
            try
            {
                await DocumentationExport.ExportAsync(destination, cancellationToken).ConfigureAwait(false);
                await stdout.WriteLineAsync($"Exported bundled documentation and examples to {Path.GetFullPath(destination)}.")
                    .ConfigureAwait(false);
                return CliExitCodes.Success;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                await stderr.WriteLineAsync($"eh docs export: {exception.Message}").ConfigureAwait(false);
                return CliExitCodes.InvalidInput;
            }
        }

        await stderr.WriteLineAsync(Help).ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }
}
