using System.Text.Json;

namespace EffortHours.Analyzers.Cpp;

internal static class CppSupplementaryMetadataReader
{
    public static void ParseCompileCommands(
        string text,
        CppBuildAccumulator project,
        IReadOnlySet<string> scannerPaths,
        string rootPath)
    {
        project.BuildSystems.Add("compile-commands");
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
        foreach (JsonElement entry in document.RootElement.EnumerateArray().Take(100_000))
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("file", out JsonElement file) ||
                file.ValueKind != JsonValueKind.String) continue;
            string? path = NormalizeCompilePath(file.GetString()!, rootPath);
            if (path is not null && scannerPaths.Contains(path))
            {
                project.ExplicitSources.Add(path);
                CppBuildLanguage.Add(Path.GetExtension(path), project);
            }
            if (!entry.TryGetProperty("arguments", out JsonElement arguments) ||
                arguments.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement argument in arguments.EnumerateArray())
            {
                if (argument.ValueKind != JsonValueKind.String) continue;
                string standard = CppBuildStandard.Normalize(argument.GetString()!);
                if (standard.Length > 0) project.Standards.Add(standard);
            }
        }
    }

    public static void ParseVcpkg(string text, CppBuildAccumulator project)
    {
        project.BuildSystems.Add("vcpkg");
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
        if (!document.RootElement.TryGetProperty("dependencies", out JsonElement dependencies) ||
            dependencies.ValueKind != JsonValueKind.Array) return;
        foreach (JsonElement dependency in dependencies.EnumerateArray().Take(10_000))
        {
            string? name = dependency.ValueKind == JsonValueKind.String
                ? dependency.GetString()
                : dependency.ValueKind == JsonValueKind.Object &&
                    dependency.TryGetProperty("name", out JsonElement item)
                    ? item.GetString()
                    : null;
            if (name is not null && CppBuildSyntax.IsLiteral(name)) project.Dependencies.Add(name);
        }
    }

    public static void ParseConan(string text, CppBuildAccumulator project)
    {
        project.BuildSystems.Add("conan");
        bool requirements = false;
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string value = line.Trim();
            if (value.StartsWith('['))
            {
                requirements = value == "[requires]";
                continue;
            }
            if (!requirements || value.StartsWith('#') || value.Length == 0) continue;
            string name = value.Split('/')[0].Trim();
            if (CppBuildSyntax.IsLiteral(name)) project.Dependencies.Add(name);
        }
    }

    private static string? NormalizeCompilePath(string value, string rootPath)
    {
        string candidate;
        if (Path.IsPathRooted(value))
        {
            string full = Path.GetFullPath(value);
            string relative = Path.GetRelativePath(rootPath, full);
            if (relative == ".." || Path.IsPathRooted(relative) ||
                relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) return null;
            candidate = relative;
        }
        else candidate = value;
        return CppPath.Normalize(candidate);
    }
}
