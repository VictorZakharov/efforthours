using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests : ChangeCliTestSupport
{
    [Fact]
    public async Task HistoricalDefaultsSelectUsingTheCompletedCrossRepositoryIdentityUnion()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "efforthours-historical-identity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            using GitFixture repository = await GitFixture.CreateAsync(Path.Combine(workspace, "source"));
            repository.WriteText("Demo.csproj", ProjectFile);
            string baseline = await HistoricalCommitAsync(repository, "base", "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z");
            repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
            string head = await HistoricalCommitAsync(repository, "feature", "2026-01-19T12:00:00Z", "2026-03-13T12:00:00Z");
            HistoricalProviderRunner runner = new(baseline, head, head, head, repositoryCount: 2, crossRepositoryAliases: true);
            GitHubAuthorPeriodDiscovery discovery = new(runner,
                new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), Path.Combine(workspace, "cache"),
                    _ => repository.RootPath), new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
            GitHubAuthorPeriodDiscoveryRequest request = new()
            {
                Owner = "example",
                AuthorAliases = ["selected"],
                TimeZone = "UTC",
                Scope = "engineering",
                AsOf = DateTimeOffset.Parse("2026-04-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                SinceInclusive = DateTimeOffset.Parse("2026-01-19T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                UntilExclusive = DateTimeOffset.Parse("2026-01-24T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                IncludeHistoricalPullRequests = true,
                IncludeOpenPullRequests = true,
            };
            GitHubAuthorPeriodDiscoveryResult cold = await discovery.DiscoverTodayAsync(request, CancellationToken.None);
            Assert.Equal(2, cold.Discovery.DefaultHeadCount);
            Assert.Equal(2, cold.Manifest.Repositories.Count);
            Assert.Contains("selected@example.invalid", Assert.Single(cold.Manifest.Contributors).Aliases);
            Assert.Contains("alternate@example.invalid", cold.Manifest.Contributors[0].Aliases);
            GitHubAuthorPeriodDiscoveryResult warm = await discovery.DiscoverTodayAsync(request, CancellationToken.None);
            Assert.Equal(ChangeAuthorPeriodManifestIdentity.ComputeDigest(cold.Manifest), ChangeAuthorPeriodManifestIdentity.ComputeDigest(warm.Manifest));
            Assert.Equal(2, runner.Calls.Count(call => call.Contains("&author=selected", StringComparison.Ordinal)));
            Assert.Empty(ContractValidation.Validate(warm.Manifest));
        }
        finally { DeleteDirectory(workspace); }
    }
}
