using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using Xunit.Abstractions;

namespace EffortHours.Tests;

// Diagnostic reproductions of alpha.29 behavior, not acceptance tests for a fix.
public sealed class Alpha29FinalStateDiscrepancyTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(101, false)]
    [InlineData(101, true)]
    [InlineData(401, true)]
    public async Task PartialReversalRetainsDiscardedExpansionDespiteIdenticalEndpoints(
        int intermediateMethods, bool independentDays)
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot intermediate = Snapshot(intermediateMethods);
        InMemoryChangeSnapshot closing = Snapshot(2);
        ChangeEstimateReport expansion = await ChangeAsync(opening, intermediate);
        ChangeEstimateReport reduction = await ChangeAsync(intermediate, closing);
        ChangeEstimateReport endpoint = await ChangeAsync(opening, closing);
        ChangePortfolioSelection selection = Selection(closing.ObjectId);
        ChangePortfolioCandidate first = Candidate("expansion", expansion, 9);
        ChangePortfolioCandidate second = Candidate("reduction", reduction, 10);
        ChangePortfolioReport split = ChangePortfolioReconciler.Reconcile(selection,
            [first, second], EstimationProfile.Implementation, independentDays: independentDays);
        ChangePortfolioReport unsplit = ChangePortfolioReconciler.Reconcile(selection,
            [Candidate("endpoint", endpoint, 10)], EstimationProfile.Implementation,
            independentDays: independentDays);
        ChangePortfolioReport reordered = ChangePortfolioReconciler.Reconcile(selection,
            [second, first], EstimationProfile.Implementation, independentDays: independentDays);

        Assert.Equal(expansion.Selection.Head.ObjectId, reduction.Selection.Base.ObjectId);
        Assert.Equal(endpoint.Selection.Base, expansion.Selection.Base);
        Assert.Equal(endpoint.Selection.Head, reduction.Selection.Head);
        Assert.Equal(split.TotalEffort, reordered.TotalEffort);
        Assert.Equal(endpoint.TotalEffort, unsplit.TotalEffort);
        Assert.True(Hours(split.Categories, EffortCategory.ProductionImplementation) >
            Hours(unsplit.Categories, EffortCategory.ProductionImplementation));
        Assert.DoesNotContain(split.Adjustments, item => item.Kind == ChangePortfolioAdjustmentKind.Revert);
        Assert.Equal(split.TotalEffort.Expected, split.Items.Sum(item => item.AllocatedExpectedHours));
        Assert.Empty(ContractValidation.Validate(split));
        Assert.Empty(ContractValidation.Validate(unsplit));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioReport,
            ContractJson.Serialize(split));
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(ContractJson.Deserialize<ChangePortfolioReport>(
            ContractJson.Serialize(split))));
        Assert.Equal(split.TotalEffort, ContractJson.Deserialize<ChangePortfolioReport>(
            ContractJson.Serialize(split)).TotalEffort);

        // This fixture has one represented source path. No category can hide a
        // redistribution between paths: its retained contribution is the maximum.
        foreach (CategoryEstimate category in split.Categories)
        {
            Assert.Equal(Math.Max(Hours(expansion.Categories, category.Category),
                Hours(reduction.Categories, category.Category)), category.Hours.Expected);
        }
        output.WriteLine($"Intermediate methods: {intermediateMethods}; independent days: {independentDays}");
        output.WriteLine($"Expansion: {expansion.TotalEffort.Expected}; reduction: {reduction.TotalEffort.Expected}; " +
            $"endpoint: {endpoint.TotalEffort.Expected}; selected: {split.TotalEffort.Expected}");
        output.WriteLine($"Production endpoint: {Hours(endpoint.Categories, EffortCategory.ProductionImplementation)}; " +
            $"selected: {Hours(split.Categories, EffortCategory.ProductionImplementation)}");

        EstimateReport baseStock = await StockAsync(opening);
        EstimateReport headStock = await StockAsync(closing);
        output.WriteLine($"Signed stock growth: {headStock.TotalEffort.Expected - baseStock.TotalEffort.Expected}");
    }

    [Fact]
    public async Task CompleteReversalRemainsZeroWhilePartialReversalDoesNot()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot intermediate = Snapshot(101);
        ChangePortfolioReport reverted = ChangePortfolioReconciler.Reconcile(Selection(opening.ObjectId),
            [Candidate("expansion", await ChangeAsync(opening, intermediate), 9),
                Candidate("revert", await ChangeAsync(intermediate, opening), 10)],
            EstimationProfile.Implementation, independentDays: true);
        Assert.Equal(0m, reverted.TotalEffort.Expected);
        Assert.All(reverted.Items, item => Assert.Equal(0m, item.AllocatedExpectedHours));
        Assert.Contains(reverted.Adjustments, item => item.Kind == ChangePortfolioAdjustmentKind.Revert);
        Assert.Empty(ContractValidation.Validate(reverted));
    }

    [Fact]
    public async Task ChangedScopeResetsMarginalNormalizationAgainstUnchangedContext()
    {
        string unchanged = "namespace Demo; public sealed class Retained {\n" +
            string.Join('\n', Enumerable.Range(0, 400).Select(index =>
                $"public int RetainedOperation{index}(int input) => input * 2 + {index};")) + "\n}\n";
        InMemoryChangeSnapshot WithContext(int methods) => new(
            ("Demo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n"),
            ("Feature.cs", "namespace Demo; public sealed class Feature {\n" +
                string.Join('\n', Enumerable.Range(0, methods).Select(index =>
                    $"public int Operation{index}(int input) => input + {index};")) + "\n}\n"),
            ("ZUnchanged.cs", unchanged));
        InMemoryChangeSnapshot opening = WithContext(1);
        InMemoryChangeSnapshot closing = WithContext(101);
        ChangeAnalysisScope scope = ChangeAnalysisScope.CreateForFiles(opening.Files, closing.Files);
        Assert.Contains("Feature.cs", scope.Paths);
        Assert.DoesNotContain("ZUnchanged.cs", scope.Paths);
        async Task<EstimateReport> ScopedStockAsync(InMemoryChangeSnapshot snapshot) =>
            new SeedEstimator().Estimate(await new RepositoryAnalysisPipeline(
                new ScopedRepositoryFileSystem(snapshot.FileSystem, snapshot.RootPath, scope.Paths), cacheStore: null)
                .ScanAsync(snapshot.RootPath), EstimationProfile.Implementation);
        EstimateReport fullBase = await StockAsync(opening);
        EstimateReport fullHead = await StockAsync(closing);
        EstimateReport scopedBase = await ScopedStockAsync(opening);
        EstimateReport scopedHead = await ScopedStockAsync(closing);
        decimal fullGrowth = Hours(fullHead.Categories, EffortCategory.ProductionImplementation) -
            Hours(fullBase.Categories, EffortCategory.ProductionImplementation);
        decimal scopedGrowth = Hours(scopedHead.Categories, EffortCategory.ProductionImplementation) -
            Hours(scopedBase.Categories, EffortCategory.ProductionImplementation);
        Assert.True(scopedGrowth > fullGrowth);
        output.WriteLine($"Full production marginal: {fullGrowth}; changed-scope production marginal: {scopedGrowth}");
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
            RepositoryName = "public-synthetic-partial-reversal",
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

    private static async Task<EstimateReport> StockAsync(InMemoryChangeSnapshot snapshot) =>
        new SeedEstimator().Estimate(await new RepositoryAnalysisPipeline(snapshot.FileSystem, cacheStore: null)
            .ScanAsync(snapshot.RootPath), EstimationProfile.Implementation);

    private static decimal Hours(IReadOnlyList<CategoryEstimate> categories, EffortCategory category) =>
        categories.SingleOrDefault(item => item.Category == category)?.Hours.Expected ?? 0m;

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
