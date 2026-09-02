using System.Text.Json;
using EffortHours.Analysis;
using EffortHours.Change;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private async Task<RepositoryInputContext?> LoadRepositoryInputAsync(
        RepositoryInputSelection selection,
        bool allowEvidenceFile,
        RepositoryScanOptions? scanOptions,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repositoryInputs.LoadAsync(
                selection,
                allowEvidenceFile,
                scanOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            DirectoryNotFoundException or
            FileNotFoundException or
            ExternalCommandException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private static async Task<RepositoryInputSelection?> BuildRepositoryInputAsync(
        RepositoryInputOptionsBuilder builder,
        TextWriter standardError)
    {
        if (builder.TryBuild(out RepositoryInputSelection? selection, out string? error))
        {
            return selection;
        }

        await UsageErrorAsync(standardError, error!).ConfigureAwait(false);
        return null;
    }
}
