using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictResolvedRebaseRetainsNewBehaviorWithoutDuplicatingSharedWork(bool substantial)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        repository.WriteText("Feature.cs", "namespace Demo; public class Feature { public int Value => 0; }\n");
        string baseline = await repository.CommitAsync("baseline");
        await repository.GitAsync("switch", "-c", "original");
        repository.WriteText("Feature.cs", "namespace Demo; public class Feature { public int Value => 1; }\n");
        string originalFeature = await HistoricalCommitAsync(repository, "feature", "2026-01-19T12:00:00Z", "2026-01-19T12:00:00Z");
        repository.WriteText("Beta.cs", "namespace Demo; public class Beta { public bool Enabled => true; }\n");
        string original = await HistoricalCommitAsync(repository, "beta", "2026-01-20T12:00:00Z", "2026-01-20T12:00:00Z");
        await repository.GitAsync("switch", "main");
        repository.WriteText("Feature.cs", "namespace Demo; public class Feature { public int Value => 2; }\n");
        _ = await repository.CommitAsync("upstream conflict");
        await repository.GitAsync("switch", "-c", "rebased", original);
        var rebase = StartInfo("git", repository.RootPath);
        rebase.ArgumentList.Add("rebase");
        rebase.ArgumentList.Add("main");
        ProcessResult conflict = await RunAsync(rebase);
        Assert.NotEqual(0, conflict.ExitCode);
        Assert.Contains("Feature.cs", await repository.GitAsync("diff", "--name-only", "--diff-filter=U"), StringComparison.Ordinal);
        repository.WriteText("Feature.cs", "namespace Demo; public class Feature { public int Value => 3; }\n");
        if (substantial)
        {
            repository.WriteText("Resolution.cs", "namespace Demo; public class Resolution { public int Sum(int[] values) { int total = 0; foreach (int value in values) { if (value < 0) throw new System.ArgumentException(); total += value; } return total; } }\n");
        }
        await repository.GitAsync("add", "--all");
        await repository.GitAsync("-c", "core.editor=true", "rebase", "--continue");
        string rebased = (await repository.GitAsync("rev-parse", "HEAD")).Trim();
        string resolvedFeature = (await repository.GitAsync("rev-parse", "HEAD~1")).Trim();
        Assert.NotEqual(originalFeature, resolvedFeature);
        Assert.NotEqual(await repository.GitAsync("rev-parse", originalFeature + ":Feature.cs"),
            await repository.GitAsync("rev-parse", resolvedFeature + ":Feature.cs"));
        string root = Path.Combine(Path.GetTempPath(), "efforthours-rebase-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            async Task<JsonDocument> Estimate(params string[] heads)
            {
                string manifest = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifest, JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0.0",
                    selection = new
                    {
                        sinceInclusive = "2026-01-01T00:00:00Z",
                        untilExclusive = "2026-02-01T00:00:00Z",
                        timeZone = "UTC",
                        dateField = "author",
                        mergePolicy = "exclude",
                        coauthorPolicy = "include",
                        intervalSemantics = "since-inclusive-until-exclusive"
                    },
                    contributors = new[] { new { id = "selected", aliases = (string[])["selected@example.invalid"] } },
                    repositories = new[] { new { id = "repository", repositoryPath = repository.RootPath,
                        heads = heads.Select((head, index) => new { id = "head-" + index, objectId = head }) } }
                }));
                ProcessResult result = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifest, "--no-rate", "--compact");
                Assert.True(result.ExitCode == 0, result.StandardError);
                return JsonDocument.Parse(result.StandardOutput);
            }

            using JsonDocument originalOnly = await Estimate(original);
            using JsonDocument rebasedOnly = await Estimate(rebased);
            using JsonDocument combined = await Estimate(original, rebased);
            decimal Total(JsonDocument report) => report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal();
            Assert.Equal(Total(rebasedOnly), Total(combined));
            Assert.True(Total(combined) > 0);
            if (substantial)
            {
                Assert.True(Total(combined) > Total(originalOnly));
            }
            JsonElement[] items = [.. combined.RootElement.GetProperty("items").EnumerateArray()];
            Assert.Equal(4, items.Length);
            Assert.Single(items, item => item.TryGetProperty("duplicateOfItemId", out _));
            foreach (string head in new[] { originalFeature, resolvedFeature })
            {
                JsonElement item = Assert.Single(items, item => item.GetProperty("selection").GetProperty("head").GetProperty("objectId").GetString() == head);
                Assert.False(item.TryGetProperty("exactComposition", out _));
                Assert.False(item.TryGetProperty("duplicateOfItemId", out _));
                Assert.True(item.GetProperty("isolatedEffort").GetProperty("expected").GetDecimal() > 0m);
                Assert.True(item.GetProperty("allocatedExpectedHours").GetDecimal() > 0m);
            }
            repository.WriteText("FollowUp.cs", "namespace Demo; public class FollowUp { public string Name => \"follow-up\"; }\n");
            string followUp = await HistoricalCommitAsync(repository, "follow-up", "2026-01-21T12:00:00Z", "2026-03-20T12:00:00Z");
            string status = await repository.GitAsync("status", "--porcelain=v1");
            using JsonDocument extended = await Estimate(original, followUp);
            Assert.True(Total(extended) > Total(combined));
            Assert.Equal(Total(extended), extended.RootElement.GetProperty("items").EnumerateArray().Sum(item => item.GetProperty("allocatedExpectedHours").GetDecimal()));
            Assert.Equal(status, await repository.GitAsync("status", "--porcelain=v1"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }
}
