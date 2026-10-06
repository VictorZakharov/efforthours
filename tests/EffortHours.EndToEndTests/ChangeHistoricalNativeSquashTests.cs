using System.Globalization;
using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    [Fact]
    public async Task NativeHistoryDiscoversRetainedChainAndSquashOnceWithOfflineEngineeringParity()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "efforthours-native-squash", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            using GitFixture repository = await GitFixture.CreateAsync(Path.Combine(workspace, "source"));
            repository.WriteText("Demo.csproj", ProjectFile);
            string baseline = await HistoricalCommitAsync(repository, "base", "2025-01-01T12:00:00Z", "2025-01-01T12:00:00Z");
            await repository.GitAsync("switch", "-c", "retained");
            repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
            string first = await HistoricalCommitAsync(repository, "first", "2026-01-19T12:00:00Z", "2026-03-13T12:00:00Z");
            repository.WriteText("Review.cs", "public class Review { public bool Ready => true; }");
            string retained = await HistoricalCommitAsync(repository, "second", "2026-01-20T12:00:00Z", "2026-03-13T13:00:00Z");
            await repository.GitAsync("update-ref", "refs/pull/7/head", retained);
            await repository.GitAsync("switch", "main");
            repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
            repository.WriteText("Review.cs", "public class Review { public bool Ready => true; }");
            string squash = await HistoricalCommitAsync(repository, "squash", "2026-01-19T12:00:00Z", "2026-03-13T14:00:00Z");
            HistoricalProviderRunner runner = new(baseline, first, retained, squash);
            GitHubAuthorPeriodDiscovery discovery = new(runner,
                new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), Path.Combine(workspace, "cache"), _ => repository.RootPath),
                new GitHubProviderMetadataCache(Path.Combine(workspace, "metadata")));
            GitHubAuthorPeriodDiscoveryRequest request = new()
            {
                Owner = "example",
                Repositories = ["example/repository"],
                AuthorAliases = ["selected"],
                AsOf = DateTimeOffset.Parse("2026-04-01T12:00:00Z", CultureInfo.InvariantCulture),
                TimeZone = "UTC",
                Scope = "engineering",
                SinceInclusive = DateTimeOffset.Parse("2026-01-19T00:00:00Z", CultureInfo.InvariantCulture),
                UntilExclusive = DateTimeOffset.Parse("2026-01-24T00:00:00Z", CultureInfo.InvariantCulture),
                IncludeOpenPullRequests = true,
                IncludeHistoricalPullRequests = true
            };
            GitHubAuthorPeriodDiscoveryResult pinned = await discovery.DiscoverTodayAsync(request);
            Assert.Equal(2, Assert.Single(pinned.Manifest.Repositories).Heads.Count);
            string manifestPath = Path.Combine(workspace, "pinned.json");
            await File.WriteAllTextAsync(manifestPath, ContractJson.Serialize(pinned.Manifest));
            string nativePath = Path.Combine(workspace, "native.json"), offlinePath = Path.Combine(workspace, "offline.json");
            ChangePortfolioCommand native = new(new ChangeEstimator(), (_, _, _, _, _) => throw new NotSupportedException(),
                (_, _, _, _) => throw new NotSupportedException(), (_, _) => throw new NotSupportedException(),
                (_, _, _) => throw new NotSupportedException(), null, discovery.DiscoverTodayAsync);
            using StringWriter stdout = new(CultureInfo.InvariantCulture), stderr = new(CultureInfo.InvariantCulture);
            Assert.True(await native.ExecuteAsync(["--native-period", "--owner", "example", "--repository", "example/repository", "--author", "selected",
                "--since", "2026-01-19", "--until", "2026-01-24", "--breakdown", "day", "--timezone", "UTC", "--scope", "engineering",
                "--capacity-hours-per-day", "8", "--generated-at", "2026-04-01T12:00:00Z", "--output", nativePath, "--no-rate"], stdout, stderr, CancellationToken.None) == 0, stderr.ToString());
            ProcessResult offline = await RunCliAsync("change", "portfolio", "--author-period-manifest", manifestPath,
                "--bucket", "calendar-day", "--scope", "engineering", "--output", offlinePath, "--no-rate");
            Assert.Equal(0, offline.ExitCode);
            ChangePortfolioComparisonReport report = ContractJson.Deserialize<ChangePortfolioComparisonReport>(await File.ReadAllTextAsync(nativePath));
            ChangePortfolioComparisonReport explicitReport = ContractJson.Deserialize<ChangePortfolioComparisonReport>(await File.ReadAllTextAsync(offlinePath));
            Assert.Equal(explicitReport.SourcePortfolio!.TotalEffort, report.SourcePortfolio!.TotalEffort);
            Assert.Equal(explicitReport.SourcePortfolio.Categories, report.SourcePortfolio.Categories);
            Assert.Equal(3, report.SourcePortfolio.Items.Count);
            Assert.Equal(0m, report.SourcePortfolio.Items.Single(item => item.Selection.Head.ObjectId == squash).AllocatedExpectedHours);
            Assert.Contains(report.SourcePortfolio.Items, item => item.ExactComposition is not null);
            Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5341");
            Assert.Equal(40m, report.Series.Single(series => series.Kind == ChangePortfolioSeriesKind.Portfolio).TotalCapacityHours);
            Assert.Equal(1, report.Discovery!.ProviderDiagnostics!.HistoricalPullRequests!.CacheHitCount);
        }
        finally { DeleteDirectory(workspace); }
    }
}
