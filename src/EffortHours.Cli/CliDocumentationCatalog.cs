using System.Reflection;
using System.Text;

namespace EffortHours.Cli;

internal static class CliDocumentationCatalog
{
    private const string Prefix = "EffortHours.Cli.Documentation.";
    private const int MaximumFiles = 256;
    private const long MaximumFileBytes = 2 * 1024 * 1024;
    private const long MaximumTotalBytes = 16 * 1024 * 1024;
    private static readonly Assembly Assembly = typeof(CliDocumentationCatalog).Assembly;
    private static readonly Lazy<IReadOnlyList<DocumentationAsset>> Catalog = new(Load);

    public static IReadOnlyList<DocumentationAsset> Assets => Catalog.Value;

    public static IEnumerable<DocumentationAsset> Topics => Assets.Where(asset => asset.Topic is not null);

    public static Stream Open(DocumentationAsset asset) =>
        Assembly.GetManifestResourceStream(Prefix + asset.Path)
        ?? throw new InvalidOperationException("Bundled documentation is missing.");

    public static async Task<string> ReadAsync(DocumentationAsset asset, CancellationToken cancellationToken)
    {
        using Stream stream = Open(asset);
        using MemoryStream buffer = new((int)asset.Bytes);
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    private static List<DocumentationAsset> Load()
    {
        List<DocumentationAsset> assets = [];
        HashSet<string> topics = new(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (string name in Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            string path = name[Prefix.Length..];
            if (path.Length == 0 || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            {
                throw new InvalidOperationException("Bundled documentation has an invalid path.");
            }

            using Stream stream = Assembly.GetManifestResourceStream(name)!;
            long bytes = stream.Length;
            totalBytes += bytes;
            if (assets.Count >= MaximumFiles || bytes > MaximumFileBytes || totalBytes > MaximumTotalBytes)
            {
                throw new InvalidOperationException("Bundled documentation exceeds its resource bounds.");
            }

            string? topic = Topic(path);
            if (topic is not null && !topics.Add(topic))
            {
                throw new InvalidOperationException("Bundled documentation has duplicate topics.");
            }

            string title = path;
            if (topic is not null)
            {
                using StreamReader reader = new(stream, new UTF8Encoding(false, true));
                while (reader.ReadLine() is { } line)
                {
                    if (line.StartsWith("# ", StringComparison.Ordinal))
                    {
                        title = line[2..];
                        break;
                    }
                }
            }

            assets.Add(new(path, topic, title, bytes));
        }

        if (assets.Count == 0)
        {
            throw new InvalidOperationException("Bundled documentation is missing.");
        }

        return assets;
    }

    private static string? Topic(string path) => path switch
    {
        "README.md" => "getting-started",
        "CHANGELOG.md" => "release-notes",
        "THIRD-PARTY-NOTICES.md" => "third-party-notices",
        "docs/README.md" => "documentation-index",
        "examples/historical-refresh/README.md" => "historical-refresh-example",
        _ when path.StartsWith("docs/", StringComparison.Ordinal) && path.EndsWith(".md", StringComparison.Ordinal)
            => Path.GetFileNameWithoutExtension(path).Replace('_', '-').ToLowerInvariant(),
        _ => null,
    };
}

internal sealed record DocumentationAsset(string Path, string? Topic, string Title, long Bytes);
