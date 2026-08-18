using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task AuthorPeriodPortfolioPreservesEmptyCommitsAtEveryPosition()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");

        string emptyFirst = await CommitEmptyAsync(repository, "empty first");
        repository.WriteText(
            "Alpha.cs",
            "namespace Demo; public sealed class Alpha { public int Value => 1; }\n");
        string ordinaryFirst = await repository.CommitAsync("ordinary first");
        string emptyMiddle = await CommitEmptyAsync(repository, "empty middle");
        string consecutiveEmpty = await CommitEmptyAsync(repository, "consecutive empty");
        repository.WriteText(
            "Beta.cs",
            "namespace Demo; public sealed class Beta { public bool Enabled => true; }\n");
        string ordinaryLast = await repository.CommitAsync("ordinary last");
        string emptyLast = await CommitEmptyAsync(repository, "empty last");

        ProcessResult result = await RunCliAsync(
            "change",
            "portfolio",
            repository.RootPath,
            "--author", "selected@example.invalid",
            "--since", "2020-01-01T00:00:00Z",
            "--until", "2030-01-01T00:00:00Z",
            "--timezone", "UTC",
            "--no-rate",
            "--compact");

        Assert.True(result.ExitCode == 0, result.StandardError);
        using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
        JsonElement[] items = [.. report.RootElement.GetProperty("items").EnumerateArray()];
        Assert.Equal(6, items.Length);
        Assert.Equal(
            new[] { emptyFirst, ordinaryFirst, emptyMiddle, consecutiveEmpty, ordinaryLast, emptyLast }
                .Order(StringComparer.Ordinal),
            items.Select(HeadObjectId).Order(StringComparer.Ordinal));
        foreach (string objectId in new[] { emptyFirst, emptyMiddle, consecutiveEmpty, emptyLast })
        {
            AssertZeroPathItem(Assert.Single(items, item => HeadObjectId(item) == objectId));
        }

        Assert.All(
            items.Where(item => HeadObjectId(item) is not null &&
                new[] { ordinaryFirst, ordinaryLast }.Contains(HeadObjectId(item), StringComparer.Ordinal)),
            item => Assert.True(
                item.GetProperty("isolatedEffort").GetProperty("expected").GetDecimal() > 0m));
    }

    [Fact]
    public async Task AuthorPeriodManifestCountsOneEmptyCommitAcrossHeadsAndContributorsOnce()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Contributor B");
        await repository.GitAsync("config", "user.email", "selected-b@example.invalid");
        string empty = await CommitEmptyAsync(repository, "shared empty");

        await repository.GitAsync("switch", "-c", "open-change");
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Open.md", "# Open\n");
        string openHead = await repository.CommitAsync("open head");
        await repository.GitAsync("switch", "main");
        repository.WriteText("Default.md", "# Default\n");
        string defaultHead = await repository.CommitAsync("default head");

        string executionRoot = Path.Combine(
            Path.GetTempPath(),
            "efforthours-empty-commit-manifest",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(executionRoot);
        try
        {
            string manifestPath = Path.Combine(executionRoot, "portfolio.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = "1.0.0",
                    selection = new
                    {
                        sinceInclusive = "2020-01-01T00:00:00Z",
                        untilExclusive = "2030-01-01T00:00:00Z",
                        timeZone = "UTC",
                        dateField = "author",
                        mergePolicy = "exclude",
                        coauthorPolicy = "include",
                        intervalSemantics = "since-inclusive-until-exclusive",
                    },
                    contributors = new object[]
                    {
                        new { id = "contributor-a", aliases = ContributorAAliases },
                        new { id = "contributor-b", aliases = ContributorBAliases },
                    },
                    repositories = new[]
                    {
                        new
                        {
                            id = "repository-a",
                            repositoryPath = repository.RootPath,
                            heads = new[]
                            {
                                new { id = "default", objectId = defaultHead },
                                new { id = "open", objectId = openHead },
                            },
                        },
                    },
                }));

            ProcessResult result = await RunCliAsync(
                "change",
                "portfolio",
                "--author-period-manifest", manifestPath,
                "--no-rate",
                "--compact");

            Assert.True(result.ExitCode == 0, result.StandardError);
            using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
            JsonElement item = Assert.Single(report.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal(empty, HeadObjectId(item));
            AssertZeroPathItem(item);
            Assert.Equal(
                ["default", "open"],
                item.GetProperty("attribution").GetProperty("headIds")
                    .EnumerateArray().Select(value => value.GetString()));
            JsonElement match = Assert.Single(
                item.GetProperty("attribution").GetProperty("contributorMatches").EnumerateArray());
            Assert.Equal("contributor-b", match.GetProperty("contributorId").GetString());
            Assert.Contains(
                report.RootElement.GetProperty("aggregation").GetProperty("contributorGroups")
                    .EnumerateArray(),
                group => group.GetProperty("kind").GetString() == "single-contributor" &&
                    group.GetProperty("contributorIds")[0].GetString() == "contributor-a" &&
                    group.GetProperty("selectedCommitCount").GetInt32() == 0);
        }
        finally
        {
            DeleteDirectory(executionRoot);
        }
    }

    [Fact]
    public async Task AuthorPeriodPortfolioPreservesAnEmptyRootCommit()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        string root = await CommitEmptyAsync(repository, "empty root");

        ProcessResult result = await RunCliAsync(
            "change",
            "portfolio",
            repository.RootPath,
            "--author", "selected@example.invalid",
            "--since", "2020-01-01T00:00:00Z",
            "--until", "2030-01-01T00:00:00Z",
            "--timezone", "UTC",
            "--no-rate",
            "--compact");

        Assert.True(result.ExitCode == 0, result.StandardError);
        using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
        JsonElement item = Assert.Single(report.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(root, HeadObjectId(item));
        AssertZeroPathItem(item);
    }

    private static async Task<string> CommitEmptyAsync(GitFixture repository, string message)
    {
        await repository.GitAsync("commit", "--allow-empty", "--quiet", "-m", message);
        return await repository.GitAsync("rev-parse", "HEAD");
    }

    private static string? HeadObjectId(JsonElement item) =>
        item.GetProperty("selection").GetProperty("head").GetProperty("objectId").GetString();

    private static void AssertZeroPathItem(JsonElement item)
    {
        Assert.Equal(0, item.GetProperty("representedPathCount").GetInt32());
        JsonElement isolated = item.GetProperty("isolatedEffort");
        Assert.Equal(0m, isolated.GetProperty("low").GetDecimal());
        Assert.Equal(0m, isolated.GetProperty("expected").GetDecimal());
        Assert.Equal(0m, isolated.GetProperty("high").GetDecimal());
        Assert.Equal(0m, item.GetProperty("allocatedExpectedHours").GetDecimal());
    }
}
