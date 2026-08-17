using System.Security.Cryptography;
using System.Text;

namespace EffortHours.Change;

internal sealed record ChangeAnalysisScope(
    string Id,
    IReadOnlySet<string> Paths,
    int ChangedPathCount,
    int ContextPathCount,
    int RepresentativePathCount,
    int AvailableContextPathCount,
    int FullPathCount)
{
    private static readonly HashSet<string> ContextExtensions = new(
        [
            ".csproj", ".fsproj", ".vbproj", ".vcxproj", ".props", ".targets",
            ".sln", ".slnx", ".gradle", ".kts",
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> RepresentativeExtensions = new(
        [
            ".c", ".cc", ".cpp", ".cxx", ".h", ".hh", ".hpp", ".hxx", ".ixx",
            ".cs", ".cshtml", ".dockerignore", ".go", ".hcl", ".html", ".ipynb",
            ".java", ".js", ".jsx", ".kt", ".kts", ".php", ".ps1", ".psd1",
            ".psm1", ".py", ".pyi", ".razor", ".rs", ".sh", ".sql", ".tf",
            ".tfvars", ".ts", ".tsx", ".vue",
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ContextFileNames = new(
        [
            ".dockerignore", ".efforthoursignore", ".gitignore", "angular.json",
            "build.gradle", "build.gradle.kts", "cargo.toml", "cmakelists.txt",
            "composer.json", "configure.ac", "directory.build.props",
            "directory.build.targets", "directory.packages.props", "docker-compose.yml",
            "docker-compose.yaml", "go.mod", "go.work", "global.json", "gradle.properties",
            "jsconfig.json", "makefile", "meson.build", "nuget.config", "package.json",
            "packages.config", "pipfile", "pom.xml", "pyproject.toml", "setup.cfg",
            "setup.py", "settings.gradle", "settings.gradle.kts", "tox.ini",
        ],
        StringComparer.OrdinalIgnoreCase);

    public static ChangeAnalysisScope? Create(
        IChangeSnapshot baseSnapshot,
        IChangeSnapshot headSnapshot)
    {
        if (baseSnapshot is not GitSnapshotFileSystem baseGitSnapshot ||
            headSnapshot is not GitSnapshotFileSystem headGitSnapshot ||
            Math.Max(baseSnapshot.Files.Count, headSnapshot.Files.Count) <= FullSnapshotFileLimit)
        {
            return null;
        }

        IReadOnlySet<string> changedPaths = headGitSnapshot.TryGetChangedPathsFrom(
            baseGitSnapshot.ObjectId,
            out IReadOnlyList<string> knownChangedPaths)
            ? new HashSet<string>(knownChangedPaths, StringComparer.Ordinal)
            : FindChangedPaths(baseGitSnapshot.FilesByPath, headGitSnapshot.FilesByPath);
        return CreateFromIndexes(
            changedPaths,
            baseGitSnapshot.AnalysisIndex,
            headGitSnapshot.AnalysisIndex,
            baseGitSnapshot.Files.Count,
            headGitSnapshot.Files.Count);
    }

    private const int FullSnapshotFileLimit = ChangeEstimator.FullSnapshotAnalysisFileLimit;

    internal static ChangeAnalysisScope CreateForFiles(
        IReadOnlyList<ChangeSnapshotFile> baseSnapshotFiles,
        IReadOnlyList<ChangeSnapshotFile> headSnapshotFiles)
    {
        ArgumentNullException.ThrowIfNull(baseSnapshotFiles);
        ArgumentNullException.ThrowIfNull(headSnapshotFiles);

        IReadOnlyDictionary<string, ChangeSnapshotFile> baseFiles = baseSnapshotFiles.ToDictionary(
            file => file.Path,
            StringComparer.Ordinal);
        IReadOnlyDictionary<string, ChangeSnapshotFile> headFiles = headSnapshotFiles.ToDictionary(
            file => file.Path,
            StringComparer.Ordinal);
        IReadOnlySet<string> changedPaths = FindChangedPaths(baseFiles, headFiles);
        return CreateFromIndexes(
            changedPaths,
            CreateInventoryIndex(baseSnapshotFiles),
            CreateInventoryIndex(headSnapshotFiles),
            baseSnapshotFiles.Count,
            headSnapshotFiles.Count);
    }

    internal static ChangeAnalysisInventoryIndex CreateInventoryIndex(
        IReadOnlyList<ChangeSnapshotFile> files)
    {
        string[] contextPaths = [.. files
            .Select(file => file.Path)
            .Where(IsContextPath)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        string[] representativePaths = [.. files
            .Where(file => RepresentativeExtensions.Contains(Path.GetExtension(file.Path)))
            .GroupBy(file => Path.GetExtension(file.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MinBy(file => file.Path, StringComparer.Ordinal)!.Path)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        return new ChangeAnalysisInventoryIndex(contextPaths, representativePaths);
    }

    internal static HashSet<string> FindChangedPaths(
        IReadOnlyDictionary<string, ChangeSnapshotFile> baseFiles,
        IReadOnlyDictionary<string, ChangeSnapshotFile> headFiles)
    {
        HashSet<string> changedPaths = new(StringComparer.Ordinal);
        foreach ((string path, ChangeSnapshotFile after) in headFiles)
        {
            if (!baseFiles.TryGetValue(path, out ChangeSnapshotFile? before) ||
                !string.Equals(before.ObjectId, after.ObjectId, StringComparison.Ordinal) ||
                !string.Equals(before.Mode, after.Mode, StringComparison.Ordinal))
            {
                changedPaths.Add(path);
            }
        }

        foreach (string path in baseFiles.Keys)
        {
            if (!headFiles.ContainsKey(path))
            {
                changedPaths.Add(path);
            }
        }

        return changedPaths;
    }

    private static ChangeAnalysisScope CreateFromIndexes(
        IReadOnlySet<string> changedPaths,
        ChangeAnalysisInventoryIndex baseIndex,
        ChangeAnalysisInventoryIndex headIndex,
        int baseFileCount,
        int headFileCount)
    {
        HashSet<string> paths = new(changedPaths, StringComparer.Ordinal);
        string[] contextPaths = [.. baseIndex.ContextPaths
            .Concat(headIndex.ContextPaths)
            .Distinct(StringComparer.Ordinal)];
        foreach (string path in contextPaths.Where(path => IsRelevantContextPath(path, changedPaths)))
        {
            paths.Add(path);
        }

        int contextPathCount = paths.Count - changedPaths.Count;
        int beforeRepresentatives = paths.Count;
        paths.UnionWith(baseIndex.RepresentativePaths);
        paths.UnionWith(headIndex.RepresentativePaths);
        int representativePathCount = paths.Count - beforeRepresentatives;

        string id = ComputeScopeId(paths);
        return new ChangeAnalysisScope(
            id,
            paths,
            changedPaths.Count,
            contextPathCount,
            representativePathCount,
            contextPaths.Length,
            Math.Max(baseFileCount, headFileCount));
    }

    public static string ComputeInventoryDigest(IReadOnlyList<ChangeSnapshotFile> files)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("efforthours:git-snapshot-inventory:1\0"u8);
        foreach (ChangeSnapshotFile file in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Path));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(file.Mode));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(file.ObjectId));
            hash.AppendData([(byte)'\n']);
        }

        return $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    private static bool IsContextPath(string path)
    {
        string fileName = Path.GetFileName(path);
        string extension = Path.GetExtension(path);
        return ContextFileNames.Contains(fileName) ||
            ContextExtensions.Contains(extension) ||
            fileName.StartsWith("dockerfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) &&
                extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("tsconfig", StringComparison.OrdinalIgnoreCase) &&
                extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains(".config.", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("compose.", StringComparison.OrdinalIgnoreCase) &&
                (extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRelevantContextPath(
        string contextPath,
        IReadOnlySet<string> changedPaths)
    {
        int separator = contextPath.LastIndexOf('/');
        if (separator < 0)
        {
            return true;
        }

        string directoryPrefix = contextPath[..(separator + 1)];
        return changedPaths.Any(path => path.StartsWith(directoryPrefix, StringComparison.Ordinal));
    }

    private static string ComputeScopeId(IEnumerable<string> paths)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([(byte)'\n']);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
