namespace EffortHours.Analyzers.Go;

internal static class GoPlatformFile
{
    private static readonly HashSet<string> Suffixes = new(StringComparer.Ordinal)
    {
        "aix", "android", "darwin", "dragonfly", "freebsd", "illumos", "ios", "js",
        "linux", "netbsd", "openbsd", "plan9", "solaris", "wasip1", "windows",
        "386", "amd64", "arm", "arm64", "loong64", "mips", "mips64", "mips64le",
        "mipsle", "ppc64", "ppc64le", "riscv64", "s390x", "wasm",
    };

    public static bool IsPlatformSpecific(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        string[] parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Skip(1).Any(Suffixes.Contains);
    }
}
