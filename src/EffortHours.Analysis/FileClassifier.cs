using System.Collections.Frozen;

namespace EffortHours.Analysis;

internal static class FileClassifier
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
            [".graphql"] = "graphql",
            [".gql"] = "graphql",
            [".sh"] = "shell",
            [".bash"] = "shell",
            [".ps1"] = "powershell",
            [".py"] = "python",
            [".java"] = "java",
            [".kt"] = "kotlin",
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
            [".hpp"] = "cpp",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool HasKnownBinaryExtension(string path) =>
        BinaryExtensions.Contains(Path.GetExtension(path));

    public static FileClassification Classify(string normalizedPath, FileInspection inspection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPath);
        ArgumentNullException.ThrowIfNull(inspection);

        string fileName = Path.GetFileName(normalizedPath);
        string lowerName = fileName.ToLowerInvariant();
        string lowerPath = normalizedPath.ToLowerInvariant();
        string extension = Path.GetExtension(fileName);
        Languages.TryGetValue(extension, out string? language);

        bool isTest = IsTestPath(lowerPath, lowerName);
        bool isGenerated = IsGeneratedPath(lowerPath, lowerName, inspection.SampleText);
        bool isMinified = IsMinifiedFile(lowerName, extension, inspection);
        bool isVendored = HasAnySegment(lowerPath, "vendor", "third_party", "third-party", "external");
        bool isComponentManifest = IsComponentManifest(lowerName, extension);
        string role = DetermineRole(
            lowerPath,
            lowerName,
            extension,
            language,
            inspection.IsBinary,
            isTest,
            isGenerated,
            isMinified,
            isVendored);

        return new FileClassification(
            role,
            language,
            DetectEcosystems(lowerName, extension, language),
            isTest,
            isGenerated,
            isMinified,
            isVendored,
            isComponentManifest);
    }

    private static string DetermineRole(
        string lowerPath,
        string lowerName,
        string extension,
        string? language,
        bool isBinary,
        bool isTest,
        bool isGenerated,
        bool isMinified,
        bool isVendored)
    {
        if (isVendored)
        {
            return "vendored";
        }

        if (isMinified)
        {
            return "minified";
        }

        if (isGenerated)
        {
            return "generated";
        }

        if (IsCoverage(lowerPath, lowerName, extension))
        {
            return "coverage";
        }

        if (IsDependencyLock(lowerName))
        {
            return "dependency-lock";
        }

        if (isTest)
        {
            return "test";
        }

        if (IsDocumentation(lowerPath, lowerName, extension))
        {
            return "documentation";
        }

        if (IsContinuousIntegration(lowerPath, lowerName))
        {
            return "ci-configuration";
        }

        if (IsContainerConfiguration(lowerName))
        {
            return "container-configuration";
        }

        if (IsInfrastructure(lowerPath, lowerName, extension))
        {
            return "infrastructure";
        }

        if (isBinary)
        {
            return "binary";
        }

        if (lowerName == "package.json")
        {
            return "package-manifest";
        }

        if (IsProjectFile(lowerName, extension))
        {
            return "project";
        }

        if (IsSolutionFile(extension))
        {
            return "solution";
        }

        if (IsBuildConfiguration(lowerName, extension))
        {
            return "build-configuration";
        }

        if (language is not null)
        {
            return "source";
        }

        if (IsConfiguration(extension, lowerName))
        {
            return "configuration";
        }

        return "other";
    }

    private static List<string> DetectEcosystems(
        string lowerName,
        string extension,
        string? language)
    {
        List<string> ecosystems = [];
        if (language is "csharp" or "razor" or "fsharp" or "visual-basic" ||
            IsProjectFile(lowerName, extension) ||
            IsSolutionFile(extension) ||
            lowerName is "directory.build.props" or "directory.build.targets" or "directory.packages.props" or "global.json")
        {
            ecosystems.Add("dotnet");
        }

        if (language is "javascript" or "vue" or "svelte" || lowerName == "package.json")
        {
            ecosystems.Add("javascript");
        }

        if (language == "typescript" || lowerName is "tsconfig.json" or "jsconfig.json")
        {
            ecosystems.Add("typescript");
        }

        return ecosystems;
    }

    private static bool IsTestPath(string lowerPath, string lowerName)
    {
        if (HasAnySegment(lowerPath, "test", "tests", "__tests__", "spec", "specs", "e2e"))
        {
            return true;
        }

        return lowerName.Contains(".test.", StringComparison.Ordinal) ||
               lowerName.Contains(".spec.", StringComparison.Ordinal) ||
               lowerName.EndsWith("tests.cs", StringComparison.Ordinal) ||
               lowerName.EndsWith("test.cs", StringComparison.Ordinal) ||
               lowerName.EndsWith("tests.csproj", StringComparison.Ordinal) ||
               lowerName.EndsWith("test.csproj", StringComparison.Ordinal);
    }

    private static bool IsGeneratedPath(string lowerPath, string lowerName, string sampleText)
    {
        if (HasAnySegment(lowerPath, "generated", "gen") ||
            lowerName.EndsWith(".g.cs", StringComparison.Ordinal) ||
            lowerName.Contains(".generated.", StringComparison.Ordinal) ||
            lowerName.EndsWith(".designer.cs", StringComparison.Ordinal) ||
            lowerName.EndsWith(".assemblyattributes.cs", StringComparison.Ordinal))
        {
            return true;
        }

        return sampleText.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase) ||
               sampleText.Contains("@generated", StringComparison.OrdinalIgnoreCase) ||
               sampleText.Contains("do not edit", StringComparison.OrdinalIgnoreCase) ||
               sampleText.Contains("generated by", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMinifiedFile(
        string lowerName,
        string extension,
        FileInspection inspection)
    {
        if (lowerName.Contains(".min.", StringComparison.Ordinal) ||
            lowerName.EndsWith(".bundle.js", StringComparison.Ordinal))
        {
            return true;
        }

        return (extension is ".js" or ".css") &&
               inspection.Bytes >= 1024 &&
               inspection.Lines > 0 &&
               inspection.Bytes / inspection.Lines > 300;
    }

    private static bool IsDocumentation(string lowerPath, string lowerName, string extension) =>
        HasAnySegment(lowerPath, "docs", "documentation") ||
        extension is ".md" or ".mdx" or ".rst" or ".adoc" or ".asciidoc" ||
        lowerName.StartsWith("readme", StringComparison.Ordinal) ||
        lowerName.StartsWith("changelog", StringComparison.Ordinal) ||
        lowerName.StartsWith("contributing", StringComparison.Ordinal) ||
        lowerName.StartsWith("license", StringComparison.Ordinal) ||
        lowerName.StartsWith("security", StringComparison.Ordinal);

    private static bool IsContinuousIntegration(string lowerPath, string lowerName) =>
        lowerPath.StartsWith(".github/workflows/", StringComparison.Ordinal) ||
        lowerPath.StartsWith(".circleci/", StringComparison.Ordinal) ||
        lowerName is ".gitlab-ci.yml" or ".gitlab-ci.yaml" or "azure-pipelines.yml" or "azure-pipelines.yaml" or "jenkinsfile";

    private static bool IsContainerConfiguration(string lowerName) =>
        lowerName.StartsWith("dockerfile", StringComparison.Ordinal) ||
        lowerName.StartsWith("docker-compose", StringComparison.Ordinal) ||
        lowerName.StartsWith("compose.", StringComparison.Ordinal) ||
        lowerName == ".dockerignore";

    private static bool IsInfrastructure(string lowerPath, string lowerName, string extension) =>
        extension is ".tf" or ".tfvars" or ".bicep" ||
        HasAnySegment(lowerPath, "terraform", "k8s", "kubernetes", "helm") ||
        lowerName == "chart.yaml";

    private static bool IsCoverage(string lowerPath, string lowerName, string extension) =>
        HasAnySegment(lowerPath, "coverage") ||
        lowerName.StartsWith("coverage.", StringComparison.Ordinal) ||
        lowerName.Contains("coverlet", StringComparison.Ordinal) ||
        lowerName is ".coveragerc" or "lcov.info" or "cobertura.xml" or "jacoco.xml" or
            "codecov.yml" or "codecov.yaml" ||
        extension is ".coverage" or ".coveragexml" or ".lcov" or ".runsettings";

    private static bool IsDependencyLock(string lowerName) =>
        lowerName is "package-lock.json" or "npm-shrinkwrap.json" or "yarn.lock" or
            "pnpm-lock.yaml" or "bun.lock" or "bun.lockb" or "packages.lock.json" or
            "paket.lock" or "composer.lock" or "gemfile.lock" or
            "poetry.lock" or "cargo.lock" or "go.sum";

    private static bool IsComponentManifest(string lowerName, string extension) =>
        lowerName == "package.json" || IsProjectFile(lowerName, extension);

    private static bool IsProjectFile(string lowerName, string extension) =>
        extension is ".csproj" or ".fsproj" or ".vbproj" or ".vcxproj" or ".esproj" ||
        lowerName == "project.json";

    private static bool IsSolutionFile(string extension) => extension is ".sln" or ".slnx";

    private static bool IsBuildConfiguration(string lowerName, string extension) =>
        lowerName is "directory.build.props" or "directory.build.targets" or "directory.packages.props" or
            "global.json" or "nuget.config" or "makefile" or "cmakelists.txt" or "taskfile.yml" or
            "taskfile.yaml" or "tsconfig.json" or "jsconfig.json" or ".npmrc" or ".yarnrc" or
            ".yarnrc.yml" or ".editorconfig" or ".gitattributes" or ".gitignore" or ".efforthoursignore" ||
        lowerName.StartsWith("webpack.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("vite.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("rollup.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("next.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("nuxt.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("svelte.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("jest.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("vitest.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("playwright.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("cypress.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("eslint.config.", StringComparison.Ordinal) ||
        lowerName.StartsWith("prettier.config.", StringComparison.Ordinal) ||
        lowerName is "angular.json" or "nx.json" or "turbo.json" or "lerna.json" or
            "pnpm-workspace.yaml" or "pnpm-workspace.yml" ||
        extension is ".props" or ".targets";

    private static bool IsConfiguration(string extension, string lowerName) =>
        extension is ".json" or ".jsonc" or ".yaml" or ".yml" or ".toml" or ".ini" or ".config" or ".xml" or ".env" ||
        lowerName.StartsWith(".env.", StringComparison.Ordinal);

    private static bool HasAnySegment(string lowerPath, params string[] candidates)
    {
        string[] segments = lowerPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            if (candidates.Contains(segment, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed record FileClassification(
    string Role,
    string? Language,
    IReadOnlyList<string> Ecosystems,
    bool IsTest,
    bool IsGenerated,
    bool IsMinified,
    bool IsVendored,
    bool IsComponentManifest);
