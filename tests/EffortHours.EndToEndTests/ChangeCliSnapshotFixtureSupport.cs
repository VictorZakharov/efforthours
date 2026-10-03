using System.Text;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.EndToEndTests;

public abstract partial class ChangeCliTestSupport
{
    protected static async Task<GitFixture> SnapshotFixtureAsync()
    {
        GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("App.csproj", ProjectFile);
        repository.WriteText("src/Main.cs", "public class Main { public int Add(int a, int b) => a + b; }\n");
        repository.WriteText("Other.cs", "public class Other { public string Message => \"source-secret\"; }\n");
        repository.WriteText("README.md", "# Synthetic snapshot portfolio fixture\n");
        repository.WriteText(".gitignore", "ignored.cs\n");
        repository.WriteText(".hidden", "hidden file\n");
        _ = await SnapshotCommitAtAsync(repository, "initial snapshot", "2026-01-15T12:00:00Z");
        return repository;
    }

    protected static (string Manifest, string Local, string Checkpoint) WriteSnapshotInputs(string execution, string repository)
    {
        SnapshotPortfolioManifest manifest = new()
        {
            Year = 2026,
            Timezone = "UTC",
            Profile = EstimationProfile.Implementation,
            Projects = [new() { Id = "demo", Ref = "main", Areas =
                [new() { Id = "source", Include = ["src/**"] }, new() { Id = "support", Include = ["**"] }] }],
        };
        SnapshotPortfolioLocalMap local = new() { Projects = [new() { Id = "demo", RepositoryPath = repository }] };
        string manifestPath = Path.Combine(execution, "portfolio.json");
        string localPath = Path.Combine(execution, "local.json");
        File.WriteAllText(manifestPath, ContractJson.SerializeDocument(manifest), Encoding.UTF8);
        File.WriteAllText(localPath, ContractJson.SerializeDocument(local), Encoding.UTF8);
        return (manifestPath, localPath, Path.Combine(execution, "checkpoint"));
    }

    protected static Task<ProcessResult> SnapshotRunAsync(string manifest, string local, string checkpoint, params string[] options) =>
        RunCliAsync(["estimate", "portfolio", "--manifest", manifest, "--local", local, "--checkpoint", checkpoint,
            "--as-of", "2026-04-15T12:00:00Z", "--no-rate", .. options]);

    protected static async Task<string> SnapshotCommitAtAsync(GitFixture repository, string message, string timestamp, bool allowEmpty = false)
    {
        await repository.GitAsync("add", "--all");
        System.Diagnostics.ProcessStartInfo start = StartInfo("git", repository.RootPath);
        start.Environment["GIT_AUTHOR_DATE"] = timestamp;
        start.Environment["GIT_COMMITTER_DATE"] = timestamp;
        foreach (string arg in new[] { "commit", "--quiet", "-m", message }) start.ArgumentList.Add(arg);
        if (allowEmpty) start.ArgumentList.Add("--allow-empty");
        ProcessResult commit = await RunAsync(start);
        Assert.True(commit.ExitCode == 0, commit.StandardError);
        return await repository.GitAsync("rev-parse", "HEAD");
    }
    protected static async Task<string> HistoricalCommitAsync(GitFixture repository, string message, string authorDate, string committerDate)
    {
        await repository.GitAsync("add", "--all");
        System.Diagnostics.ProcessStartInfo start = StartInfo("git", repository.RootPath);
        start.Environment["GIT_AUTHOR_DATE"] = authorDate;
        start.Environment["GIT_COMMITTER_DATE"] = committerDate;
        start.Environment["GIT_AUTHOR_NAME"] = "Selected Contributor";
        start.Environment["GIT_AUTHOR_EMAIL"] = "selected@example.invalid";
        foreach (string argument in new[] { "commit", "--quiet", "-m", message })
        {
            start.ArgumentList.Add(argument);
        }

        Assert.Equal(0, (await RunAsync(start)).ExitCode);
        return await repository.GitAsync("rev-parse", "HEAD");
    }

}
