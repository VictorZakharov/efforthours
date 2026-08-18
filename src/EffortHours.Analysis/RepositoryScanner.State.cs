using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    private sealed class ScanState(
        string rootPath,
        RepositoryScanOptions options,
        RepositoryScanCache? cache,
        IRepositoryFileSystem fileSystem,
        RepositoryAnalysisArtifactCache? analysisArtifactCache)
    {
        private readonly Dictionary<string, RepositoryScanCacheEntry> _cachedFiles =
            cache?.Files.ToDictionary(entry => entry.Path, StringComparer.Ordinal) ??
            new Dictionary<string, RepositoryScanCacheEntry>(StringComparer.Ordinal);

        public string RootPath { get; } = rootPath;

        public RepositoryScanOptions Options { get; } = options;

        public IRepositoryFileSystem FileSystem { get; } = fileSystem;

        public RepositoryAnalysisArtifactCache? AnalysisArtifactCache { get; } =
            analysisArtifactCache;

        public List<ScannedFile> Files { get; } = [];

        public List<ExcludedEntry> Exclusions { get; } = [];

        public List<Diagnostic> Diagnostics { get; } = [];

        public bool TryGetCachedFile(
            string relativePath,
            long length,
            long lastWriteTimeUtcTicks,
            out ScannedFile file)
        {
            if (!_cachedFiles.TryGetValue(relativePath, out RepositoryScanCacheEntry? entry) ||
                entry.Length != length ||
                entry.LastWriteTimeUtcTicks != lastWriteTimeUtcTicks)
            {
                file = null!;
                return false;
            }

            file = new ScannedFile(
                relativePath,
                new FileInspection(
                    entry.Bytes,
                    entry.Lines,
                    entry.Sha256,
                    entry.IsBinary,
                    string.Empty),
                new FileClassification(
                    entry.Role,
                    entry.Language,
                    entry.Ecosystems,
                    entry.IsTest,
                    entry.IsGenerated,
                    entry.IsMinified,
                    entry.IsVendored,
                    entry.IsComponentManifest),
                entry.Length,
                entry.LastWriteTimeUtcTicks);
            return true;
        }

        public void AddUnreadableDiagnostic(string relativePath, Exception exception)
        {
            string displayPath = relativePath.Length == 0 ? "." : relativePath;
            Diagnostics.Add(new Diagnostic
            {
                Code = "FB2001",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Could not inspect '{displayPath}': {DescribeReadFailure(exception)}",
                Locations = [new EvidenceLocation { Path = displayPath }],
            });
        }

        private static string DescribeReadFailure(Exception exception) => exception switch
        {
            UnauthorizedAccessException => "access was denied.",
            IOException => "the filesystem entry could not be read.",
            _ => "the entry could not be inspected.",
        };
    }

    private sealed record DirectoryFrame(
        string FullPath,
        string RelativePath,
        IReadOnlyList<IgnoreRule> InheritedRules);

    private sealed record ScannedFile(
        string RelativePath,
        FileInspection Inspection,
        FileClassification Classification,
        long MetadataLength,
        long LastWriteTimeUtcTicks);

    private sealed record ExcludedEntry(string RelativePath, string Reason, bool IsDirectory);
}
