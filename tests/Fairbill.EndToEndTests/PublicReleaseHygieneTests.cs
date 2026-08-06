using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fairbill.EndToEndTests;

public sealed partial class PublicReleaseHygieneTests
{
    [Fact]
    public void PackageMetadataIdentifiesAnExperimentalPublicPreview()
    {
        string root = FindRepositoryRoot();
        XDocument shared = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        XDocument cli = XDocument.Load(
            Path.Combine(root, "src", "Fairbill.Cli", "Fairbill.Cli.csproj"));

        Assert.Equal("MIT", Property(shared, "PackageLicenseExpression"));
        Assert.Equal(
            "https://github.com/VictorZakharov/fairbill",
            Property(shared, "PackageProjectUrl"));
        Assert.Equal(
            "https://github.com/VictorZakharov/fairbill.git",
            Property(shared, "RepositoryUrl"));
        Assert.Equal("git", Property(shared, "RepositoryType"));
        Assert.Equal("Fairbill.Tool", Property(cli, "PackageId"));
        Assert.Equal("fairbill", Property(cli, "ToolCommandName"));
        Assert.Equal("PACKAGE_README.md", Property(cli, "PackageReadmeFile"));
        Assert.Contains('-', Property(cli, "Version"));
        Assert.Contains(
            "uncalibrated",
            Property(cli, "PackageReleaseNotes"),
            StringComparison.OrdinalIgnoreCase);

        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains(
            $"Fairbill.Tool --version {Property(cli, "Version")}",
            readme,
            StringComparison.Ordinal);
        string packageReadme = File.ReadAllText(Path.Combine(root, "PACKAGE_README.md"));
        Assert.Contains(
            $"Fairbill.Tool --version {Property(cli, "Version")}",
            packageReadme,
            StringComparison.Ordinal);
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
        Assert.DoesNotContain("secrets.", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("--skip-duplicate", preview, StringComparison.Ordinal);
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
                     "RELEASING.md",
                     "SECURITY.md",
                     "THIRD-PARTY-NOTICES.md",
                     "CHANGELOG.md",
                 })
        {
            Assert.True(File.Exists(Path.Combine(root, name)), $"Missing public document '{name}'.");
        }

        string ignore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        Assert.Contains("artifacts/", ignore, StringComparison.Ordinal);
        Assert.Contains(".fairbill-private/", ignore, StringComparison.Ordinal);
        Assert.Contains("calibration/private/", ignore, StringComparison.Ordinal);

        string releasing = File.ReadAllText(Path.Combine(root, "RELEASING.md"));
        Assert.Contains("explicit", releasing, StringComparison.Ordinal);
        Assert.Contains("maintainer decision", releasing, StringComparison.Ordinal);
        Assert.Contains("Trusted Publishing", releasing, StringComparison.Ordinal);
        Assert.Contains("cannot be deleted", releasing, StringComparison.Ordinal);
    }

    private static string Property(XDocument document, string name) =>
        document.Descendants(name).Single().Value.Trim();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fairbill.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Fairbill repository root.");
    }

    [GeneratedRegex(
        @"^\s*uses:\s*(?<action>[^@\s]+)@(?<reference>[^\s#]+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ActionReference();
}
