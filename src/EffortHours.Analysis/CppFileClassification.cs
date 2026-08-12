namespace EffortHours.Analysis;

internal static class CppFileClassification
{
    public static bool IsProjectArtifact(string lowerName, string extension) =>
        lowerName is "cmakelists.txt" or "makefile" or "gnumakefile" or
            "meson.build" or "meson.options" or "meson_options.txt" or
            "cmakepresets.json" or "cmakeuserpresets.json" or
            "compile_commands.json" or "vcpkg.json" or "conanfile.txt" ||
        extension is ".cmake" or ".mk" or ".vcxproj" ||
        IsVcxConfiguration(extension);

    public static bool IsStrongProjectArtifact(string lowerName, string extension) =>
        lowerName is "cmakelists.txt" or "meson.build" or "cmakepresets.json" or
            "cmakeuserpresets.json" or "compile_commands.json" or "vcpkg.json" or
            "conanfile.txt" ||
        extension == ".vcxproj";

    public static bool IsBuildConfiguration(string lowerName, string extension) =>
        IsProjectArtifact(lowerName, extension) &&
        !extension.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase);

    public static bool IsTestSource(string lowerPath, string lowerName) =>
        HasSegment(lowerPath, "test") || HasSegment(lowerPath, "tests") ||
        HasSegment(lowerPath, "spec") || HasSegment(lowerPath, "specs") ||
        HasSegment(lowerPath, "e2e") || HasSegment(lowerPath, "end-to-end") ||
        lowerName.Contains("_test.", StringComparison.Ordinal) ||
        lowerName.Contains("_tests.", StringComparison.Ordinal) ||
        lowerName.Contains(".test.", StringComparison.Ordinal) ||
        lowerName.Contains(".spec.", StringComparison.Ordinal);

    public static bool IsHeaderExtension(string extension) => extension is
        ".h" or ".hh" or ".hpp" or ".hxx" or ".inl" or ".ipp" or ".tpp";

    private static bool IsVcxConfiguration(string extension) => extension == ".props";

    private static bool HasSegment(string path, string value) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Contains(value, StringComparer.Ordinal);
}
