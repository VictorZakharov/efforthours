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
    public async Task TodayWorkflowRunsFromAnUnrelatedFolderAndReusesManagedEvidence()
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
            DateTimeOffset asOf = selectedAt.AddSeconds(1);
            File.WriteAllText(Path.Combine(malformed, ".git"), "gitdir: missing\n");
            string fakeRoot = Path.Combine(workspaceRoot, "fake-gh");
            Directory.CreateDirectory(fakeRoot);
            WriteFakeGitHubCli(fakeRoot, head, selectedAt);
            string cacheRoot = Path.Combine(workspaceRoot, "managed-cache");
            await CloneBareAsync(
                repository.RootPath,
                Path.Combine(cacheRoot, "example-owner", "example-repository.git"));
            string runFrom = Path.Combine(workspaceRoot, "ordinary-user-folder");
            Directory.CreateDirectory(runFrom);
            string reportPath = Path.Combine(workspaceRoot, "today.md");

            ProcessResult result = await RunCliWithPathAsync(
                fakeRoot,
                cacheRoot,
                runFrom,
                "change", "today",
                "--owner", "example-owner",
                "--author", "@me",
                "--timezone", "UTC",
                "--include-open-prs",
                "--scope", "engineering",
                "--capacity-hours", "8",
                "--generated-at", asOf.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture),
                "--format", "markdown",
                "--output", reportPath,
                "--no-rate");

            Assert.True(
                result.ExitCode == 0,
                $"today command failed ({result.ExitCode}): {result.StandardError}\n{result.StandardOutput}");
            Assert.Empty(result.StandardOutput);
            string report = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("Status: **complete**", report, StringComparison.Ordinal);
            Assert.Contains("1 identity-selected commits", report, StringComparison.Ordinal);
            Assert.Contains("Profile: `engineering`", report, StringComparison.Ordinal);
            Assert.DoesNotContain("OLS", report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0%", report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(repository.RootPath, report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("selected@example.invalid", report, StringComparison.OrdinalIgnoreCase);

            ProcessResult rerun = await RunCliWithPathAsync(
                fakeRoot,
                cacheRoot,
                runFrom,
                "change", "today",
                "--owner", "example-owner",
                "--author", "@me",
                "--timezone", "UTC",
                "--include-open-prs",
                "--scope", "engineering",
                "--capacity-hours", "8",
                "--generated-at", asOf.ToString("O", CultureInfo.InvariantCulture),
                "--format", "markdown",
                "--output", reportPath,
                "--no-rate");
            Assert.Equal(0, rerun.ExitCode);
            Assert.Contains("hits 1, misses 0", await File.ReadAllTextAsync(reportPath),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ManagedCacheCreatesAnAbsentBareRepositoryAndReusesItsObjects()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "efforthours-cache-e2e",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using GitFixture source = await GitFixture.CreateAsync(Path.Combine(root, "source"));
            source.WriteText("App.cs", "namespace Demo; internal sealed class App { }\n");
            string head = await source.CommitAsync("source");
            GitHubRepositoryCache cache = new(
                new ExternalCommandRunner(),
                new GitClient(),
                Path.Combine(root, "cache"),
                _ => source.RootPath);
            DiscoveredHead[] heads = [new("default", head, "refs/heads/main")];

            RepositoryAcquisitionResult first = await cache.EnsureAsync(
                "example-owner/new-repository",
                heads,
                CancellationToken.None);
            RepositoryAcquisitionResult second = await cache.EnsureAsync(
                "example-owner/new-repository",
                heads,
                CancellationToken.None);

            Assert.True(Directory.Exists(first.RepositoryPath));
            Assert.Equal(
                "true",
                (await new ExternalCommandRunner().RunAsync(
                    "git",
                    first.RepositoryPath,
                    ["rev-parse", "--is-bare-repository"],
                    CancellationToken.None))
                    .StandardOutput.Trim());
            Assert.Equal(
                Path.GetFullPath(first.RepositoryPath),
                await new GitClient().ResolveRepositoryRootAsync(first.RepositoryPath));
            Assert.True(first.AcquiredObjectCount > 0);
            Assert.Equal(0, first.LocalHeadCount);
            Assert.Equal(1, second.LocalHeadCount);
            Assert.Equal(0, second.AcquiredObjectCount);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task<ProcessResult> RunCliWithPathAsync(
        string executableDirectory,
        string cacheRoot,
        string workingDirectory,
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
        ProcessStartInfo startInfo = StartInfo("dotnet", workingDirectory);
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["PATH"] = executableDirectory + Path.PathSeparator +
            Environment.GetEnvironmentVariable("PATH");
        startInfo.Environment["EFFORTHOURS_REPOSITORY_CACHE"] = cacheRoot;
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunAsync(startInfo);
    }

    private static void WriteFakeGitHubCli(string directory, string head, DateTimeOffset selectedAt)
    {
        string owner = "{\"type\":\"Organization\"}";
        string repositories =
            "[[{\"id\":42,\"full_name\":\"example-owner/example-repository\",\"default_branch\":\"main\"}]]";
        string viewer = "{\"login\":\"selected-contributor\"}";
        string emails = "[[{\"email\":\"selected@example.invalid\",\"verified\":true}]]";
        string pulls = "[[]]";
        string timestamp = selectedAt.ToString("O", CultureInfo.InvariantCulture);
        string defaultCommits = $$$"""
            [[{"sha":"{{{head}}}","parents":[],"author":{"login":"selected-contributor"},"commit":{"author":{"name":"Selected Contributor","email":"selected@example.invalid","date":"{{{timestamp}}}"},"committer":{"name":"Selected Contributor","email":"selected@example.invalid","date":"{{{timestamp}}}"},"message":"selected today"}}]]
            """;
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(
                Path.Combine(directory, "gh.cmd"),
                "@echo off\r\n" +
                "if \"%2\"==\"users/example-owner\" echo " + owner + "& exit /b 0\r\n" +
                "if \"%2\"==\"user\" echo " + viewer + "& exit /b 0\r\n" +
                "if \"%4\"==\"user/emails?per_page=100\" echo " + emails + "& exit /b 0\r\n" +
                "if \"%4\"==\"orgs/example-owner/repos?per_page=100&type=all\" echo " + repositories + "& exit /b 0\r\n" +
                "echo %* | findstr /c:\"repos/example-owner/example-repository/commits?sha=main\" >nul && echo " + defaultCommits + "& exit /b 0\r\n" +
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
            "  *'repos/example-owner/example-repository/commits?sha=main'*) printf '%s\\n' '" + defaultCommits + "' ;;\n" +
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
