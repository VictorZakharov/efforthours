using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class GitHubProviderBatchingTests
{
    [Theory]
    [InlineData("42+selected@users.noreply.github.com", true)]
    [InlineData("42+SELECTED@USERS.NOREPLY.GITHUB.COM", true)]
    [InlineData("selected@users.noreply.github.com", false)]
    [InlineData("0+selected@users.noreply.github.com", false)]
    [InlineData("42+selected@users.noreply.github.com.example.test", false)]
    [InlineData("42+../selected@users.noreply.github.com", false)]
    public void NoreplyHintsRequireAnIdAndExactProviderDomain(string alias, bool valid)
    {
        Assert.Equal(valid, GitHubAuthorPeriodDiscoveryJson.TryParseNoreply(alias, out _, out _));
    }

    [Theory]
    [InlineData(42, "selected", true)]
    [InlineData(43, "selected", false)]
    [InlineData(42, "renamed", false)]
    public async Task NoreplyHintRequiresMatchingProviderAccount(long id, string login, bool resolves)
    {
        GitHubPullAuthorIdentity identity = new(["42+selected@users.noreply.github.com"], "viewer", []);
        QueueRunner runner = new($$"""{"id":{{id}},"login":"{{login}}"}""");
        await GitHubAuthorPeriodDiscoveryJson.ResolveNoreplyAliasesAsync(
            runner, "unused", identity, new(), default);
        Assert.Contains("users/selected", Assert.Single(runner.Calls));
        Assert.Equal(resolves ? "selected" : string.Empty, string.Join(",", identity.KnownLogins()));
        Assert.Equal(resolves ? 0 : 1, identity.UnresolvedAliases().Length);
    }

    [Fact]
    public void EmailResolutionIsExactBoundedAndDoesNotBorrowTheViewerOrCoauthors()
    {
        GitHubPullAuthorIdentity identity = new(["selected@example.test", "other@example.test"], "viewer", []);
        GitCommitMetadata commit = Metadata("selected@example.test") with
        {
            Coauthors = [new GitCommitIdentity("Other", "other@example.test")],
        };
        identity.Observe(null, commit);
        Assert.Empty(identity.KnownLogins());
        identity.Observe("selected", commit);
        Assert.Equal(["selected"], identity.KnownLogins());
        Assert.Equal(["other@example.test"], identity.UnresolvedAliases());
        Assert.Throws<GitHubProviderException>(() => identity.RequireComplete(["selected"]));
        identity.Observe("selected", Metadata("other@example.test"));
        identity.RequireComplete(["selected"]);
        identity.Observe("conflicting", commit);
        GitHubProviderException error = Assert.Throws<GitHubProviderException>(() => identity.KnownLogins());
        Assert.Equal("github-contributor-identity-unresolved", error.Action.FailureCode);
        Assert.DoesNotContain("selected@example.test", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, error.Action.RetryLimit);
    }

    [Fact]
    public async Task ResolvedEmailFallbackIncludesTheRequestedPrAndKeepsGitAliasesExact()
    {
        string head = new('a', 40);
        string parent = new('b', 40);
        QueueRunner runner = new(
            """
            [[{"number":7,"user":{"login":"selected"},"head":{"sha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}},
                {"number":8,"user":{"login":"viewer"}}]]
            """,
            """{"commits":1}""", RestCommitPage(head, parent));
        ProviderQueryCounters counters = new();
        DiscoveredRepository result = Assert.IsType<DiscoveredRepository>(
            await GitHubAuthorPeriodDiscoveryJson.DiscoverHeadsAsync(runner, "unused",
                new("42", "owner/repository", "main"), ["selected@example.test"], "viewer",
                Since, Until, ChangePortfolioDateField.Author, ChangePortfolioMergePolicy.Exclude,
                ChangePortfolioCoauthorPolicy.Include, true, counters, default,
                includeDefaultHead: false, includeAuthenticatedPullAuthor: false,
                pullAuthorLogins: ["selected"]));
        Assert.Equal(head, Assert.Single(result.Heads).ObjectId);
        Assert.Equal(3, counters.QueryCount);
        Assert.Equal(1, counters.OpenPullRequestCount);
        Assert.Equal(1, counters.Diagnostics("missing").OpenPullRequestCandidateRepositoryCount);
    }

    private static GitCommitMetadata Metadata(string email) => new()
    {
        ObjectId = new('a', 40),
        Author = new("Selected Contributor", email),
        Committer = new("Unrelated Committer", "committer@example.test"),
        AuthorTimestamp = Since,
        CommitterTimestamp = Since,
    };
}
