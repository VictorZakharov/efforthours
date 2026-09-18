using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Theory]
    [InlineData(false, false, "selected")]
    [InlineData(false, false, "reviewer")]
    [InlineData(false, true, "reviewer")]
    [InlineData(true, false, "reviewer")]
    public async Task TwoEmailsSelectUnmergedWorkWithExactGitAliasesAndCompleteFallback(
        bool prOnly, bool fallback, string viewer)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Base.cs", "public class Base { }");
        string parent = await repository.CommitAsync("unselected base");
        await repository.GitAsync("config", "user.name", "Different Git Display Name");
        await repository.GitAsync("config", "user.email", "work@example.test");
        repository.WriteText("Work.cs", "public class Work { public int Value => 1; }");
        string work = await repository.CommitAsync("selected work");
        await repository.GitAsync("config", "user.email", "42+selected@users.noreply.github.com");
        repository.WriteText("Open.cs", "public class Open { public bool Enabled => true; }");
        string head = await repository.CommitAsync("unmerged selected work");
        DateTimeOffset timestamp = DateTimeOffset.Parse(
            await repository.GitAsync("show", "-s", "--format=%aI", head), CultureInfo.InvariantCulture);
        string cacheRoot = Path.Combine(repository.RootPath, "managed");
        string metadataRoot = Path.Combine(repository.RootPath, "metadata");
        await CloneBareAsync(repository.RootPath, Path.Combine(cacheRoot, "owner", "repository.git"));
        EmailContributorRunner runner = new(parent, work, head, timestamp)
        {
            PullOnly = prOnly,
            ForceFallback = fallback,
            Viewer = viewer,
        };
        GitHubAuthorPeriodDiscovery discovery = new(runner,
            new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), cacheRoot),
            new GitHubProviderMetadataCache(metadataRoot));
        GitHubAuthorPeriodDiscoveryRequest request = new()
        {
            Owner = "owner",
            AuthorAliases = ["work@example.test", "42+selected@users.noreply.github.com"],
            AsOf = timestamp.AddSeconds(1),
            TimeZone = "UTC",
            Scope = "engineering",
            IncludeOpenPullRequests = true,
            EngineeringScope = EngineeringScopeProfile.LoadBundled(),
        };
        GitHubAuthorPeriodDiscoveryResult result = await discovery.DiscoverTodayAsync(request);
        Assert.Equal(1, result.Discovery.OpenPullRequestHeadCount);
        Assert.Equal(1, result.Discovery.OpenPullRequestCount);
        Assert.Equal("provider-linked-aliases", result.Discovery.ProviderDiagnostics!.IdentityResolution);
        Assert.Equal(1, result.Discovery.ProviderDiagnostics.OpenPullRequestCandidateRepositoryCount);
        Assert.Equal(1, runner.NoreplyReads);
        Assert.Equal(fallback ? 1 : 0, runner.RepositoryPullReads);
        Assert.Equal(0, runner.ViewerEmailReads);
        Assert.Equal(request.AuthorAliases.Order(StringComparer.OrdinalIgnoreCase),
            Assert.Single(result.Manifest.Contributors).Aliases);
        string digest = ChangeAuthorPeriodManifestIdentity.ComputeDigest(result.Manifest);
        GitAuthorPeriodManifestPortfolioPlan plan = await new GitPortfolioPlanner().PlanAuthorPeriodManifestAsync(
            result.Manifest, digest, result.RepositoryPaths);
        Assert.Equal(2, plan.Items.Count);

        // A legacy cache causes one cold refresh, including on the email-only path.
        string entry = Assert.Single(Directory.GetFiles(metadataRoot, "*.json"));
        string legacy = (await File.ReadAllTextAsync(entry)).Replace(
            "github-provider-metadata-cache/1.1.0", "github-provider-metadata-cache/1.0.0", StringComparison.Ordinal);
        await File.WriteAllTextAsync(entry, legacy);
        GitHubAuthorPeriodDiscoveryResult migrated = await discovery.DiscoverTodayAsync(request);
        Assert.Equal("unsupported-protocol", migrated.Discovery.ProviderDiagnostics!.MetadataCacheStatus);
        GitHubAuthorPeriodDiscoveryResult repeated = await discovery.DiscoverTodayAsync(
            request with { AuthorAliases = [.. request.AuthorAliases.Reverse()] });
        Assert.Equal("hit-owner-only", repeated.Discovery.ProviderDiagnostics!.MetadataCacheStatus);
        Assert.Equal(digest, ChangeAuthorPeriodManifestIdentity.ComputeDigest(repeated.Manifest));

        if (!fallback)
        {
            runner.ForceFallback = true;
            GitHubAuthorPeriodDiscoveryResult explicitAccount = await discovery.DiscoverTodayAsync(
                request with { ProviderLogin = "selected" });
            Assert.Equal(digest, ChangeAuthorPeriodManifestIdentity.ComputeDigest(explicitAccount.Manifest));
            Assert.Equal("explicit-login", explicitAccount.Discovery.ProviderDiagnostics!.IdentityResolution);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnresolvedOrConflictingEmailIdentityCannotPublishCompletePrCoverage(bool conflict)
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        EmailContributorRunner runner = new(new('a', 40), new('b', 40), new('c', 40), DateTimeOffset.UtcNow)
        {
            Conflict = conflict,
            Empty = !conflict,
        };
        GitHubAuthorPeriodDiscovery discovery = new(runner,
            new GitHubRepositoryCache(new ExternalCommandRunner(), new GitClient(), repository.RootPath),
            new GitHubProviderMetadataCache(Path.Combine(repository.RootPath, "metadata")));
        GitHubProviderException error = await Assert.ThrowsAsync<GitHubProviderException>(() =>
            discovery.DiscoverTodayAsync(new()
            {
                Owner = "owner",
                AuthorAliases = ["work@example.test", "42+selected@users.noreply.github.com"],
                AsOf = DateTimeOffset.UtcNow,
                TimeZone = "UTC",
                Scope = "engineering",
                IncludeOpenPullRequests = true,
            }));
        Assert.Equal("github-contributor-identity-unresolved", error.Action.FailureCode);
        Assert.Equal(0, runner.RepositoryPullReads);
    }

    private sealed class EmailContributorRunner(
        string parent, string work, string head, DateTimeOffset timestamp) : IExternalCommandRunner
    {
        public string Viewer { get; init; } = "reviewer";
        public bool PullOnly { get; init; }
        public bool ForceFallback { get; set; }
        public bool Empty { get; init; }
        public bool Conflict { get; init; }
        public int NoreplyReads { get; private set; }
        public int RepositoryPullReads { get; private set; }
        public int ViewerEmailReads { get; private set; }

        public Task<ExternalCommandResult> RunAsync(string executable, string workingDirectory,
            IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool requireSuccess = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string endpoint = arguments[^1];
            string json;
            if (endpoint == "user")
            {
                json = JsonSerializer.Serialize(new { login = Viewer });
            }
            else if (endpoint == "users/owner")
            {
                json = """{"type":"Organization"}""";
            }
            else if (endpoint.StartsWith("user/emails", StringComparison.Ordinal))
            {
                ViewerEmailReads++;
                json = """[[{"email":"viewer@example.test","verified":true}]]""";
            }
            else if (endpoint == "users/selected")
            {
                NoreplyReads++;
                json = """{"login":"selected","id":42}""";
            }
            else if (endpoint.StartsWith("orgs/owner/repos", StringComparison.Ordinal))
            {
                json = """[[{"id":42,"full_name":"owner/repository","default_branch":"main"}]]""";
            }
            else if (arguments.Contains("--paginate") && arguments.Contains("graphql"))
            {
                Assert.Contains("login=selected", arguments);
                json = ForceFallback ? """[{"data":{"user":null}}]""" :
                    JsonSerializer.Serialize(new[] { new { data = new { user = new { pullRequests = new
                    {
                        totalCount = Empty ? 0 : 1,
                        nodes = Empty ? [] : new[] { new { number = 7, author = new { login = "selected" },
                            repository = new { nameWithOwner = "owner/repository" } } },
                        pageInfo = new { hasNextPage = false, endCursor = (string?)null },
                    } } } } });
            }
            else if (endpoint.Contains("/pulls?", StringComparison.Ordinal))
            {
                RepositoryPullReads++;
                json = JsonSerializer.Serialize(new[] { new[]
                {
                    new { number = 7, user = new { login = "selected" }, head = new { sha = head } },
                    new { number = 8, user = new { login = "reviewer" }, head = new { sha = parent } },
                } });
            }
            else if (endpoint.EndsWith("/pulls/7", StringComparison.Ordinal))
            {
                json = JsonSerializer.Serialize(new { commits = 2, head = new { sha = head } });
            }
            else if (endpoint.Contains("/pulls/7/commits", StringComparison.Ordinal))
            {
                json = new JsonArray(new JsonArray(RestCommit(work, parent, "work@example.test"),
                    RestCommit(head, work, "42+selected@users.noreply.github.com"))).ToJsonString();
            }
            else
            {
                Assert.Contains("graphql", arguments);
                JsonArray nodes = [];
                if (!PullOnly && !Empty)
                {
                    JsonObject commit = RestCommit(work, parent, "work@example.test");
                    JsonNode author = commit["commit"]!["author"]!.DeepClone();
                    author["user"] = new JsonObject { ["login"] = Conflict ? "different" : "selected" };
                    nodes.Add(new JsonObject
                    {
                        ["oid"] = work,
                        ["parents"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["oid"] = parent }) },
                        ["author"] = author,
                        ["committer"] = commit["commit"]!["committer"]!.DeepClone(),
                        ["authoredDate"] = timestamp,
                        ["committedDate"] = timestamp,
                        ["message"] = "work",
                    });
                }

                if (Conflict && nodes.Count > 0)
                {
                    JsonNode conflicting = nodes[0]!.DeepClone();
                    conflicting["oid"] = head;
                    conflicting["author"]!["user"]!["login"] = "selected";
                    nodes.Add(conflicting);
                }

                json = new JsonObject
                {
                    ["data"] = new JsonObject
                    {
                        ["r0"] = new JsonObject
                        {
                            ["defaultBranchRef"] = new JsonObject
                            {
                                ["name"] = "main",
                                ["target"] = new JsonObject
                                {
                                    ["history"] = new JsonObject { ["nodes"] = nodes, ["pageInfo"] = new JsonObject { ["hasNextPage"] = false } },
                                }
                            },
                        }
                    }
                }.ToJsonString();
            }

            return Task.FromResult(new ExternalCommandResult(0, json, ""));
        }

        private JsonObject RestCommit(string oid, string baseId, string email) => new()
        {
            ["sha"] = oid,
            ["parents"] = new JsonArray(new JsonObject { ["sha"] = baseId }),
            ["author"] = new JsonObject { ["login"] = "selected" },
            ["commit"] = new JsonObject
            {
                ["author"] = new JsonObject { ["name"] = "Different Git Display Name", ["email"] = email, ["date"] = timestamp },
                ["committer"] = new JsonObject { ["name"] = "Different Git Display Name", ["email"] = email, ["date"] = timestamp },
                ["message"] = "selected work",
            },
        };
    }
}
