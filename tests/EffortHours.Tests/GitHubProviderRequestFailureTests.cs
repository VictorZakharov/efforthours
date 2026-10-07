using EffortHours.Change;
using EffortHours.Contracts;

namespace EffortHours.Tests;

public sealed class GitHubProviderRequestFailureTests
{
    [Theory]
    [InlineData("HTTP 502: upstream failed private/repository", "http-failure", 502, null)]
    [InlineData("net/http: TLS handshake timeout https://private.invalid", "transport-timeout", null, "provider-transport")]
    [InlineData("context deadline exceeded secret-token", "transport-timeout", null, "provider-transport")]
    [InlineData("connection reset by peer private.invalid", "transport-failure", null, null)]
    [InlineData("HTTP 504: gateway deadline exceeded private.invalid", "http-failure", 504, null)]
    [InlineData("GraphQL: Internal error private/repository", "api-failure", null, null)]
    [InlineData("unrecognized private error", "process-exit", null, null)]
    public async Task FailedRequestsRetainSafeRootDetails(string stderr, string outcome, int? status, string? owner)
    {
        ProviderQueryCounters counters = new();
        var failure = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            GitHubAuthorPeriodDiscoveryJson.ResolveHistoricalDefaultHeadAsync(new ResultRunner(new(1, "", stderr)),
                "virtual-directory", new("id", "private/repository", "main"), counters, CancellationToken.None));
        var observed = counters.Diagnostics("missing").LastRequest!;
        Assert.Equal("rest", observed.Api);
        Assert.Equal("default-head", observed.Operation);
        Assert.Equal("incomplete", observed.State);
        Assert.Equal(1, observed.ExitCode);
        Assert.Equal(outcome, observed.Outcome);
        Assert.Equal(status, observed.HttpStatus);
        Assert.Equal(owner, observed.TimeoutOwner);
        if (status >= 500)
        {
            Assert.Equal("github-provider-service-unavailable", failure.Action.FailureCode);
            Assert.Equal("retry-after-provider-recovery", failure.Action.SuggestedAction);
            Assert.Equal(0, failure.Action.RetryLimit);
        }
        Assert.Equal(1, counters.QueryCount);
        Assert.Equal(0, counters.PageCount);
        Assert.DoesNotContain("private", ContractJson.Serialize(observed), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", ContractJson.Serialize(observed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedJsonIsParsingFailureRatherThanSuccessfulZero()
    {
        ProviderQueryCounters counters = new();
        var failure = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            GitHubAuthorPeriodDiscoveryJson.ResolveHistoricalDefaultHeadAsync(new ResultRunner(new(0, "not json private", "")),
                "virtual-directory", new("id", "private/repository", "main"), counters, CancellationToken.None));
        Assert.Equal("github-provider-response-malformed", failure.Action.FailureCode);
        Assert.Equal("response-malformed", counters.Diagnostics("missing").LastRequest!.Outcome);
        Assert.Equal(0, counters.PageCount);
    }

    [Fact]
    public void RootRequestWinsOverCancelledAndCompletedSiblingsAndFallbackCanComplete()
    {
        ProviderQueryCounters counters = new();
        using (var fallback = counters.ObserveRequest(["api", "graphql"], "open-pr-discovery"))
        { fallback.Result(new(1, "", "unsupported")); fallback.Fallback(); }
        using (var success = counters.ObserveRequest(["api", "repos/private/repository/pulls/1"], "open-pr-discovery"))
        { success.Result(new(0, "{}", "")); success.Complete(1); }
        Assert.Equal("pull-detail", counters.Diagnostics("missing").LastRequest!.Operation);
        using (var root = counters.ObserveRequest(["api", "graphql", "query=pullRequest(number: nodes{commit"], "open-pr-discovery"))
            root.Result(new(1, "", "HTTP 503: unavailable"));
        using (var sibling = counters.ObserveRequest(["api", "repos/private/repository/pulls/1/commits"], "open-pr-discovery"))
            sibling.Fail("cancelled", "sibling-failure");
        using (var sibling = counters.ObserveRequest(["api", "user"], "provider-authentication"))
            sibling.Complete(1);
        var observed = counters.Diagnostics("missing").LastRequest!;
        Assert.Equal("pull-metadata-batch", observed.Operation);
        Assert.Equal(503, observed.HttpStatus);
        Assert.Equal("graphql", observed.Api);
    }

    [Fact]
    public async Task AcceptedEmptyRepositoryKeepsTheActualHttpReceipt()
    {
        ProviderQueryCounters counters = new();
        var head = await GitHubAuthorPeriodDiscoveryJson.ResolveHistoricalDefaultHeadAsync(
            new ResultRunner(new(1, "", "gh: Git Repository is empty. (HTTP 409)")), "virtual-directory",
            new("id", "private/repository", "main"), counters, CancellationToken.None);
        Assert.Null(head);
        var request = counters.Diagnostics("missing").LastRequest!;
        Assert.Equal("complete", request.State);
        Assert.Equal("fallback", request.Outcome);
        Assert.Equal(1, request.ExitCode);
        Assert.Equal(409, request.HttpStatus);
        Assert.Equal(1, request.PageCount);
    }

    [Fact]
    public void CancellationOwnershipSeparatesTheSharedDeadlineFromRootFailureAndCaller()
    {
        using CancellationTokenSource caller = new();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
        GitHubDiscoveryAcquisitionBudget budget = new(new()
        {
            Owner = "owner",
            AsOf = DateTimeOffset.UnixEpoch,
            TimeZone = "UTC",
            Scope = "engineering",
        }, deadline, caller.Token);
        deadline.Cancel();
        Assert.Equal("discovery-deadline", budget.CancellationOwner());
        budget.Stop(new InvalidOperationException("root failure"));
        Assert.Equal("sibling-failure", budget.CancellationOwner());
        caller.Cancel();
        Assert.Equal("caller", budget.CancellationOwner());
    }

    private sealed class ResultRunner(ExternalCommandResult result) : IExternalCommandRunner
    {
        public Task<ExternalCommandResult> RunAsync(string executable, string directory, IReadOnlyList<string> arguments,
            CancellationToken token, bool requireSuccess = true) => Task.FromResult(result);
    }
}
