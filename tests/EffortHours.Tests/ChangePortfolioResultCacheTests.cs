using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioCommandTests
{
    [Fact]
    public async Task ResultCacheWarmRunIsIdenticalAndAnalyzesNothing()
    {
        GitChangePlan[] plans = CachePlans(3);
        MemoryResultCache cache = new("context");
        CountingEstimator coldEstimator = new();
        ChangePortfolioResultCacheCounters cold = new();

        ChangePortfolioEstimateBatch first = await new ChangeEstimator(coldEstimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(plans, EstimationProfile.Implementation,
                new ChangePortfolioExecutionTelemetry(), cache, cold);
        CountingEstimator warmEstimator = new();
        ChangePortfolioResultCacheCounters warm = new();
        ChangePortfolioEstimateBatch second = await new ChangeEstimator(warmEstimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(CachePlans(3), EstimationProfile.Implementation,
                new ChangePortfolioExecutionTelemetry(), cache, warm);
        IReadOnlyList<ChangeEstimateReport> uncached = await new ChangeEstimator()
            .EstimatePortfolioCandidatesAsync(CachePlans(3), EstimationProfile.Implementation);

        Assert.True(coldEstimator.InvocationCount > 0);
        Assert.Equal(0, warmEstimator.InvocationCount);
        Assert.Equal((3, 0), (cold.CommitRequests, cold.CommitHits));
        Assert.Equal((3, 3), (warm.CommitRequests, warm.CommitHits));
        Assert.Equal(3, cache.Writes);
        Assert.Equal(0, second.Statistics.SnapshotAnalysisRequests);
        Assert.Equal(ContractJson.Serialize(uncached), ContractJson.Serialize(first.Reports));
        Assert.Equal(ContractJson.Serialize(uncached), ContractJson.Serialize(second.Reports));
    }

    [Fact]
    public async Task ResultCacheAnalyzesOnlyNewCommitsAndPreservesOrder()
    {
        MemoryResultCache cache = new("context");
        await new ChangeEstimator().EstimatePortfolioCandidatesWithStatisticsAsync(CachePlans(2),
            EstimationProfile.Implementation, new ChangePortfolioExecutionTelemetry(), cache, null);
        GitChangePlan[] grown = [CachePlans(4)[3], .. CachePlans(2), CachePlans(4)[2]];
        CountingEstimator estimator = new();
        ChangePortfolioResultCacheCounters counters = new();

        ChangePortfolioEstimateBatch warm = await new ChangeEstimator(estimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(grown, EstimationProfile.Implementation,
                new ChangePortfolioExecutionTelemetry(), cache, counters);
        IReadOnlyList<ChangeEstimateReport> cold = await new ChangeEstimator()
            .EstimatePortfolioCandidatesAsync(grown, EstimationProfile.Implementation);

        Assert.Equal((4, 2), (counters.CommitRequests, counters.CommitHits));
        Assert.Equal(4, estimator.InvocationCount); // two new commits, base and head each
        Assert.Equal(ContractJson.Serialize(cold), ContractJson.Serialize(warm.Reports));
    }

    [Fact]
    public async Task ResultCacheDisposesSessionsOfFullyCachedPlans()
    {
        MemoryResultCache cache = new("context");
        await new ChangeEstimator().EstimatePortfolioCandidatesAsync(CachePlans(1), EstimationProfile.Implementation);
        await new ChangeEstimator().EstimatePortfolioCandidatesWithStatisticsAsync(CachePlans(1),
            EstimationProfile.Implementation, new ChangePortfolioExecutionTelemetry(), cache, null);
        GitSnapshotSession session = new("virtual-repository",
            (_, _, _) => throw new InvalidOperationException("A cached plan must not open snapshots."));

        await new ChangeEstimator().EstimatePortfolioCandidatesWithStatisticsAsync(
            [CachePlans(1)[0] with { SnapshotSession = session }], EstimationProfile.Implementation,
            new ChangePortfolioExecutionTelemetry(), cache, null);

        Assert.True(session.IsDisposed);
    }

    [Fact]
    public void CommitResultKeyBindsEveryEstimateInput()
    {
        MemoryResultCache cache = new("context");
        GitChangePlan plan = CachePlans(1)[0] with { RepositoryName = "repository" };
        string key = ChangePortfolioResultCacheKeys.ForCommit(cache, plan, EstimationProfile.Implementation);
        ChangeSelection selection = plan.Selection;
        string[] variants =
        [
            ChangePortfolioResultCacheKeys.ForCommit(new MemoryResultCache("other"), plan, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan, EstimationProfile.Recreation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with { RepositoryName = "renamed" }, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
            {
                Selection = selection with { Base = selection.Base with { ObjectId = new string('1', 40) } },
            }, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
            {
                Selection = selection with { Head = selection.Head with { ObjectId = new string('2', 40) } },
            }, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
            {
                Selection = selection with { Commit = "rewritten" },
            }, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
            {
                PathAdmission = new ChangePathAdmission("sha256:" + new string('a', 64), _ => true),
            }, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
            {
                Diagnostics = [new Diagnostic { Code = "FB0001", Severity = DiagnosticSeverity.Warning, Message = "planned" }],
            }, EstimationProfile.Implementation),
            ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
            {
                Components = [new ChangeComponentInput
                {
                    Selector = "component", BaseObjectId = new string('3', 40), HeadObjectId = new string('4', 40),
                    OpenBaseAsync = plan.OpenBaseAsync, OpenHeadAsync = plan.OpenHeadAsync,
                }],
            }, EstimationProfile.Implementation),
        ];

        Assert.Equal(key, ChangePortfolioResultCacheKeys.ForCommit(cache, plan with
        {
            RepositoryPath = "moved-checkout/repository",
        }, EstimationProfile.Implementation));
        Assert.All(variants, variant => Assert.NotEqual(key, variant));
        Assert.Equal(variants.Length, variants.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task FinalDeltaResultKeyBindsGroupMembersAndPolicyInputs()
    {
        MemoryResultCache cache = new("context");
        ChangeEstimateReport[] reports = [.. await new ChangeEstimator()
            .EstimatePortfolioCandidatesAsync(CachePlans(2), EstimationProfile.Implementation)];
        ChangePortfolioCandidate first = CacheCandidate("first", reports[0], 9);
        ChangePortfolioCandidate second = CacheCandidate("second", reports[1], 10);
        string Key(IEnumerable<ChangePortfolioCandidate> group, ChangePathAdmission? admission = null,
            EstimationProfile profile = EstimationProfile.Implementation, string repository = "repository",
            IChangePortfolioResultCache? other = null) =>
            ChangePortfolioResultCacheKeys.ForFinalDelta(other ?? cache, repository, group, admission, profile);
        string key = Key([first, second]);
        string[] variants =
        [
            Key([first, second], other: new MemoryResultCache("other")),
            Key([first, second], profile: EstimationProfile.Recreation),
            Key([first, second], repository: "renamed"),
            Key([first, second], new ChangePathAdmission("sha256:" + new string('a', 64), _ => true)),
            Key([first]),
            Key([first, second with { SelectorId = "renamed" }]),
            Key([first, second with { Report = reports[0] }]),
            Key([first, CacheCandidate("second", reports[1], 11)]),
            Key([first, second with
            {
                Attribution = second.Attribution with { HeadIds = ["other-head"] },
            }]),
        ];

        Assert.Equal(key, Key([second, first]));
        Assert.All(variants, variant => Assert.NotEqual(key, variant));
        Assert.Equal(variants.Length, variants.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CachedResultRequiresExactlyOneValidMember()
    {
        ChangePortfolioFinalDeltaRejection rejection = new()
        {
            Code = "composition-unproven",
            InputDigest = "sha256:" + new string('b', 64),
        };

        Assert.False(new ChangePortfolioCachedResult().IsValid());
        Assert.True(new ChangePortfolioCachedResult { FinalDeltaRejection = rejection }.IsValid());
        Assert.False(new ChangePortfolioCachedResult
        {
            FinalDeltaRejection = rejection with { Code = "unknown" },
        }.IsValid());
    }

    private static GitChangePlan[] CachePlans(int count) => [.. Enumerable.Range(0, count).Select(index =>
        CommitPlan(
            $"commit-{index}",
            State(("Demo.csproj", ProjectFile), ($"Before{index}.cs", $"namespace Demo; public sealed class Before{index} {{ }}\n")),
            State(("Demo.csproj", ProjectFile), ($"After{index}.cs", $"namespace Demo; public sealed class After{index} {{ public int Value => {index}; }}\n"))))];

    private static ChangePortfolioCandidate CacheCandidate(string id, ChangeEstimateReport report, int hour) => new()
    {
        RepositoryId = "repository",
        SelectorId = id,
        Report = report,
        Attribution = new ChangePortfolioAttribution
        {
            Kind = ChangePortfolioAttributionKind.DirectAuthor,
            ParentCount = 1,
            HeadIds = ["default"],
            ContributorMatches = [new ChangePortfolioContributorMatch
            {
                ContributorId = "contributor", Kind = ChangePortfolioContributorMatchKind.DirectAuthor,
            }],
            SelectedTimestamp = new DateTimeOffset(2026, 1, 1, hour, 0, 0, TimeSpan.Zero),
        },
    };

    /// <summary>Round-trips through contract JSON, like the durable store.</summary>
    internal sealed class MemoryResultCache(string context) : IChangePortfolioResultCache
    {
        private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

        public string Context { get; } = context;

        public int Writes { get; private set; }

        public int Loads { get; private set; }

        public Task<ChangePortfolioCachedResult?> TryLoadAsync(string key, CancellationToken cancellationToken)
        {
            Loads++;
            return Task.FromResult(_entries.TryGetValue(key, out string? json)
                ? ContractJson.Deserialize<ChangePortfolioCachedResult>(json)
                : null);
        }

        public Task StoreAsync(string key, ChangePortfolioCachedResult result, CancellationToken cancellationToken)
        {
            Assert.True(result.IsValid());
            Writes++;
            _entries[key] = ContractJson.Serialize(result);
            return Task.CompletedTask;
        }
    }
}
