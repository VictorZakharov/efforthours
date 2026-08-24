using System.Diagnostics;

namespace EffortHours.EndToEndTests;

public sealed class AgentCliTests
{
    [Fact]
    public async Task PackagedCodexSkillRequiresExplicitInstallAndDetectsStaleness()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "efforthours-codex-skill-e2e",
            Guid.NewGuid().ToString("N"));
        try
        {
            ProcessResult printed = await RunAsync(root, "agent", "codex");

            Assert.Equal(0, printed.ExitCode);
            Assert.Contains("name: efforthours", printed.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(
                "efforthours-integration-contract: efforthours-codex/1.0.0",
                printed.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "request sufficient permission",
                printed.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains(
                "Keep EH end-to-end time distinct from total conversation latency.",
                printed.StandardOutput,
                StringComparison.Ordinal);
            Assert.False(Directory.Exists(root));

            ProcessResult missing = await RunAsync(root, "agent", "codex", "--check");
            Assert.Equal(3, missing.ExitCode);
            Assert.Equal(
                "status=missing integrationContract=efforthours-codex/1.0.0",
                missing.StandardOutput);
            Assert.False(Directory.Exists(root));

            ProcessResult installed = await RunAsync(root, "agent", "codex", "--install");
            Assert.Equal(0, installed.ExitCode);
            string skillPath = Path.Combine(root, "efforthours", "SKILL.md");
            Assert.True(File.Exists(skillPath));
            Assert.Equal(
                printed.StandardOutput.TrimEnd() + "\n",
                (await File.ReadAllTextAsync(skillPath)).ReplaceLineEndings("\n"));

            ProcessResult current = await RunAsync(root, "agent", "codex", "--check");
            Assert.Equal(0, current.ExitCode);
            Assert.StartsWith("status=current", current.StandardOutput, StringComparison.Ordinal);

            await File.AppendAllTextAsync(skillPath, "\n# local stale marker\n");
            ProcessResult stale = await RunAsync(root, "agent", "codex", "--check");
            Assert.Equal(3, stale.ExitCode);
            Assert.StartsWith("status=stale", stale.StandardOutput, StringComparison.Ordinal);

            ProcessResult updated = await RunAsync(root, "agent", "codex", "--install");
            Assert.Equal(0, updated.ExitCode);
            ProcessResult rechecked = await RunAsync(root, "agent", "codex", "--check");
            Assert.Equal(0, rechecked.ExitCode);
            Assert.StartsWith("status=current", rechecked.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<ProcessResult> RunAsync(string skillsRoot, params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ??
            throw new InvalidOperationException("Could not determine the test configuration.");
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
        startInfo.Environment["EFFORTHOURS_CODEX_SKILLS_ROOT"] = skillsRoot;
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start the EffortHours CLI.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            (await stdout).ReplaceLineEndings("\n").TrimEnd(),
            (await stderr).ReplaceLineEndings("\n").TrimEnd());
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
}
