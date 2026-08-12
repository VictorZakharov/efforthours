namespace EffortHours.Analysis;

internal static class FileClassifier
{
    public static bool HasKnownBinaryExtension(string path) =>
        FileClassificationCatalog.IsBinaryExtension(Path.GetExtension(path));

    public static FileClassification Classify(string normalizedPath, FileInspection inspection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPath);
        ArgumentNullException.ThrowIfNull(inspection);

        string fileName = Path.GetFileName(normalizedPath);
        string lowerName = fileName.ToLowerInvariant();
        string lowerPath = normalizedPath.ToLowerInvariant();
        string extension = Path.GetExtension(fileName);
        string? language = TerraformFileClassification.IsDependencyLock(lowerName) ||
            TerraformFileClassification.IsStateOrPlan(lowerName, extension)
                ? null
                : TerraformFileClassification.DetectLanguage(lowerName, extension) ??
                    FileClassificationCatalog.LanguageFor(extension);
        language ??= ScriptingFileClassification.DetectLanguage(
            lowerName,
            extension,
            inspection.SampleText);

        bool isTest = !DockerFileClassification.IsProjectArtifact(lowerName) &&
            IsTestPath(lowerPath, fileName, lowerName, language);
        bool isGenerated = GeneratedFileClassification.IsGeneratedPath(
            lowerPath,
            lowerName,
            language,
            inspection.SampleText);
        bool isMinified = IsMinifiedFile(lowerName, extension, inspection);
        bool isVendored = HasAnySegment(
            lowerPath,
            "vendor",
            "third_party",
            "third-party",
            "external",
            "site-packages",
            ".venv",
            "venv");
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
            EcosystemClassification.Detect(lowerName, extension, language, lowerPath),
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

        if (ScriptingFileClassification.IsCiAutomation(lowerPath, lowerName, language))
        {
            return "ci-configuration";
        }

        if (DockerFileClassification.IsProjectArtifact(lowerName))
        {
            return "container-configuration";
        }

        if (IsInfrastructure(lowerPath, lowerName, extension, language))
        {
            return "infrastructure";
        }

        if (ScriptingFileClassification.IsInfrastructureAutomation(lowerPath, lowerName, language))
        {
            return "infrastructure";
        }

        if (ScriptingFileClassification.IsDeliveryAutomation(lowerPath, lowerName, language))
        {
            return "delivery";
        }

        if (ScriptingFileClassification.IsBuildAutomation(lowerPath, lowerName, language))
        {
            return "build-configuration";
        }

        if (isBinary)
        {
            return "binary";
        }

        if (lowerName == "package.json")
        {
            return "package-manifest";
        }

        if (PythonFileClassification.IsProjectManifest(lowerName))
        {
            return "package-manifest";
        }

        if (GoFileClassification.IsProjectManifest(lowerName))
        {
            return "package-manifest";
        }

        if (JavaFileClassification.IsProjectManifest(lowerName))
        {
            return "package-manifest";
        }

        if (PhpFileClassification.IsProjectManifest(lowerName)) return "package-manifest";

        if (RustFileClassification.IsProjectManifest(lowerName)) return "package-manifest";

        if (EcosystemClassification.IsProjectFile(lowerName, extension))
        {
            return "project";
        }

        if (EcosystemClassification.IsSolutionFile(extension))
        {
            return "solution";
        }

        if (IsBuildConfiguration(lowerPath, lowerName, extension))
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

    private static bool IsTestPath(
        string lowerPath,
        string fileName,
        string lowerName,
        string? language)
    {
        if (ScriptingFileClassification.IsTest(lowerPath, lowerName, language))
        {
            return true;
        }

        if (TerraformFileClassification.IsTest(lowerName, language))
        {
            return true;
        }

        if (HasAnySegment(lowerPath, "test", "tests", "__tests__", "spec", "specs", "e2e"))
        {
            return true;
        }

        if (language == "sql" &&
            HasAnySegment(lowerPath, "fixture", "fixtures", "test-data", "testdata"))
        {
            return true;
        }

        if (language == "python" &&
            (lowerName.StartsWith("test_", StringComparison.Ordinal) ||
             lowerName.EndsWith("_test.py", StringComparison.Ordinal) ||
             lowerName == "conftest.py"))
        {
            return true;
        }

        if (language == "go" &&
            (GoFileClassification.IsTestSource(lowerName) || HasAnySegment(lowerPath, "testdata")))
        {
            return true;
        }

        if (language == "java" && JavaFileClassification.IsTestSource(fileName))
        {
            return true;
        }

        if (language == "kotlin" && KotlinFileClassification.IsTestSource(fileName))
        {
            return true;
        }

        if (language == "php" && PhpFileClassification.IsTestSource(fileName)) return true;

        if (language == "rust" && RustFileClassification.IsTestSource(lowerPath)) return true;

        return lowerName.Contains(".test.", StringComparison.Ordinal) ||
               lowerName.Contains(".spec.", StringComparison.Ordinal) ||
               lowerName.EndsWith("tests.cs", StringComparison.Ordinal) ||
               lowerName.EndsWith("test.cs", StringComparison.Ordinal) ||
               lowerName.EndsWith("tests.csproj", StringComparison.Ordinal) ||
               lowerName.EndsWith("test.csproj", StringComparison.Ordinal);
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

    private static bool IsInfrastructure(
        string lowerPath,
        string lowerName,
        string extension,
        string? language) =>
        language is "terraform" or "terraform-json" or "hcl" ||
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
            "poetry.lock" or "pdm.lock" or "uv.lock" or "pipfile.lock" or
            "cargo.lock" or "go.sum" or "gradle.lockfile" or ".terraform.lock.hcl";

    private static bool IsComponentManifest(string lowerName, string extension) =>
        lowerName == "package.json" || PythonFileClassification.IsProjectManifest(lowerName) ||
        GoFileClassification.IsProjectManifest(lowerName) ||
        JavaFileClassification.IsProjectManifest(lowerName) ||
        PhpFileClassification.IsProjectManifest(lowerName) ||
        RustFileClassification.IsProjectManifest(lowerName) ||
        EcosystemClassification.IsProjectFile(lowerName, extension);

    private static bool IsBuildConfiguration(string lowerPath, string lowerName, string extension) =>
        lowerName is "directory.build.props" or "directory.build.targets" or "directory.packages.props" or
            "global.json" or "nuget.config" or "makefile" or "cmakelists.txt" or "taskfile.yml" or
            "taskfile.yaml" or "tsconfig.json" or "jsconfig.json" or ".npmrc" or ".yarnrc" or
            "pyproject.toml" or "setup.cfg" or "setup.py" or "pipfile" or
            "go.work" or
            "settings.gradle" or "settings.gradle.kts" or "gradle.properties" or
            "gradlew" or "gradlew.bat" or "mvnw" or "mvnw.cmd" or
            "tox.ini" or "pytest.ini" or ".coveragerc" or
            "phpunit.xml" or "phpunit.xml.dist" or "phpstan.neon" or "phpstan.neon.dist" or
            "psalm.xml" or "psalm.xml.dist" or
            "build.rs" or "rust-toolchain" or "rust-toolchain.toml" or
            "rustfmt.toml" or ".rustfmt.toml" or "clippy.toml" or ".clippy.toml" or
            ".yarnrc.yml" or ".editorconfig" or ".gitattributes" or ".gitignore" or ".efforthoursignore" ||
        RustFileClassification.IsBuildConfiguration(lowerName, lowerPath) ||
        KotlinFileClassification.IsGradleScript(lowerName) ||
        PythonFileClassification.IsRequirementsFile(lowerName) ||
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
