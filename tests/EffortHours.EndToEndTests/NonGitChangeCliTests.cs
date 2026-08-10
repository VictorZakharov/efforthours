using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class NonGitChangeCliTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task DirectoryPairIsDeterministicAndLeavesBothNonGitTreesUnchanged()
    {
        using NonGitFixture fixture = new();
        fixture.WriteBase("Demo.csproj", ProjectFile);
        fixture.WriteHead("Demo.csproj", ProjectFile);
        fixture.WriteHead(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        string[] baseBefore = Fingerprint(fixture.BasePath);
        string[] headBefore = Fingerprint(fixture.HeadPath);

        ProcessResult first = await RunCliAsync(
            "change",
            "--base-path",
            fixture.BasePath,
            "--head-path",
            fixture.HeadPath,
            "--no-rate",
            "--compact");
        ProcessResult second = await RunCliAsync(
            "change",
            "--base-path",
            fixture.BasePath,
            "--head-path",
            fixture.HeadPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(baseBefore, Fingerprint(fixture.BasePath));
        Assert.Equal(headBefore, Fingerprint(fixture.HeadPath));
        Assert.False(Directory.Exists(Path.Combine(fixture.BasePath, ".git")));
        Assert.False(Directory.Exists(Path.Combine(fixture.HeadPath, ".git")));
        Assert.DoesNotContain(fixture.RootPath, first.StandardOutput, StringComparison.OrdinalIgnoreCase);

        using JsonDocument report = JsonDocument.Parse(first.StandardOutput);
        JsonElement selection = report.RootElement.GetProperty("selection");
        Assert.Equal("directory", selection.GetProperty("base").GetProperty("kind").GetString());
        Assert.Equal("directory", selection.GetProperty("head").GetProperty("kind").GetString());
        Assert.StartsWith(
            "sha256:",
            selection.GetProperty("base").GetProperty("objectId").GetString(),
            StringComparison.Ordinal);
        Assert.True(report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal() > 0m);
        Assert.Contains(
            report.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB5110");
    }

    [Fact]
    public async Task SavedEvidencePairWorksWithoutARepositoryAndMatchesAnAddedFileDelta()
    {
        using NonGitFixture fixture = new();
        fixture.WriteBase("Demo.csproj", ProjectFile);
        fixture.WriteHead("Demo.csproj", ProjectFile);
        fixture.WriteHead(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        string baseEvidencePath = Path.Combine(fixture.RootPath, "base-evidence.json");
        string headEvidencePath = Path.Combine(fixture.RootPath, "head-evidence.json");
        Assert.Equal(0, (await RunCliAsync(
            "scan",
            fixture.BasePath,
            "--output",
            baseEvidencePath)).ExitCode);
        Assert.Equal(0, (await RunCliAsync(
            "scan",
            fixture.HeadPath,
            "--output",
            headEvidencePath)).ExitCode);

        ProcessResult directory = await RunCliAsync(
            "change",
            "--base-path",
            fixture.BasePath,
            "--head-path",
            fixture.HeadPath,
            "--no-rate",
            "--compact");
        ProcessResult evidence = await RunCliAsync(
            "change",
            "--base-evidence",
            baseEvidencePath,
            "--head-evidence",
            headEvidencePath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, evidence.ExitCode);
        Assert.Equal(string.Empty, evidence.StandardError);
        using JsonDocument directoryReport = JsonDocument.Parse(directory.StandardOutput);
        using JsonDocument evidenceReport = JsonDocument.Parse(evidence.StandardOutput);
        JsonElement selection = evidenceReport.RootElement.GetProperty("selection");
        Assert.Equal("evidence", selection.GetProperty("base").GetProperty("kind").GetString());
        Assert.Equal("evidence", selection.GetProperty("head").GetProperty("kind").GetString());
        Assert.Equal(
            directoryReport.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal(),
            evidenceReport.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal());
        Assert.Contains(
            evidenceReport.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB5111");
    }

    [Fact]
    public async Task NonGitSelectorsRejectIncompleteAndMixedPairsBeforeAnalysis()
    {
        using NonGitFixture fixture = new();

        ProcessResult incomplete = await RunCliAsync(
            "change",
            "--base-path",
            fixture.BasePath,
            "--no-rate");
        ProcessResult mixed = await RunCliAsync(
            "change",
            "--base-path",
            fixture.BasePath,
            "--head-path",
            fixture.HeadPath,
            "--base-evidence",
            "base.json",
            "--head-evidence",
            "head.json",
            "--no-rate");

        Assert.Equal(2, incomplete.ExitCode);
        Assert.Contains("--base-path and --head-path", incomplete.StandardError, StringComparison.Ordinal);
        Assert.Equal(2, mixed.ExitCode);
        Assert.Contains("Select exactly one", mixed.StandardError, StringComparison.Ordinal);
        Assert.Equal(string.Empty, incomplete.StandardOutput);
        Assert.Equal(string.Empty, mixed.StandardOutput);
    }

    private static string[] Fingerprint(string rootPath) =>
        [
            .. Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                .Select(path => $"{Path.GetRelativePath(rootPath, path).Replace('\\', '/')}:" +
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant())
                .Order(StringComparer.Ordinal),
        ];

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
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
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the EffortHours CLI.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            (await stdout).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            (await stderr).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

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

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class NonGitFixture : IDisposable
    {
        public NonGitFixture()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-non-git-change-e2e",
                Guid.NewGuid().ToString("N"));
            BasePath = Path.Combine(RootPath, "base");
            HeadPath = Path.Combine(RootPath, "head");
            Directory.CreateDirectory(BasePath);
            Directory.CreateDirectory(HeadPath);
        }

        public string RootPath { get; }

        public string BasePath { get; }

        public string HeadPath { get; }

        public void WriteBase(string relativePath, string content) =>
            Write(BasePath, relativePath, content);

        public void WriteHead(string relativePath, string content) =>
            Write(HeadPath, relativePath, content);

        public void Dispose()
        {
            string expectedParent = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "efforthours-non-git-change-e2e"));
            string resolvedRoot = Path.GetFullPath(RootPath);
            if (!resolvedRoot.StartsWith(
                    expectedParent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to remove a non-fixture directory.");
            }

            Directory.Delete(resolvedRoot, recursive: true);
        }

        private static void Write(string rootPath, string relativePath, string content)
        {
            string path = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
