using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class CalendarCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task CalendarMovedWindowReusesCommitResultsWithColdIdenticalOutput()
    {
        using GitFixture repo = await GitFixture.CreateAsync();
        await repo.GitAsync("config", "user.email", "calendar-person@example.test");
        repo.WriteText("Demo.csproj", ProjectFile);
        _ = await SnapshotCommitAtAsync(repo, "initial", "2026-01-10T12:00:00Z");
        repo.WriteText("Feature.cs", "public class Feature { public string Parse(string input) => input.Trim(); }\n");
        _ = await SnapshotCommitAtAsync(repo, "morning", "2026-02-10T09:00:00Z");
        repo.WriteText("Feature.cs", "public class Feature { public string Parse(string input) => input.Trim().ToUpperInvariant(); }\n");
        repo.WriteText("Other.cs", "public class Other { public int Count(string value) => value.Length; }\n");
        _ = await SnapshotCommitAtAsync(repo, "afternoon", "2026-02-10T15:00:00Z");
        repo.WriteText("Later.cs", "public class Later { public bool Ready => true; }\n");
        _ = await SnapshotCommitAtAsync(repo, "later", "2026-02-20T12:00:00Z");
        using GitFixture execution = await GitFixture.CreateAsync();
        string checkpoint = Path.Combine(execution.RootPath, "checkpoint");
        string[] February = ["calendar", repo.RootPath, "--from", "2026-02-01", "--to", "2026-02-28",
            "--timezone", "UTC", "--format", "json"];

        ProcessResult early = await RunCliAsync("calendar", repo.RootPath, "--from", "2026-02-01", "--to", "2026-02-15",
            "--timezone", "UTC", "--format", "json", "--checkpoint", checkpoint);
        ProcessResult warm = await RunCliAsync([.. February, "--checkpoint", checkpoint]);
        ProcessResult cold = await RunCliAsync(February);
        foreach (string file in Directory.EnumerateFiles(checkpoint, "*.json"))
            File.Delete(file); // drop repository evidence so only commit results remain
        ProcessResult cached = await RunCliAsync([.. February, "--checkpoint", checkpoint]);
        foreach (string file in Directory.EnumerateFiles(checkpoint, "*.json"))
            File.Delete(file);
        foreach (string file in Directory.EnumerateFiles(Path.Combine(checkpoint, "results"), "*", SearchOption.AllDirectories))
            await File.WriteAllBytesAsync(file, [0x1f, 0x8b, 0x00]);
        ProcessResult corrupt = await RunCliAsync([.. February, "--checkpoint", checkpoint]);

        foreach (ProcessResult result in new[] { early, warm, cold, cached, corrupt })
            Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Contains("result-cache commits=0/2, final-deltas=0/1", early.StandardError, StringComparison.Ordinal);
        Assert.Contains("result-cache commits=2/3, final-deltas=1/1", warm.StandardError, StringComparison.Ordinal);
        Assert.Contains("result-cache commits=3/3, final-deltas=1/1", cached.StandardError, StringComparison.Ordinal);
        Assert.Contains("result-cache commits=0/3, final-deltas=0/1", corrupt.StandardError, StringComparison.Ordinal);
        Assert.Contains("rejected=4", corrupt.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("result-cache", cold.StandardError, StringComparison.Ordinal);
        ChangePortfolioComparisonReport expected = ContractJson.Deserialize<ChangePortfolioComparisonReport>(cold.StandardOutput);
        foreach (ProcessResult result in new[] { warm, cached, corrupt })
        {
            ChangePortfolioComparisonReport actual = ContractJson.Deserialize<ChangePortfolioComparisonReport>(result.StandardOutput);
            Assert.Equal(expected.Verification.SemanticDigest, actual.Verification.SemanticDigest);
            Assert.Equal(ContractJson.Serialize(expected.SourcePortfolio), ContractJson.Serialize(actual.SourcePortfolio));
            Assert.Equal(ContractJson.Serialize(expected.Series), ContractJson.Serialize(actual.Series));
        }
    }
}
