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
    int FullPathCount,
    ChangePathAdmission? PathAdmission = null)
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
        IChangeSnapshot headSnapshot,
        ChangePathAdmission? pathAdmission = null)
    {
        if (baseSnapshot is not GitSnapshotFileSystem baseGitSnapshot ||
            headSnapshot is not GitSnapshotFileSystem headGitSnapshot)
        {
            return null;
        }

        if (pathAdmission is not null)
        {
            ChangeSnapshotFile[] admittedBase =
                [.. baseGitSnapshot.Files.Where(file => pathAdmission.Admits(file.Path))];
            ChangeSnapshotFile[] admittedHead =
                [.. headGitSnapshot.Files.Where(file => pathAdmission.Admits(file.Path))];
            return CreateFromIndexes(
                FindChangedPaths(
                    admittedBase.ToDictionary(file => file.Path, StringComparer.Ordinal),
                    admittedHead.ToDictionary(file => file.Path, StringComparer.Ordinal)),
                CreateInventoryIndex(admittedBase),
                CreateInventoryIndex(admittedHead),
                admittedBase.Length,
                admittedHead.Length,
                pathAdmission);
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
            baseGitSnapshot.FileCount,
            headGitSnapshot.FileCount,
            pathAdmission: null);
    }

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
            headSnapshotFiles.Count,
            pathAdmission: null);
    }

    internal static ChangeAnalysisInventoryIndex CreateInventoryIndex(
        IEnumerable<ChangeSnapshotFile> files) => ChangeAnalysisInventoryIndex.Create(files);

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
        int headFileCount,
        ChangePathAdmission? pathAdmission)
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

        string id = ComputeScopeId(paths, pathAdmission?.ProfileDigest);
        return new ChangeAnalysisScope(
            id,
            paths,
            changedPaths.Count,
            contextPathCount,
            representativePathCount,
            contextPaths.Length,
            Math.Max(baseFileCount, headFileCount),
            pathAdmission);
    }

    public static string ComputeInventoryDigest(IEnumerable<ChangeSnapshotFile> files)
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

    internal static bool IsContextPath(string path)
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

    internal static bool IsRepresentativePath(string path) =>
        RepresentativeExtensions.Contains(Path.GetExtension(path));

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

    internal static string ComputeScopedInventoryDigest(
        IEnumerable<ChangeSnapshotFile> files,
        string profileDigest)
    {
        string inventory = ComputeInventoryDigest(files);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "efforthours:scoped-git-snapshot:1\n" + profileDigest + "\n" + inventory)))
            .ToLowerInvariant();
    }

    private static string ComputeScopeId(IEnumerable<string> paths, string? profileDigest)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (profileDigest is not null)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(profileDigest));
            hash.AppendData([(byte)'\n']);
        }
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([(byte)'\n']);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
