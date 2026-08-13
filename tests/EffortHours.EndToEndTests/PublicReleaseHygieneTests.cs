using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EffortHours.EndToEndTests;

public sealed partial class PublicReleaseHygieneTests
{
    [Fact]
    public void PackageMetadataIdentifiesAnExperimentalPublicPreview()
    {
        string root = FindRepositoryRoot();
        XDocument shared = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        XDocument cli = XDocument.Load(
            Path.Combine(root, "src", "EffortHours.Cli", "EffortHours.Cli.csproj"));

        Assert.Equal("MIT", Property(shared, "PackageLicenseExpression"));
        Assert.Equal(
            "https://github.com/VictorZakharov/efforthours",
            Property(shared, "PackageProjectUrl"));
        Assert.Equal(
            "https://github.com/VictorZakharov/efforthours.git",
            Property(shared, "RepositoryUrl"));
        Assert.Equal("git", Property(shared, "RepositoryType"));
        Assert.Equal("EffortHours.Tool", Property(cli, "PackageId"));
        Assert.Equal("eh", Property(cli, "ToolCommandName"));
        Assert.Equal("PACKAGE_README.md", Property(cli, "PackageReadmeFile"));
        Assert.Contains('-', Property(cli, "Version"));
        Assert.Contains(
            "uncalibrated",
            Property(cli, "PackageReleaseNotes"),
            StringComparison.OrdinalIgnoreCase);

        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains(
            $"EffortHours.Tool --version {Property(cli, "Version")}",
            readme,
            StringComparison.Ordinal);
        string packageReadme = File.ReadAllText(Path.Combine(root, "PACKAGE_README.md"));
        Assert.Contains(
            $"EffortHours.Tool --version {Property(cli, "Version")}",
            packageReadme,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PackageReadmePresentsEveryAnalyzerFamilyAsAScannableMatrix()
    {
        string root = FindRepositoryRoot();
        string readme = File.ReadAllText(Path.Combine(root, "PACKAGE_README.md"));

        Assert.Contains("## Supported analyzers", readme, StringComparison.Ordinal);
        Assert.Contains("### Shared static-analysis boundary", readme, StringComparison.Ordinal);
        Assert.Contains("### Ecosystem-specific boundaries", readme, StringComparison.Ordinal);

        string[] familyRows = [.. readme.Split('\n').Where(line => line.StartsWith("| **", StringComparison.Ordinal))];
        Assert.Equal(13, familyRows.Length);

        foreach (string family in new[]
                 {
                     ".NET",
                     "JavaScript/TypeScript + frontend",
                     "SQL",
                     "Python + Jupyter",
                     "Go",
                     "Java",
                     "Kotlin/JVM",
                     "Shell + PowerShell",
                     "Terraform/HCL",
                     "PHP/Composer",
                     "Rust/Cargo",
                     "Docker/Compose",
                     "C/C++",
                 })
        {
            Assert.Contains($"| **{family}** |", readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RepositoryReadmePublishesPerLanguagePerformanceCheckpoints()
    {
        string root = FindRepositoryRoot();
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Contains("## Quick start", readme, StringComparison.Ordinal);
        Assert.Contains("## Main workflows", readme, StringComparison.Ordinal);
        Assert.Contains("## What the model counts", readme, StringComparison.Ordinal);
        Assert.Contains("## Supported analyzers", readme, StringComparison.Ordinal);
        Assert.Contains("### One-million-line performance checkpoints", readme, StringComparison.Ordinal);
        Assert.Contains("## How Change EHE works", readme, StringComparison.Ordinal);
        Assert.Contains("## Model status", readme, StringComparison.Ordinal);
        Assert.Contains("docs/BENCHMARKS.md", readme, StringComparison.Ordinal);
        Assert.Contains("| .NET / C# | 1,000,001 |", readme, StringComparison.Ordinal);
        Assert.Contains("| JavaScript / TypeScript | 1,000,001 |", readme, StringComparison.Ordinal);
        Assert.Contains("| HTML/CSS/SCSS frontend assets | 1,000,001 | 8.187 s |", readme, StringComparison.Ordinal);
        Assert.Contains("| SQL | 1,000,000 | 8.421 s |", readme, StringComparison.Ordinal);
        Assert.Contains("| Go | 1,000,003 |", readme, StringComparison.Ordinal);
        Assert.Contains("| C++ | 990,002 |", readme, StringComparison.Ordinal);
        Assert.Contains("<details>", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("The current static analyzers support:", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("For `.sql`, formatting comparison", readme, StringComparison.Ordinal);
        Assert.True(
            readme.Split('\n').Length < 400,
            "The customer-facing README should stay scannable; move deep reference material into docs/.");
    }

    [Fact]
    public void WorkflowsPinActionsAndKeepNuGetPublishingManual()
    {
        string root = FindRepositoryRoot();
        string workflowDirectory = Path.Combine(root, ".github", "workflows");
        string notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md"));
        string[] workflows = Directory.GetFiles(workflowDirectory, "*.yml");
        Assert.NotEmpty(workflows);

        int actionCount = 0;
        foreach (string workflowPath in workflows)
        {
            string workflow = File.ReadAllText(workflowPath);
            foreach (Match match in ActionReference().Matches(workflow))
            {
                actionCount++;
                string action = match.Groups["action"].Value;
                string reference = match.Groups["reference"].Value;
                Assert.Matches("^[0-9a-f]{40}$", reference);
                Assert.Contains(action, notices, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.True(actionCount >= 5, "Expected pinned actions in public workflows.");

        string ci = File.ReadAllText(Path.Combine(workflowDirectory, "ci.yml"));
        Assert.Contains("ubuntu-latest", ci, StringComparison.Ordinal);
        Assert.Contains("windows-latest", ci, StringComparison.Ordinal);
        Assert.Contains("macos-latest", ci, StringComparison.Ordinal);

        string preview = File.ReadAllText(
            Path.Combine(workflowDirectory, "nuget-preview.yml"));
        Assert.Contains("workflow_dispatch:", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  push:\n", preview, StringComparison.Ordinal);
        Assert.Contains("environment: nuget.org", preview, StringComparison.Ordinal);
        Assert.Contains("id-token: write", preview, StringComparison.Ordinal);
        Assert.Contains("refs/tags/v", preview, StringComparison.Ordinal);
        Assert.Contains("NuGet/login@", preview, StringComparison.Ordinal);
        Assert.Contains("--locked-mode", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("--force-evaluate", preview, StringComparison.Ordinal);
        Assert.Contains("models/seed-rules/0.4.0.json", preview, StringComparison.Ordinal);
        Assert.Contains("efforthours.deps.json", preview, StringComparison.Ordinal);
        Assert.Contains("efforthours.runtimeconfig.json", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("--skip-duplicate", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDependencyNoticesMatchAuditedLicensePins()
    {
        string root = FindRepositoryRoot();
        XDocument packages = XDocument.Load(Path.Combine(root, "Directory.Packages.props"));
        string notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md"));

        Assert.Equal("8.0.5", PackageVersion(packages, "JsonSchema.Net"));
        Assert.Contains("| JsonSchema.Net | 8.0.5 | MIT |", notices, StringComparison.Ordinal);
        Assert.Equal("1.7.0", PackageVersion(packages, "Acornima"));
        Assert.Contains("| Acornima | 1.7.0 | BSD-3-Clause |", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicProjectDocumentsAndPrivateDataIgnoresArePresent()
    {
        string root = FindRepositoryRoot();
        foreach (string name in new[]
                 {
                     "LICENSE",
                     "PACKAGE_README.md",
                     "CODE_OF_CONDUCT.md",
                     "CONTRIBUTING.md",
                     "GOVERNANCE.md",
                     "SECURITY.md",
                     "THIRD-PARTY-NOTICES.md",
                     "CHANGELOG.md",
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, name)), $"Missing public document '{name}'.");
        }

        Assert.True(File.Exists(Path.Combine(root, "docs", "README.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "RELEASING.md")));

        string ignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        Assert.Contains("artifacts/", ignore, StringComparison.Ordinal);
        Assert.Contains(".efforthours-private/", ignore, StringComparison.Ordinal);
        Assert.Contains("calibration/private/", ignore, StringComparison.Ordinal);

        string releasing = File.ReadAllText(Path.Combine(root, "docs", "RELEASING.md"));
        Assert.Contains("explicit", releasing, StringComparison.Ordinal);
        Assert.Contains("maintainer decision", releasing, StringComparison.Ordinal);
        Assert.Contains("Trusted Publishing", releasing, StringComparison.Ordinal);
        Assert.Contains("cannot be deleted", releasing, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeMarkdownLinksResolveWithinTheRepository()
    {
        string root = FindRepositoryRoot();
        string[] excludedPrefixes =
        [
            ".packages/",
            "artifacts/",
            "bin/",
            "obj/",
        ];

        foreach (string path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
        {
            string repositoryPath = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (excludedPrefixes.Any(prefix =>
                repositoryPath.StartsWith(prefix, StringComparison.Ordinal) ||
                repositoryPath.Contains($"/{prefix}", StringComparison.Ordinal)))
            {
                continue;
            }

            string markdown = File.ReadAllText(path);
            foreach (Match match in MarkdownLink().Matches(markdown))
            {
                string target = match.Groups["target"].Value.Trim('<', '>');
                int fragment = target.IndexOf('#', StringComparison.Ordinal);
                if (fragment >= 0)
                {
                    target = target[..fragment];
                }

                if (string.IsNullOrWhiteSpace(target) ||
                    Uri.TryCreate(target, UriKind.Absolute, out _))
                {
                    continue;
                }

                string resolved = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(path)!,
                    Uri.UnescapeDataString(target).Replace('/', Path.DirectorySeparatorChar)));
                Assert.True(
                    File.Exists(resolved) || Directory.Exists(resolved),
                    $"Broken relative Markdown link '{target}' in '{repositoryPath}'.");
            }
        }
    }

    private static string Property(XDocument document, string name) =>
        document.Descendants(name).Single().Value.Trim();

    private static string PackageVersion(XDocument document, string name) =>
        document.Descendants("PackageVersion")
            .Single(element => string.Equals(
                (string?)element.Attribute("Include"),
                name,
                StringComparison.Ordinal))
            .Attribute("Version")?.Value
        ?? throw new InvalidOperationException($"Package '{name}' has no central version.");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the EffortHours repository root.");
    }

    [GeneratedRegex(
        @"^\s*uses:\s*(?<action>[^@\s]+)@(?<reference>[^\s#]+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ActionReference();

    [GeneratedRegex(
        @"\[[^\]]+\]\((?<target>[^)]+)\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();
}
