using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public abstract partial class ChangeCliTestSupport
{
    protected const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    protected static async Task<ProcessResult> RunCliAsync(params string[] arguments)
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
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunAsync(startInfo);
    }

    protected static async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo)
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

    protected static ProcessStartInfo StartInfo(string executable, string workingDirectory) => new(executable)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    protected static string FindRepositoryRoot()
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

    protected sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    protected sealed class FixedPullRequestResolver(string baseObjectId, string headObjectId)
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

    protected sealed class GitFixture : IDisposable
    {
        private GitFixture(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static async Task<GitFixture> CreateAsync(string? rootPath = null)
        {
            rootPath ??= Path.Combine(
                Path.GetTempPath(),
                "efforthours-change-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            GitFixture fixture = new(rootPath);
            await fixture.GitAsync("init", "--initial-branch=main");
            await fixture.GitAsync("config", "user.name", "EffortHours E2E");
            await fixture.GitAsync("config", "user.email", "efforthours-e2e@example.invalid");
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
