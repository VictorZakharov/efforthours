using EffortHours.Change;

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
