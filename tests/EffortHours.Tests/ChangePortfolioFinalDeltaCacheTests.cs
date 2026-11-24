using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangePortfolioFinalDeltaCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedFinalDeltaReplaysEndpointOutcomeWithoutOpeningSnapshots(bool independentDays)
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot intermediate = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(2);
        ChangePortfolioCandidate[] candidates =
        [
            Candidate("expansion", await ChangeAsync(opening, intermediate), 9),
            Candidate("reduction", await ChangeAsync(intermediate, closing), 10),
        ];
        ChangePortfolioSelection selection = Selection(closing.ObjectId);
        int opened = 0;
        Task<IChangeSnapshot> Open(string repository, string id, CancellationToken cancellationToken)
        {
            opened++;
            return Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : closing);
        }

        ChangePortfolioCommandTests.MemoryResultCache cache = new("context");
        ChangePortfolioResultCacheCounters cold = new();
        IReadOnlyList<ChangePortfolioCandidate> uncached = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            selection, candidates, EstimationProfile.Implementation, Open, independentDays);
        IReadOnlyList<ChangePortfolioCandidate> first = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            selection, candidates, EstimationProfile.Implementation, Open, independentDays,
            resultCache: cache, cacheCounters: cold);
        ChangePortfolioResultCacheCounters warm = new();
        IReadOnlyList<ChangePortfolioCandidate> second = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            selection, candidates, EstimationProfile.Implementation,
            (_, _, _) => throw new InvalidOperationException("A cached final delta must not open snapshots."),
            independentDays, resultCache: cache, cacheCounters: warm);

        Assert.True(opened > 0);
        Assert.Contains(uncached, candidate => candidate.FinalDelta is not null);
        Assert.Equal((1, 0), (cold.FinalDeltaRequests, cold.FinalDeltaHits));
        Assert.Equal((1, 1), (warm.FinalDeltaRequests, warm.FinalDeltaHits));
        Assert.Equal(1, cache.Writes);
        Assert.Equal(ContractJson.Serialize(uncached), ContractJson.Serialize(first));
        Assert.Equal(ContractJson.Serialize(uncached), ContractJson.Serialize(second));
        Assert.Equal(
            ContractJson.Serialize(ChangePortfolioReconciler.Reconcile(selection, uncached,
                EstimationProfile.Implementation, independentDays: independentDays)),
            ContractJson.Serialize(ChangePortfolioReconciler.Reconcile(selection, second,
                EstimationProfile.Implementation, independentDays: independentDays)));
    }

    [Fact]
    public async Task ChangedGroupMemberMissesTheFinalDeltaCache()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot intermediate = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(2);
        ChangePortfolioCandidate expansion = Candidate("expansion", await ChangeAsync(opening, intermediate), 9);
        ChangePortfolioCandidate reduction = Candidate("reduction", await ChangeAsync(intermediate, closing), 10);
        ChangePortfolioSelection selection = Selection(closing.ObjectId);
        Task<IChangeSnapshot> Open(string repository, string id, CancellationToken cancellationToken) =>
            Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : closing);
        ChangePortfolioCommandTests.MemoryResultCache cache = new("context");
        await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(selection, [expansion, reduction],
            EstimationProfile.Implementation, Open, resultCache: cache);
        ChangePortfolioResultCacheCounters counters = new();

        await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(selection,
            [expansion, reduction with { Attribution = reduction.Attribution with { HeadIds = ["moved"] } }],
            EstimationProfile.Implementation, Open, resultCache: cache, cacheCounters: counters);

        Assert.Equal((1, 0), (counters.FinalDeltaRequests, counters.FinalDeltaHits));
    }

    private static InMemoryChangeSnapshot Snapshot(int methods) => new(
        ("Demo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n"),
        ("Feature.cs", "namespace Demo; public sealed class Feature {\n" +
            string.Join('\n', Enumerable.Range(0, methods).Select(index =>
                $"public int Operation{index}(int input) => input + {index};")) + "\n}\n"));

    private static Task<ChangeEstimateReport> ChangeAsync(
        InMemoryChangeSnapshot before, InMemoryChangeSnapshot after) => new ChangeEstimator().EstimateAsync(
        new ChangeEstimateInput
        {
            RepositoryName = "public-synthetic-cache",
            Selection = new ChangeSelection
            {
                Kind = ChangeSelectionKind.Commit,
                Base = Reference("base", before.ObjectId),
                Head = Reference("head", after.ObjectId),
                Commit = after.ObjectId,
                Parent = before.ObjectId,
            },
            OpenBaseAsync = _ => Task.FromResult<IChangeSnapshot>(before),
            OpenHeadAsync = _ => Task.FromResult<IChangeSnapshot>(after),
        }, EstimationProfile.Implementation);

    private static ChangePortfolioCandidate Candidate(string id, ChangeEstimateReport report, int hour) => new()
    {
        RepositoryId = "synthetic",
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

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangePortfolioSelection Selection(string head) =>
        ChangeAuthorPeriodManifestIdentity.CreateReportSelection(new ChangeAuthorPeriodManifest
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UntilExclusive = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Committer,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
            },
            Contributors = [new ChangeAuthorPeriodManifestContributor
            {
                Id = "contributor", Aliases = ["synthetic@example.test"],
            }],
            Repositories = [new ChangeAuthorPeriodManifestRepository
            {
                Id = "synthetic", RepositoryPath = "virtual",
                Heads = [new ChangeAuthorPeriodManifestHead { Id = "default", ObjectId = head }],
            }],
        });
}
