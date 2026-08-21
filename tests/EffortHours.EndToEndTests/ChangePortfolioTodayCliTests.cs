using System.Diagnostics;
using System.Globalization;
using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task WorkspaceCatalogContinuesPastAMalformedGitMarker()
    {
        string workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-workspace-catalog-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            string malformed = Path.Combine(workspaceRoot, "malformed-marker");
            using GitFixture repository = await GitFixture.CreateAsync(
                Path.Combine(malformed, "repository"));
            await repository.GitAsync(
                "remote",
                "add",
                "origin",
                "https://github.com/example-owner/example-repository.git");
            string canonicalRoot = Path.GetFullPath(
                await repository.GitAsync("rev-parse", "--show-toplevel"));
            File.WriteAllText(Path.Combine(malformed, ".git"), "gitdir: missing\n");

            IReadOnlyList<WorkspaceGitHubRepository> repositories =
                await WorkspaceGitHubRepositoryCatalog.DiscoverAsync(
                    workspaceRoot,
                    new ExternalCommandRunner(),
                    CancellationToken.None);

            WorkspaceGitHubRepository discovered = Assert.Single(repositories);
            Assert.Equal(canonicalRoot, discovered.RootPath);
            Assert.Equal("example-owner/example-repository", Assert.Single(discovered.RepositoryIdentities));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task TodayWorkflowDiscoversAndReportsOneLocalDefaultHeadFromOneCommand()
    {
        // ProcessStartInfo cannot execute a .cmd shim as a direct gh executable. The provider
        // adapter is covered with an in-process fake on Windows; this process-level shim runs on
        // the Unix CI hosts where an executable shell file can stand in for gh.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-today-workspace-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            string malformed = Path.Combine(workspaceRoot, "malformed-marker");
            using GitFixture repository = await GitFixture.CreateAsync(
                Path.Combine(malformed, "repository"));
            await repository.GitAsync("config", "user.name", "Selected Contributor");
            await repository.GitAsync("config", "user.email", "selected@example.invalid");
            await repository.GitAsync(
                "remote",
                "add",
                "origin",
                "https://github.com/example-owner/example-repository.git");
            repository.WriteText("Demo.csproj", ProjectFile);
            repository.WriteText(
                "Feature.cs",
                "namespace Demo; public sealed class Feature { public bool Enabled => true; }\n");
            string head = await repository.CommitAsync("selected today");
            DateTimeOffset selectedAt = DateTimeOffset.Parse(
                await repository.GitAsync("show", "-s", "--format=%aI", head),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind).ToUniversalTime();
            File.WriteAllText(Path.Combine(malformed, ".git"), "gitdir: missing\n");
            string fakeRoot = Path.Combine(workspaceRoot, "fake-gh");
            Directory.CreateDirectory(fakeRoot);
            WriteFakeGitHubCli(fakeRoot, head);

            ProcessResult result = await RunCliWithPathAsync(
                fakeRoot,
                "change", "portfolio",
                "--owner", "example-owner",
                "--workspace", workspaceRoot,
                "--author", "@me",
                "--today",
                "--timezone", "UTC",
                "--include-open-prs",
                "--capacity-hours", "8",
                "--generated-at", selectedAt.ToString("O", CultureInfo.InvariantCulture),
                "--format", "markdown",
                "--no-rate");

            Assert.True(
                result.ExitCode == 0,
                $"today command failed ({result.ExitCode}): {result.StandardError}\n{result.StandardOutput}");
            Assert.Contains("Today-to-date expected ratio:", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Selected changes: **1**", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Repositories: **1**", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Open PR heads included: **0**", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(repository.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected@example.invalid", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    private static async Task<ProcessResult> RunCliWithPathAsync(
        string executableDirectory,
        params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "EffortHours.Cli",
            "bin",
            configuration,
            "net10.0",
            "efforthours.dll");
        ProcessStartInfo startInfo = StartInfo("dotnet", repositoryRoot);
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["PATH"] = executableDirectory + Path.PathSeparator +
            Environment.GetEnvironmentVariable("PATH");
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunAsync(startInfo);
    }

    private static void WriteFakeGitHubCli(string directory, string head)
    {
        string owner = "{\"type\":\"Organization\"}";
        string repositories =
            "[[{\"id\":42,\"full_name\":\"example-owner/example-repository\",\"default_branch\":\"main\"}]]";
        string viewer = "{\"login\":\"selected-contributor\"}";
        string emails = "[[{\"email\":\"selected@example.invalid\",\"verified\":true}]]";
        string defaultHead = "{\"sha\":\"" + head + "\"}";
        string pulls = "[[]]";
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(
                Path.Combine(directory, "gh.cmd"),
                "@echo off\r\n" +
                "if \"%2\"==\"users/example-owner\" echo " + owner + "& exit /b 0\r\n" +
                "if \"%2\"==\"user\" echo " + viewer + "& exit /b 0\r\n" +
                "if \"%4\"==\"user/emails?per_page=100\" echo " + emails + "& exit /b 0\r\n" +
                "if \"%4\"==\"orgs/example-owner/repos?per_page=100&type=all\" echo " + repositories + "& exit /b 0\r\n" +
                "if \"%2\"==\"repos/example-owner/example-repository/commits/main\" echo " + defaultHead + "& exit /b 0\r\n" +
                "if \"%4\"==\"repos/example-owner/example-repository/pulls?state=open&per_page=100\" echo " + pulls + "& exit /b 0\r\n" +
                "exit /b 1\r\n");
            return;
        }

        string script =
            "#!/bin/sh\n" +
            "case \"$*\" in\n" +
            "  'api users/example-owner') printf '%s\\n' '" + owner + "' ;;\n" +
            "  'api user') printf '%s\\n' '" + viewer + "' ;;\n" +
            "  'api --paginate --slurp user/emails?per_page=100') printf '%s\\n' '" + emails + "' ;;\n" +
            "  'api --paginate --slurp orgs/example-owner/repos?per_page=100&type=all') printf '%s\\n' '" + repositories + "' ;;\n" +
            "  'api repos/example-owner/example-repository/commits/main') printf '%s\\n' '" + defaultHead + "' ;;\n" +
            "  'api --paginate --slurp repos/example-owner/example-repository/pulls?state=open&per_page=100') printf '%s\\n' '" + pulls + "' ;;\n" +
            "  *) exit 1 ;;\n" +
            "esac\n";
        string path = Path.Combine(directory, "gh");
        File.WriteAllText(path, script);
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
