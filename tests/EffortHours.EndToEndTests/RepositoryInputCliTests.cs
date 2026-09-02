using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Review;

namespace EffortHours.EndToEndTests;

public sealed partial class PullRequestSelectionGitTests
{
    [Fact]
    public async Task EveryRepositoryQueryUsesOneCheckoutFreeSnapshotAndRunsOffline()
    {
        using GitFixture provider = await GitFixture.CreateAsync();
        provider.WriteText("Demo.csproj", ProjectFile);
        provider.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public bool Enabled => true; }\n");
        provider.WriteText("README.md", "# Demo\n\nA compact repository-query fixture.\n");
        string head = await provider.CommitAsync("repository snapshot");
        string providerRefs = await provider.GitAsync(
            "for-each-ref",
            "--format=%(refname):%(objectname)");
        string providerStatus = await provider.GitAsync("status", "--porcelain=v1");
        string cacheRoot = TemporaryCacheRoot();

        try
        {
            GitHubRepositoryCache repositoryCache = RepositoryCache(cacheRoot, provider.RootPath);
            Dictionary<string, string> revisions = new(StringComparer.Ordinal)
            {
                ["HEAD"] = head,
            };
            ManagedGitQueryPlanner livePlanner = new(
                new FixedRevisionResolver(revisions),
                repositoryCache,
                new GitHubRevisionResolutionCache(cacheRoot),
                new GitChangePlanner());
            RepositoryInputLoader liveLoader = CreateRepositoryInputLoader(livePlanner);
            RepositoryInputSelection remoteSelection = new()
            {
                GitHubRepository = "acme/demo",
                Revision = "HEAD",
                FetchMissing = true,
            };

            await using RepositoryInputContext cold = await liveLoader.LoadAsync(
                remoteSelection,
                allowEvidenceFile: true,
                scanOptions: null,
                CancellationToken.None);
            RepositoryEvidence local = await new RepositoryAnalysisPipeline().ScanAsync(
                provider.RootPath);
            RepositoryEvidence normalizedRemote = cold.Evidence with
            {
                Diagnostics =
                [.. cold.Evidence.Diagnostics.Where(diagnostic => diagnostic.Code != "FB5108")],
            };
            RepositoryEvidence normalizedLocal = local with
            {
                Repository = local.Repository with { Name = "demo" },
            };

            Assert.Equal(
                ContractJson.Serialize(normalizedLocal),
                ContractJson.Serialize(normalizedRemote));
            Assert.Contains(cold.Evidence.Diagnostics, diagnostic => diagnostic.Code == "FB5108");
            Assert.Equal(
                new SeedEstimator().Estimate(local, EstimationProfile.Implementation).TotalEffort,
                new SeedEstimator().Estimate(cold.Evidence, EstimationProfile.Implementation).TotalEffort);
            Assert.NotNull(cold.SourceContext);
            string[] sourceLines = await cold.SourceContext!.FileSystem.ReadAllLinesAsync(
                Path.Combine(cold.SourceContext.RepositoryRoot, "Feature.cs"));
            Assert.Contains("public sealed class Feature", Assert.Single(sourceLines));

            ManagedGitQueryPlanner offlinePlanner = new(
                new ThrowingRevisionResolver(),
                repositoryCache,
                new GitHubRevisionResolutionCache(cacheRoot),
                new GitChangePlanner());
            RepositoryInputLoader offlineLoader = CreateRepositoryInputLoader(offlinePlanner);
            EffortHoursApplication application = new(
                new SeedEstimator(),
                new RepositoryAnalysisPipeline(),
                offlineLoader);
            string[] common = ["--repo", "acme/demo", "--revision", "HEAD"];

            CommandResult scan = await RunRepositoryCommandAsync(application, ["scan", .. common]);
            AssertSuccessful(scan);
            CommandResult estimate = await RunRepositoryCommandAsync(
                application,
                ["estimate", .. common, "--no-rate", "--compact"]);
            AssertSuccessful(estimate);

            EstimateReport report = new SeedEstimator().Estimate(
                cold.Evidence,
                EstimationProfile.Implementation,
                rateCard: null);
            Assert.NotEmpty(report.WorkItems);
            string workItemId = report.WorkItems[0].Id;
            CommandResult explain = await RunRepositoryCommandAsync(
                application,
                ["explain", .. common, "--item", workItemId, "--compact"]);
            AssertSuccessful(explain);

            CommandResult packetResult = await RunRepositoryCommandAsync(
                application,
                ["review", "packet", .. common, "--compact"]);
            AssertSuccessful(packetResult);
            HostReviewPacket packet = ContractJson.Deserialize<HostReviewPacket>(packetResult.Output);
            CommandResult sourceQuery = await RunRepositoryCommandAsync(
                application,
                [
                    "review", "query", .. common,
                    "--input-digest", packet.InputDigest,
                    "--source", "Feature.cs",
                    "--reason", "checkout-free regression coverage",
                    "--compact",
                ]);
            AssertSuccessful(sourceQuery);
            Assert.Contains("public sealed class Feature", sourceQuery.Output, StringComparison.Ordinal);

            await AssertBareCacheHasNoMutableSelectionStateAsync(
                Path.Combine(cacheRoot, "acme", "demo.git"));
            Assert.Equal(providerRefs, await provider.GitAsync(
                "for-each-ref",
                "--format=%(refname):%(objectname)"));
            Assert.Equal(providerStatus, await provider.GitAsync("status", "--porcelain=v1"));
        }
        finally
        {
            DeleteCacheRoot(cacheRoot);
        }
    }

    private static RepositoryInputLoader CreateRepositoryInputLoader(
        ManagedGitQueryPlanner planner) => new(
            new RepositoryAnalysisPipeline(),
            fileSystem => new RepositoryAnalysisPipeline(fileSystem),
            planner,
            new GitClient());

    private static async Task<CommandResult> RunRepositoryCommandAsync(
        EffortHoursApplication application,
        string[] arguments)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = await application.RunAsync(arguments, output, error);
        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static void AssertSuccessful(CommandResult result)
    {
        Assert.True(
            result.ExitCode == CliExitCodes.Success,
            $"Command failed ({result.ExitCode}): {result.Error}\n{result.Output}");
        Assert.Equal(string.Empty, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
