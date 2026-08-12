using System.Collections.Frozen;

namespace EffortHours.Analysis;

internal static class FileClassificationCatalog
{
    private static readonly FrozenSet<string> BinaryExtensions = new[]
    {
        ".7z", ".a", ".avi", ".bin", ".bmp", ".class", ".dat", ".db", ".dll", ".dylib",
        ".eot", ".exe", ".gif", ".gz", ".ico", ".jar", ".jpeg", ".jpg", ".lockb", ".mov",
        ".mp3", ".mp4", ".o", ".otf", ".pdf", ".pdb", ".png", ".pyc", ".so", ".sqlite",
        ".tar", ".tiff", ".ttf", ".wav", ".webm", ".webp", ".woff", ".woff2", ".zip",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> Languages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "csharp",
            [".cshtml"] = "razor",
            [".razor"] = "razor",
            [".fs"] = "fsharp",
            [".fsx"] = "fsharp",
            [".vb"] = "visual-basic",
            [".js"] = "javascript",
            [".jsx"] = "javascript",
            [".mjs"] = "javascript",
            [".cjs"] = "javascript",
            [".ts"] = "typescript",
            [".tsx"] = "typescript",
            [".mts"] = "typescript",
            [".cts"] = "typescript",
            [".css"] = "css",
            [".scss"] = "scss",
            [".sass"] = "sass",
            [".less"] = "less",
            [".html"] = "html",
            [".htm"] = "html",
            [".vue"] = "vue",
            [".svelte"] = "svelte",
            [".sql"] = "sql",
            [".tf"] = "terraform",
            [".tfvars"] = "terraform",
            [".hcl"] = "hcl",
            [".graphql"] = "graphql",
            [".gql"] = "graphql",
            [".sh"] = "shell",
            [".bash"] = "shell",
            [".ksh"] = "shell",
            [".bats"] = "shell",
            [".ps1"] = "powershell",
            [".psm1"] = "powershell",
            [".psd1"] = "powershell",
            [".py"] = "python",
            [".pyi"] = "python",
            [".ipynb"] = "jupyter",
            [".java"] = "java",
            [".kt"] = "kotlin",
            [".kts"] = "kotlin",
            [".go"] = "go",
            [".rs"] = "rust",
            [".rb"] = "ruby",
            [".php"] = "php",
            [".swift"] = "swift",
            [".c"] = "c",
            [".h"] = "c",
            [".cc"] = "cpp",
            [".cpp"] = "cpp",
            [".cxx"] = "cpp",
            [".cppm"] = "cpp",
            [".ixx"] = "cpp",
            [".hh"] = "cpp",
            [".hpp"] = "cpp",
            [".hxx"] = "cpp",
            [".inl"] = "cpp",
            [".ipp"] = "cpp",
            [".tpp"] = "cpp",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool IsBinaryExtension(string extension) => BinaryExtensions.Contains(extension);

    public static string? LanguageFor(string extension) =>
        Languages.TryGetValue(extension, out string? language) ? language : null;
}
