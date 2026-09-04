using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class GitHubAuthorPeriodDiscoveryTests
{
    [Fact]
    public async Task ActiveContributorPopulationExcludesBotsServicesAndUnmappedAuthors()
    {
        string human = Commit("human", "User", "Human", "human@example.test");
        string bot = Commit("build[bot]", "Bot", "Build", "build@example.test");
        string service = Commit("release-bot", "User", "Release", "release@example.test");
        string unmapped = Commit(null, null, "Unmapped", "unmapped@example.test");
        QueueRunner runner = new(JsonSerializer.Serialize(new[] { new[]
        {
            JsonSerializer.Deserialize<JsonElement>(human),
            JsonSerializer.Deserialize<JsonElement>(bot),
            JsonSerializer.Deserialize<JsonElement>(service),
            JsonSerializer.Deserialize<JsonElement>(unmapped),
        } }));

        IReadOnlyList<GitHubActiveContributor> contributors =
            await GitHubAuthorPeriodDiscoveryJson.ListActiveContributorsAsync(
                runner,
                "virtual-workspace",
                new GitHubDiscoveryRepository("42", "owner/reference", "main"),
                new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                ChangePortfolioDateField.Author,
                ChangePortfolioMergePolicy.Exclude,
                new ProviderQueryCounters(),
                CancellationToken.None);

        GitHubActiveContributor selected = Assert.Single(contributors);
        Assert.Equal("human", selected.Login);
        Assert.Contains("human@example.test", selected.Aliases);
        Assert.Equal(
            GitHubAuthorPeriodDiscovery.SampleRank("seed", "HUMAN"),
            GitHubAuthorPeriodDiscovery.SampleRank("seed", "human"));
        Assert.NotEqual(
            GitHubAuthorPeriodDiscovery.SampleRank("seed", "human"),
            GitHubAuthorPeriodDiscovery.SampleRank("other-seed", "human"));
    }

    private static string Commit(string? login, string? type, string name, string email) =>
        JsonSerializer.Serialize(new
        {
            sha = new string('a', 40),
            parents = new[] { new { sha = new string('f', 40) } },
            author = login is null ? null : new { login, type },
            commit = new
            {
                author = new { name, email, date = "2026-08-21T12:00:00Z" },
                committer = new { name, email, date = "2026-08-21T12:00:00Z" },
                message = name,
            },
        });
}
