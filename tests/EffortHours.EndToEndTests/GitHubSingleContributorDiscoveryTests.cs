using System.Globalization;
using System.Text.Json;
using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task SingleContributorDiscoveryIsIndependentOfViewerAndReusesOwnerMetadata()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.test");
        repository.WriteText("Feature.cs", "public class Feature { public int Value => 1; }");
        string head = await repository.CommitAsync("selected");
        DateTimeOffset timestamp = DateTimeOffset.Parse(
            await repository.GitAsync("show", "-s", "--format=%aI", head), CultureInfo.InvariantCulture);
        string cacheRoot = Path.Combine(repository.RootPath, "managed");
        await CloneBareAsync(repository.RootPath, Path.Combine(cacheRoot, "owner", "repository.git"));
        SingleContributorRunner runner = new(head, timestamp);
        GitHubAuthorPeriodDiscovery discovery = new(
            runner, new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), cacheRoot),
            new GitHubProviderMetadataCache(Path.Combine(repository.RootPath, "metadata")));
        GitHubAuthorPeriodDiscoveryRequest request = new()
        {
            Owner = "owner",
            AuthorAliases = ["@me"],
            AsOf = timestamp.AddSeconds(1),
            TimeZone = "UTC",
            Scope = "engineering",
            IncludeOpenPullRequests = true,
            EngineeringScope = EngineeringScopeProfile.LoadBundled(),
        };
        GitHubAuthorPeriodDiscoveryResult me = await discovery.DiscoverTodayAsync(request);
        GitHubAuthorPeriodDiscoveryResult explicitViewer = await discovery.DiscoverTodayAsync(
            request with { AuthorAliases = ["selected"] });
        runner.Viewer = "reviewer";
        GitHubAuthorPeriodDiscoveryResult other = await discovery.DiscoverTodayAsync(
            request with { AuthorAliases = ["selected"] });
        GitHubAuthorPeriodDiscoveryResult repeatedOther = await discovery.DiscoverTodayAsync(
            request with { AuthorAliases = ["selected"] });

        Assert.Equal(6, me.Discovery.ProviderQueryCount);
        Assert.Equal(4, explicitViewer.Discovery.ProviderQueryCount);
        Assert.Equal(5, other.Discovery.ProviderQueryCount);
        Assert.Equal(4, repeatedOther.Discovery.ProviderQueryCount);
        Assert.False(me.Discovery.ProviderMetadataCacheHit);
        Assert.True(explicitViewer.Discovery.ProviderMetadataCacheHit);
        Assert.Equal("hit-owner-only", repeatedOther.Discovery.ProviderDiagnostics!.MetadataCacheStatus);
        foreach (GitHubAuthorPeriodDiscoveryResult result in new[] { me, explicitViewer, other, repeatedOther })
        {
            Assert.Equal(["selected", "selected@example.test"], Assert.Single(result.Manifest.Contributors).Aliases);
            Assert.Equal(head, Assert.Single(Assert.Single(result.Manifest.Repositories).Heads).ObjectId);
            Assert.Equal(1, result.Discovery.ProviderDiagnostics!.OpenPullRequestAccountQueryCount);
            Assert.Empty(result.Discovery.ProviderDiagnostics.Fallbacks);
            Assert.DoesNotContain("reviewer", Assert.Single(result.Manifest.Contributors).Aliases);
        }

        runner.ForceAccountFallback = true;
        GitHubAuthorPeriodDiscoveryResult fallback = await discovery.DiscoverTodayAsync(
            request with { AuthorAliases = ["selected"] });
        Assert.Equal(0, fallback.Discovery.OpenPullRequestCount);
        Assert.Equal(0, fallback.Discovery.OpenPullRequestHeadCount);
        Assert.Equal("account-connection-unavailable",
            Assert.Single(fallback.Discovery.ProviderDiagnostics!.Fallbacks).Reason);
        Assert.Equal(1, runner.EmailReads);
    }

    private sealed class SingleContributorRunner(string head, DateTimeOffset timestamp) : IExternalCommandRunner
    {
        public string Viewer { get; set; } = "selected";
        public int EmailReads { get; private set; }
        public bool ForceAccountFallback { get; set; }

        public Task<ExternalCommandResult> RunAsync(
            string executable, string workingDirectory, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, bool requireSuccess = true)
        {
            string json;
            if (arguments.Contains("user"))
            {
                json = JsonSerializer.Serialize(new { login = Viewer });
            }
            else if (arguments.Contains("users/owner"))
            {
                json = """{"type":"Organization"}""";
            }
            else if (arguments.Contains("user/emails?per_page=100"))
            {
                EmailReads++;
                json = """[[{"email":"selected@example.test","verified":true}]]""";
            }
            else if (arguments.Contains("orgs/owner/repos?per_page=100&type=all"))
            {
                json = """[[{"id":42,"full_name":"owner/repository","default_branch":"main"}]]""";
            }
            else if (arguments.Any(argument => argument.Contains("/pulls?state=open", StringComparison.Ordinal)))
            {
                json = """[[{"number":8,"user":{"login":"reviewer"}}]]""";
            }
            else if (arguments.Contains("login=selected"))
            {
                json = ForceAccountFallback ? """[{"data":{"user":null}}]""" : """[{"data":{"user":{"pullRequests":{"totalCount":0,"nodes":[],"pageInfo":{"hasNextPage":false}}}}}]""";
            }
            else
            {
                Assert.Contains("graphql", arguments);
                Assert.Contains("name0=repository", arguments);
                json = JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        r0 = new
                        {
                            defaultBranchRef = new
                            {
                                name = "main",
                                target = new
                                {
                                    history = new
                                    {
                                        nodes = new[] { new
                            {
                                oid = head, parents = new { nodes = Array.Empty<object>() },
                                author = new { name = "Selected Contributor", email = "selected@example.test",
                                    user = new { login = "selected" } },
                                authoredDate = timestamp, committedDate = timestamp,
                                committer = new { name = "Selected Contributor", email = "selected@example.test" },
                                message = "selected",
                            } },
                                        pageInfo = new { hasNextPage = false },
                                    }
                                },
                            }
                        }
                    },
                });
            }

            return Task.FromResult(new ExternalCommandResult(0, json, ""));
        }
    }
}
