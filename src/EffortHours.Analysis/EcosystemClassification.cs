namespace EffortHours.Analysis;

internal static class EcosystemClassification
{
    public static List<string> Detect(
        string lowerName,
        string extension,
        string? language)
    {
        List<string> ecosystems = [];
        if (language is "csharp" or "razor" or "fsharp" or "visual-basic" ||
            IsProjectFile(lowerName, extension) || IsSolutionFile(extension) ||
            lowerName is "directory.build.props" or "directory.build.targets" or
                "directory.packages.props" or "global.json")
            ecosystems.Add("dotnet");

        if (language is "javascript" or "vue" or "svelte" or
            "css" or "scss" or "sass" or "less" or "html" ||
            lowerName == "package.json")
            ecosystems.Add("javascript");

        if (language == "typescript" || lowerName is "tsconfig.json" or "jsconfig.json")
            ecosystems.Add("typescript");
        if (language == "sql") ecosystems.Add("sql");
        if (language == "python" || PythonFileClassification.IsProjectArtifact(lowerName))
            ecosystems.Add("python");
        if (language == "go" || GoFileClassification.IsProjectArtifact(lowerName))
            ecosystems.Add("go");
        if (language == "java" || JavaFileClassification.IsProjectArtifact(lowerName))
            ecosystems.Add("java");
        return ecosystems;
    }

    public static bool IsProjectFile(string lowerName, string extension) =>
        extension is ".csproj" or ".fsproj" or ".vbproj" or ".vcxproj" or ".esproj" ||
        lowerName == "project.json";

    public static bool IsSolutionFile(string extension) => extension is ".sln" or ".slnx";
}
