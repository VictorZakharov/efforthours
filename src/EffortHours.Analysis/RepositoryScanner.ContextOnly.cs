using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    /// <summary>Tag recorded on file facts admitted only as analyzer context; such files are never valued.</summary>
    public const string ContextOnlyTag = "scope:context-only";

    // Applied after traversal so cached and shared file artifacts never carry a scope-specific tag.
    private static void ApplyContextOnlyPaths(ScanState state, CancellationToken cancellationToken)
    {
        IReadOnlySet<string>? paths = state.Options.ContextOnlyPaths;
        if (paths is null || paths.Count == 0) return;
        Dictionary<string, int> files = state.Files.Select((file, index) => (file.RelativePath, index))
            .ToDictionary(item => item.RelativePath, item => item.index, StringComparer.Ordinal);
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!files.TryGetValue(path, out int index))
            {
                // A scanner-excluded context file is equally absent from the whole scan, so it owns nothing there either.
                if (state.FileSystem.FileExists(Path.Combine(state.RootPath, path.Replace('/', Path.DirectorySeparatorChar)))) continue;
                throw new InvalidDataException("A context-only path is not a file of the selected scope.");
            }

            ScannedFile file = state.Files[index];
            EvidenceFact fact = file.FileFact ?? CreateFileFact(file);
            state.Files[index] = file with { FileFact = fact with { Tags = NormalizeTags([.. fact.Tags, ContextOnlyTag]) } };
        }
    }

    private static string? ContextOnlyDigest(RepositoryScanOptions options) =>
        options.ContextOnlyPaths is not { Count: > 0 } paths
            ? null
            : string.Join('\n', paths.Order(StringComparer.Ordinal));

    private static void AppendContextOnly(System.Security.Cryptography.IncrementalHash hash, string? contextOnly)
    {
        if (contextOnly is not null) hash.AppendData(Encoding.UTF8.GetBytes("context-only-paths\0" + contextOnly));
    }
}
