using System.Diagnostics;

namespace EffortHours.EndToEndTests;

public sealed class ReleaseMergeProvenanceTests
{
    [Fact]
    public void GuardAcceptsMergeWhoseTreeMatchesTestedPullRequestHead()
    {
        using GitFixture fixture = new();
        fixture.Commit("base.txt", "base\n", "Base");
        fixture.Git("switch", "-c", "release");
        fixture.Commit("release.txt", "release\n", "Prepare release");
        string releaseHead = fixture.Git("rev-parse", "HEAD").StandardOutput.Trim();
        fixture.Git("switch", "main");
        fixture.Git("merge", "--no-ff", "release", "-m", "Merge release");

        ProcessResult result = RunGuard(fixture.RootPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"pr_head={releaseHead}", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("tree=", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("tree_matches_pr_head=true", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            $"validation_commit={releaseHead}",
            result.StandardOutput,
            StringComparison.Ordinal);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void GuardOutputCanBeCapturedByAPowerShellCaller()
    {
        using GitFixture fixture = new();
        fixture.Commit("base.txt", "base\n", "Base");
        fixture.Git("switch", "-c", "release");
        fixture.Commit("release.txt", "release\n", "Prepare release");
        fixture.Git("switch", "main");
        fixture.Git("merge", "--no-ff", "release", "-m", "Merge release");

        ProcessResult result = RunGuardThroughPipelineCapture(fixture.RootPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("captured_count=5", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("merge_commit=", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("validation_commit=", result.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void GuardSelectsMergedCommitWhenFirstParentAddsParallelChanges()
    {
        using GitFixture fixture = new();
        fixture.Commit("base.txt", "base\n", "Base");
        fixture.Git("switch", "-c", "release");
        fixture.Commit("release.txt", "release\n", "Prepare release");
        fixture.Git("switch", "main");
        fixture.Commit("parallel.txt", "parallel\n", "Parallel change");
        fixture.Git("merge", "--no-ff", "release", "-m", "Merge release");
        string mergeCommit = fixture.Git("rev-parse", "HEAD").StandardOutput.Trim();

        ProcessResult result = RunGuard(fixture.RootPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "tree_matches_pr_head=false",
            result.StandardOutput,
            StringComparison.Ordinal);
        Assert.Contains(
            $"validation_commit={mergeCommit}",
            result.StandardOutput,
            StringComparison.Ordinal);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void GuardRejectsNonMergeCommit()
    {
        using GitFixture fixture = new();
        fixture.Commit("base.txt", "base\n", "Base");

        ProcessResult result = RunGuard(fixture.RootPath);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("must have exactly two parents", result.StandardError, StringComparison.Ordinal);
    }

    private static ProcessResult RunGuard(string workingDirectory)
    {
        string root = FindRepositoryRoot();
        string script = Path.Combine(root, "eng", "verify-tested-merge.ps1");
        string executable = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
        List<string> arguments = ["-NoLogo", "-NoProfile", "-NonInteractive"];
        if (OperatingSystem.IsWindows())
        {
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
        }

        arguments.Add("-File");
        arguments.Add(script);
        arguments.Add("-Commit");
        arguments.Add("HEAD");
        return Run(executable, workingDirectory, [.. arguments]);
    }

    private static ProcessResult RunGuardThroughPipelineCapture(string workingDirectory)
    {
        string root = FindRepositoryRoot();
        string script = Path.Combine(root, "eng", "verify-tested-merge.ps1");
        string escapedScript = script.Replace("'", "''", StringComparison.Ordinal);
        string command =
            $"$captured = @(& '{escapedScript}' -Commit HEAD); " +
            "Write-Output \"captured_count=$($captured.Count)\"; $captured";
        string executable = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
        List<string> arguments = ["-NoLogo", "-NoProfile", "-NonInteractive"];
        if (OperatingSystem.IsWindows())
        {
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
        }

        arguments.Add("-Command");
        arguments.Add(command);
        return Run(executable, workingDirectory, [.. arguments]);
    }

    private static ProcessResult Run(
        string executable,
        string workingDirectory,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
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

    private sealed class GitFixture : IDisposable
    {
        public GitFixture()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                $"efforthours-release-provenance-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
            Git("init", "--initial-branch=main");
            Git("config", "user.name", "Release Test");
            Git("config", "user.email", "release-test@example.invalid");
        }

        public string RootPath { get; }

        public void Commit(string path, string content, string message)
        {
            File.WriteAllText(Path.Combine(RootPath, path), content);
            Git("add", "--", path);
            Git("commit", "-m", message);
        }

        public ProcessResult Git(params string[] arguments)
        {
            ProcessResult result = Run("git", RootPath, arguments);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"git {string.Join(' ', arguments)} failed with exit code " +
                    $"{result.ExitCode}:{Environment.NewLine}{result.StandardError}");
            }

            return result;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                foreach (string file in Directory.EnumerateFiles(
                    RootPath,
                    "*",
                    SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
