using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHubProviderFailureTests
{
    [Fact]
    public async Task ConfigurationAccessDenialProducesOneBoundedRetryAction()
    {
        const string SensitiveDetail =
            "failed to read C:\\Users\\private\\AppData\\Roaming\\GitHub CLI\\hosts.yml: Access is denied";
        FailureRunner runner = new(1, SensitiveDetail);

        GitHubProviderException exception = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            GitHubAuthorPeriodDiscoveryJson.ResolveViewerAsync(
                runner,
                "unrelated-folder",
                new ProviderQueryCounters(),
                CancellationToken.None));

        Assert.Equal("github-cli-config-access-denied", exception.Action.FailureCode);
        Assert.Equal("provider-authentication", exception.Action.Phase);
        Assert.Equal("retry-exact-command-with-permission", exception.Action.SuggestedAction);
        Assert.Equal(["eh", "change", "today"], exception.Action.SuggestedApprovalPrefix);
        Assert.Equal(1, exception.Action.RetryLimit);
        Assert.DoesNotContain("private", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hosts.yml", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not logged into any GitHub hosts; run gh auth login", "github-cli-unauthenticated")]
    [InlineData("HTTP 404: Not Found", "github-owner-forbidden-or-not-found")]
    [InlineData("API rate limit exceeded (HTTP 429)", "github-provider-rate-limited")]
    [InlineData("could not resolve host: api.github.com", "github-network-unavailable")]
    public void ProviderFailuresUseStableSafeCodes(string detail, string expectedCode)
    {
        GitHubProviderException failure = GitHubProviderFailure.FromResult(
            new ExternalCommandResult(1, string.Empty, detail),
            GitHubProviderFailure.OwnerInventoryPhase);

        Assert.Equal(expectedCode, failure.Action.FailureCode);
        Assert.Equal(0, failure.Action.RetryLimit);
        Assert.Empty(failure.Action.SuggestedApprovalPrefix);
        Assert.DoesNotContain(detail, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedDiscoveryRetainsObservedCountersWithoutChangingRootFailure()
    {
        FailureRunner runner = new(1, "could not resolve host: api.github.com");
        GitHubAuthorPeriodDiscovery discovery = new(runner,
            new GitHubRepositoryCache(runner, new GitClient(), "virtual-cache"));
        GitHubProviderException exception = await Assert.ThrowsAsync<GitHubProviderException>(() => discovery.DiscoverTodayAsync(new()
        {
            Owner = "private-owner",
            AuthorAliases = ["private@example.invalid"],
            AsOf = DateTimeOffset.UtcNow,
            TimeZone = "UTC",
            Scope = "engineering",
            EngineeringScope = EngineeringScopeProfile.LoadBundled(),
        }));
        ChangePortfolioHostDiscovery observed = Assert.IsType<ChangePortfolioHostDiscovery>(
            GitHubAuthorPeriodDiscovery.FailureDiscovery(exception));
        Assert.False(observed.Complete);
        Assert.Equal(1, observed.ProviderQueryCount);
        Assert.Equal(1, observed.ProviderProcessCount);
        Assert.Equal(0, observed.ProviderPageCount);
        Assert.Equal("github-network-unavailable", exception.Action.FailureCode);
        string json = ContractJson.Serialize(observed);
        Assert.DoesNotContain("private-owner", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private@example.invalid", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedRepositoryProbeRetainsSafePhaseAndElapsedContext()
    {
        ProviderQueryCounters counters = new();
        await Assert.ThrowsAsync<GitHubProviderException>(() =>
            GitHubAuthorPeriodDiscoveryJson.ResolveHistoricalDefaultHeadAsync(new FailureRunner(1, "HTTP 429: rate limit"),
                "virtual-directory", new GitHubDiscoveryRepository("id", "private-owner/private-repo", "main"),
                counters, CancellationToken.None));
        ChangePortfolioProviderRepositoryObservation observation = Assert.Single(counters.Diagnostics("missing").RepositoryObservations!);
        Assert.Equal("incomplete", observation.State);
        Assert.Equal(1, observation.QueryCount);
        Assert.Equal(0, observation.PageCount);
        Assert.StartsWith("sha256:", observation.RepositoryDigest, StringComparison.Ordinal);
        Assert.True(observation.ElapsedMilliseconds >= 0m);
        Assert.DoesNotContain("private-owner", ContractJson.Serialize(observation), StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderRequestBudgetStopsBeforeIssuingAnExtraRequest()
    {
        ProviderQueryCounters counters = new();
        for (int index = 0; index < ProviderQueryCounters.MaximumQueries; index++)
            counters.AddQuery(GitHubProviderFailure.DefaultHeadPhase);
        GitHubProviderException failure = Assert.Throws<GitHubProviderException>(() => counters.AddQuery(GitHubProviderFailure.DefaultHeadPhase));
        Assert.Equal("github-discovery-budget-exceeded", failure.Action.FailureCode);
        Assert.Equal("narrow-scope-or-use-pinned-manifest", failure.Action.SuggestedAction);
        Assert.Equal(ProviderQueryCounters.MaximumQueries, counters.QueryCount);
        Assert.Equal(ProviderQueryCounters.MaximumQueries, counters.Diagnostics("missing").DefaultHeadQueryCount);
        Assert.Equal(0, counters.ProcessCount);
    }

    private sealed class FailureRunner(int exitCode, string standardError) : IExternalCommandRunner
    {
        public Task<ExternalCommandResult> RunAsync(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ExternalCommandResult(exitCode, string.Empty, standardError));
        }
    }
}
