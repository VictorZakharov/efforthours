using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Fairbill.Change;
using Fairbill.Contracts.V1;

namespace Fairbill.EndToEndTests;

public sealed class ChangeCliTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task CommitSelectorUsesImmutableSnapshotsAndDoesNotMutateWorktree()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        string baseObjectId = await repository.CommitAsync("base");
        repository.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        string headObjectId = await repository.CommitAsync("feature");
        repository.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 999; }\n");
        repository.WriteText("untracked.txt", "leave me alone\n");
        string statusBefore = await repository.GitAsync("status", "--porcelain=v1");

        ProcessResult first = await RunCliAsync(
            "change",
            repository.RootPath,
            "--commit",
            headObjectId,
            "--no-rate",
            "--compact");
        ProcessResult second = await RunCliAsync(
            "change",
            repository.RootPath,
            "--commit",
            headObjectId,
            "--no-rate",
            "--compact");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(statusBefore, await repository.GitAsync("status", "--porcelain=v1"));
        Assert.DoesNotContain("999", first.StandardOutput, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(first.StandardOutput);
        JsonElement selection = report.RootElement.GetProperty("selection");
        Assert.Equal(baseObjectId, selection.GetProperty("base").GetProperty("objectId").GetString());
        Assert.Equal(headObjectId, selection.GetProperty("head").GetProperty("objectId").GetString());
        Assert.Equal("commit", selection.GetProperty("kind").GetString());
        Assert.True(report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal() > 0m);
    }

    [Fact]
    public async Task RootCommitUsesEmptyTree()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Program.cs", "namespace Demo; public static class Program { }\n");
        repository.WriteText("empty.txt", string.Empty);
        string rootObjectId = await repository.CommitAsync("root");

        ProcessResult result = await RunCliAsync(
            "change",
            repository.RootPath,
            "--commit",
            rootObjectId,
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
        JsonElement selection = report.RootElement.GetProperty("selection");
        Assert.Equal("empty-tree", selection.GetProperty("base").GetProperty("kind").GetString());
        Assert.Equal(GitClient.EmptyTreeObjectId, selection.GetProperty("base").GetProperty("objectId").GetString());
        Assert.Equal(rootObjectId, selection.GetProperty("head").GetProperty("objectId").GetString());
        Assert.Equal(
            "us-senior-software-contractor/2026.1",
            report.RootElement.GetProperty("rateCard").GetProperty("id").GetString());
        Assert.True(report.RootElement.GetProperty("totalCost").GetProperty("expected").GetDecimal() > 0m);
    }

    [Fact]
    public async Task RangeReportsNormalizedDeltaAndExactComponentAllocation()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        string baseObjectId = await repository.CommitAsync("base");
        repository.WriteText("Alpha.cs", "namespace Demo; public sealed class Alpha { }\n");
        _ = await repository.CommitAsync("alpha");
        repository.WriteText("Beta.cs", "namespace Demo; public sealed class Beta { }\n");
        string headObjectId = await repository.CommitAsync("beta");

        ProcessResult result = await RunCliAsync(
            "change",
            repository.RootPath,
            "--range",
            $"{baseObjectId}..{headObjectId}",
            "--no-rate",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
        JsonElement reconciliation = report.RootElement.GetProperty("reconciliation");
        JsonElement.ArrayEnumerator components = reconciliation.GetProperty("components").EnumerateArray();
        decimal allocated = components.Select(component =>
            component.GetProperty("allocatedExpectedHours").GetDecimal()).Sum();
        Assert.Equal(2, reconciliation.GetProperty("components").GetArrayLength());
        Assert.Equal(
            report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal(),
            allocated);
        Assert.Equal(baseObjectId, report.RootElement.GetProperty("selection").GetProperty("base").GetProperty("objectId").GetString());
        Assert.Equal(headObjectId, report.RootElement.GetProperty("selection").GetProperty("head").GetProperty("objectId").GetString());
    }

    [Fact]
    public async Task PullRequestPlanMatchesSameImmutableBaseHeadDeltaWithoutNetwork()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        string baseObjectId = await repository.CommitAsync("base");
        repository.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        string headObjectId = await repository.CommitAsync("feature");
        FixedPullRequestResolver resolver = new(baseObjectId, headObjectId);
        GitChangePlanner planner = new(new GitClient(), resolver);

        GitChangePlan pullRequestPlan = await planner.PlanPullRequestAsync(
            repository.RootPath,
            "17",
            "acme/demo");
        GitChangePlan baseHeadPlan = await planner.PlanBaseHeadAsync(
            repository.RootPath,
            baseObjectId,
            headObjectId);
        ChangeEstimator estimator = new();
        ChangeEstimateReport pullRequest = await estimator.EstimateAsync(
            pullRequestPlan,
            EstimationProfile.Implementation);
        ChangeEstimateReport baseHead = await estimator.EstimateAsync(
            baseHeadPlan,
            EstimationProfile.Implementation);

        Assert.Equal(ChangeSelectionKind.PullRequest, pullRequest.Selection.Kind);
        Assert.Equal(17, pullRequest.Selection.PullRequest!.Number);
        Assert.Equal("acme/demo", pullRequest.Selection.PullRequest.Repository);
        Assert.Equal(baseObjectId, pullRequest.Selection.Base.ObjectId);
        Assert.Equal(headObjectId, pullRequest.Selection.Head.ObjectId);
        Assert.Equal(baseHead.TotalEffort, pullRequest.TotalEffort);
        Assert.Equal(baseHead.Categories, pullRequest.Categories);
    }

    [Fact]
    public async Task MergeCommitRequiresAndHonorsExplicitParent()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("switch", "--quiet", "-c", "feature");
        repository.WriteText("Feature.cs", "namespace Demo; public sealed class Feature { }\n");
        _ = await repository.CommitAsync("feature");
        await repository.GitAsync("switch", "--quiet", "main");
        repository.WriteText("MainOnly.cs", "namespace Demo; public sealed class MainOnly { }\n");
        string selectedParent = await repository.CommitAsync("main");
        await repository.GitAsync("merge", "--quiet", "--no-ff", "--no-edit", "feature");
        string mergeObjectId = await repository.GitAsync("rev-parse", "HEAD");

        ProcessResult ambiguous = await RunCliAsync(
            "change",
            repository.RootPath,
            "--commit",
            mergeObjectId,
            "--no-rate",
            "--compact");
        ProcessResult explicitParent = await RunCliAsync(
            "change",
            repository.RootPath,
            "--commit",
            mergeObjectId,
            "--parent",
            selectedParent,
            "--no-rate",
            "--compact");

        Assert.Equal(3, ambiguous.ExitCode);
        Assert.Contains("merge", ambiguous.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--parent", ambiguous.StandardError, StringComparison.Ordinal);
        Assert.Equal(0, explicitParent.ExitCode);
        Assert.Equal(string.Empty, explicitParent.StandardError);
        using JsonDocument report = JsonDocument.Parse(explicitParent.StandardOutput);
        Assert.Equal(
            selectedParent,
            report.RootElement.GetProperty("selection").GetProperty("base").GetProperty("objectId").GetString());
    }

    [Fact]
    public async Task ImmutableSnapshotStreamsDrainPartialReadsWithoutDesynchronizingGitBatch()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("first.txt", new string('a', 128 * 1024));
        repository.WriteText("second.txt", "second-object\n");
        string objectId = await repository.CommitAsync("streaming");
        await using IChangeSnapshot snapshot = await new GitClient().OpenSnapshotAsync(
            repository.RootPath,
            objectId);
        string firstPath = Path.Combine(snapshot.RootPath, "first.txt");
        string secondPath = Path.Combine(snapshot.RootPath, "second.txt");

        await using (Stream first = snapshot.FileSystem.OpenRead(firstPath, 4096))
        {
            Assert.Equal('a', first.ReadByte());
        }

        byte[] second = await snapshot.FileSystem.ReadAllBytesAsync(secondPath);
        byte[] firstAgain = await snapshot.FileSystem.ReadAllBytesAsync(firstPath);
        Assert.Equal("second-object\n", Encoding.UTF8.GetString(second));
        Assert.Equal(128 * 1024, firstAgain.Length);
        Assert.True(firstAgain.All(value => value == (byte)'a'));
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "Fairbill.Cli",
            "bin",
            configuration,
            "net10.0",
            "fairbill.dll");
        ProcessStartInfo startInfo = StartInfo("dotnet", repositoryRoot);
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunAsync(startInfo);
    }

    private static async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            (await stdout).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            (await stderr).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

    private static ProcessStartInfo StartInfo(string executable, string workingDirectory) => new(executable)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

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

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class FixedPullRequestResolver(string baseObjectId, string headObjectId)
        : IPullRequestResolver
    {
        public Task<ResolvedPullRequest> ResolveAsync(
            string repositoryPath,
            string input,
            string? repository,
            CancellationToken cancellationToken = default)
        {
            _ = repositoryPath;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ResolvedPullRequest
            {
                BaseObjectId = baseObjectId,
                HeadObjectId = headObjectId,
                Reference = new PullRequestReference
                {
                    Input = input,
                    Number = int.Parse(input, System.Globalization.CultureInfo.InvariantCulture),
                    Repository = repository,
                },
            });
        }
    }

    private sealed class GitFixture : IDisposable
    {
        private GitFixture(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static async Task<GitFixture> CreateAsync()
        {
            string rootPath = Path.Combine(
                Path.GetTempPath(),
                "fairbill-change-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            GitFixture fixture = new(rootPath);
            await fixture.GitAsync("init", "--initial-branch=main");
            await fixture.GitAsync("config", "user.name", "Fairbill E2E");
            await fixture.GitAsync("config", "user.email", "fairbill-e2e@example.invalid");
            return fixture;
        }

        public void WriteText(string relativePath, string content)
        {
            string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public async Task<string> CommitAsync(string message)
        {
            await GitAsync("add", "--all");
            await GitAsync("commit", "--quiet", "-m", message);
            return await GitAsync("rev-parse", "HEAD");
        }

        public async Task<string> GitAsync(params string[] arguments)
        {
            ProcessStartInfo startInfo = StartInfo("git", RootPath);
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            ProcessResult result = await RunAsync(startInfo);
            Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {result.StandardError}");
            return result.StandardOutput;
        }

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(RootPath, recursive: true);
        }
    }
}
