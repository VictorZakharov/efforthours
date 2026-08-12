namespace EffortHours.Analyzers.Cpp;

internal static class CppBuildLanguage
{
    public static void Add(string value, CppBuildAccumulator project)
    {
        string lower = value.Trim().Trim('"', '\'').ToLowerInvariant();
        if (lower is "c" or ".c") project.DeclaredLanguages.Add("c");
        else if (lower is "cxx" or "cpp" or "c++" or ".cc" or ".cpp" or ".cxx" or
            ".cppm" or ".ixx") project.DeclaredLanguages.Add("cpp");
    }
}
