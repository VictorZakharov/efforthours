namespace EffortHours.Analysis;

internal static class RustFileClassification
{
    public static bool IsProjectArtifact(string lowerName, string lowerPath) =>
        IsProjectManifest(lowerName) || lowerName is
            "cargo.lock" or "rust-toolchain" or "rust-toolchain.toml" or
            "rustfmt.toml" or ".rustfmt.toml" or "clippy.toml" or ".clippy.toml" ||
        IsCargoConfiguration(lowerName, lowerPath);

    public static bool IsProjectManifest(string lowerName) => lowerName == "cargo.toml";

    public static bool IsBuildConfiguration(string lowerName, string lowerPath) =>
        lowerName is "build.rs" or "rust-toolchain" or "rust-toolchain.toml" or
            "rustfmt.toml" or ".rustfmt.toml" or "clippy.toml" or ".clippy.toml" ||
        IsCargoConfiguration(lowerName, lowerPath);

    public static bool IsTestSource(string lowerPath) =>
        HasSegment(lowerPath, "tests") || HasSegment(lowerPath, "benches");

    private static bool IsCargoConfiguration(string lowerName, string lowerPath) =>
        lowerName is "config" or "config.toml" && HasSegment(lowerPath, ".cargo");

    private static bool HasSegment(string lowerPath, string candidate) =>
        lowerPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Contains(candidate, StringComparer.Ordinal);
}
