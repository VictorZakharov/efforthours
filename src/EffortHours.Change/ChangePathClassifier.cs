using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePathClassifier
{
    private static readonly HashSet<string> BuildOutputDirectories = new(
        ["obj", "bin", "dist", "out", "target", "artifacts", ".next", ".nuxt", ".svelte-kit"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> VendoredDirectories = new(
        ["vendor", "third_party", "third-party", "external", "node_modules", "bower_components"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> GeneratedDirectories = new(
        ["generated", "gen"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> BinaryExtensions = new(
        [
            ".7z", ".a", ".avi", ".bin", ".bmp", ".class", ".dat", ".db", ".dll", ".dylib",
            ".eot", ".exe", ".gif", ".gz", ".ico", ".jar", ".jpeg", ".jpg", ".lockb", ".mov",
            ".mp3", ".mp4", ".o", ".otf", ".pdf", ".pdb", ".png", ".pyc", ".so", ".sqlite",
            ".tar", ".tiff", ".ttf", ".wav", ".webm", ".webp", ".woff", ".woff2", ".zip",
        ],
        StringComparer.OrdinalIgnoreCase);

    public static ChangePathClassification Classify(
        ChangePathStatus status,
        string path,
        string? previousPath,
        ChangeSnapshotFile? baseFile,
        ChangeSnapshotFile? headFile,
        IReadOnlyCollection<string> tags,
        IReadOnlySet<string> baseObjectIds,
        IReadOnlySet<string> headObjectIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(baseObjectIds);
        ArgumentNullException.ThrowIfNull(headObjectIds);
        if (status == ChangePathStatus.Moved)
        {
            return ChangePathClassification.ExactMove;
        }

        ChangeSnapshotFile? file = headFile ?? baseFile;
        if (file is null ||
            baseFile is { IsLink: true } or { IsSubmodule: true } ||
            headFile is { IsLink: true } or { IsSubmodule: true })
        {
            return ChangePathClassification.Unsupported;
        }

        if (HasTag(tags, "classification:vendored") ||
            IsVendored(path) ||
            (previousPath is not null && IsVendored(previousPath)))
        {
            return ChangePathClassification.Vendored;
        }

        if (HasTag(tags, "classification:minified") ||
            IsMinified(path) ||
            (previousPath is not null && IsMinified(previousPath)))
        {
            return ChangePathClassification.Minified;
        }

        if (HasTag(tags, "content:binary") || IsBinary(path))
        {
            return ChangePathClassification.Binary;
        }

        if (HasTag(tags, "role:dependency-lock") ||
            IsDependencyLock(path) ||
            (previousPath is not null && IsDependencyLock(previousPath)))
        {
            return ChangePathClassification.Lockfile;
        }

        if (IsBuildOutput(path) ||
            (previousPath is not null && IsBuildOutput(previousPath)))
        {
            return ChangePathClassification.BuildOutput;
        }

        if ((status == ChangePathStatus.Added && baseObjectIds.Contains(file.ObjectId)) ||
            (status == ChangePathStatus.Removed && headObjectIds.Contains(file.ObjectId)))
        {
            return ChangePathClassification.ExactDuplicate;
        }

        if (HasTag(tags, "classification:generated") ||
            HasTag(tags, "classification:sql-dump") ||
            IsGenerated(path) ||
            (previousPath is not null && IsGenerated(previousPath)))
        {
            return ChangePathClassification.Generated;
        }

        return ChangePathClassification.Represented;
    }

    private static bool HasTag(IEnumerable<string> tags, string value) =>
        tags.Contains(value, StringComparer.Ordinal);

    private static bool IsBuildOutput(string path) => HasAnySegment(path, BuildOutputDirectories);

    private static bool IsVendored(string path) => HasAnySegment(path, VendoredDirectories);

    private static bool IsBinary(string path) => BinaryExtensions.Contains(Path.GetExtension(path));

    private static bool IsMinified(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        return name.Contains(".min.", StringComparison.Ordinal) ||
            name.EndsWith(".bundle.js", StringComparison.Ordinal);
    }

    private static bool IsGenerated(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        return HasAnySegment(path, GeneratedDirectories) ||
            name.EndsWith(".g.cs", StringComparison.Ordinal) ||
            name.Contains(".generated.", StringComparison.Ordinal) ||
            name.EndsWith(".designer.cs", StringComparison.Ordinal) ||
            name.EndsWith(".assemblyattributes.cs", StringComparison.Ordinal);
    }

    private static bool IsDependencyLock(string path) =>
        Path.GetFileName(path).ToLowerInvariant() is
            "package-lock.json" or "npm-shrinkwrap.json" or "yarn.lock" or
            "pnpm-lock.yaml" or "bun.lock" or "bun.lockb" or "packages.lock.json" or
            "paket.lock" or "composer.lock" or "gemfile.lock" or
            "poetry.lock" or "cargo.lock" or "go.sum";

    private static bool HasAnySegment(string path, IReadOnlySet<string> candidates) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(candidates.Contains);
}
