using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using Xunit.Abstractions;

namespace EffortHours.Tests;

// Public semantic regressions; no private numerical targets.
public sealed class Alpha29FinalStateDiscrepancyTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(101, false)]
    [InlineData(101, true)]
    [InlineData(401, true)]
    public async Task PartialReversalUsesSelectedFinalEndpointDespiteIntermediateExpansion(
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
        IReadOnlyList<ChangePortfolioCandidate> prepared = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            selection, [first, second], EstimationProfile.Implementation,
            (_, id, _) => Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : closing),
            independentDays);
        ChangePortfolioReport split = ChangePortfolioReconciler.Reconcile(selection,
            prepared, EstimationProfile.Implementation, independentDays: independentDays);
        ChangePortfolioReport unsplit = ChangePortfolioReconciler.Reconcile(selection,
            [Candidate("endpoint", endpoint, 10)], EstimationProfile.Implementation,
            independentDays: independentDays);
        ChangePortfolioReport reordered = ChangePortfolioReconciler.Reconcile(selection,
            [.. prepared.Reverse()], EstimationProfile.Implementation, independentDays: independentDays);

        Assert.Equal(expansion.Selection.Head.ObjectId, reduction.Selection.Base.ObjectId);
        Assert.Equal(endpoint.Selection.Base, expansion.Selection.Base);
        Assert.Equal(endpoint.Selection.Head, reduction.Selection.Head);
        Assert.Equal(split.TotalEffort, reordered.TotalEffort);
        Assert.Equal(endpoint.TotalEffort, unsplit.TotalEffort);
        Assert.Equal(unsplit.TotalEffort, split.TotalEffort);
        Assert.Equal(ContractJson.Serialize(unsplit.Categories), ContractJson.Serialize(split.Categories));
        Assert.Contains(split.Diagnostics, diagnostic => diagnostic.Code == "FB5336");
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

    [Fact]
    public async Task MonotoneSplitAndDirectFinalDeltaHaveIdenticalRangesAndCategories()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot middle = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(201);
        ChangeEstimateReport endpoint = await ChangeAsync(opening, closing);
        IReadOnlyList<ChangePortfolioCandidate> prepared = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(closing.ObjectId), [Candidate("first", await ChangeAsync(opening, middle), 9),
                Candidate("second", await ChangeAsync(middle, closing), 10)], EstimationProfile.Implementation,
            (_, id, _) => Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : closing));
        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(Selection(closing.ObjectId), prepared,
            EstimationProfile.Implementation);
        Assert.Equal(endpoint.TotalEffort, report.TotalEffort);
        Assert.Equal(ContractJson.Serialize(endpoint.Categories), ContractJson.Serialize(report.Categories));
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task EndpointProofRejectsUnselectedRawChangesAndBrokenObjectChains()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot middle = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(2);
        ChangePortfolioCandidate first = Candidate("first", await ChangeAsync(opening, middle), 9);
        ChangePortfolioCandidate second = Candidate("second", await ChangeAsync(middle, closing), 10);
        // Removing a raw path from a report cannot make an endpoint's extra effect selected.
        ChangePortfolioCandidate incomplete = second with
        {
            Report = second.Report with
            {
                Evidence = second.Report.Evidence with { Paths = [] },
            }
        };
        IReadOnlyList<ChangePortfolioCandidate> failedInventory = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(closing.ObjectId), [first, incomplete], EstimationProfile.Implementation,
            (_, id, _) => Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : closing));
        Assert.All(failedInventory, candidate => Assert.Null(candidate.FinalDelta));
        IReadOnlyList<ChangePortfolioCandidate> broken = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(closing.ObjectId), [first, Candidate("branch", await ChangeAsync(opening, closing), 10)],
            EstimationProfile.Implementation, (_, _, _) => throw new InvalidOperationException("No snapshots should open."));
        Assert.All(broken, candidate => Assert.Null(candidate.FinalDelta));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(closing.ObjectId), [first, second], EstimationProfile.Implementation,
            (_, _, _) => throw new OperationCanceledException(), cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public async Task FinalDeltaReceiptCannotBeReusedForChangedCanonicalEvidence()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot middle = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(2);
        IReadOnlyList<ChangePortfolioCandidate> prepared = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(closing.ObjectId), [Candidate("first", await ChangeAsync(opening, middle), 9),
                Candidate("second", await ChangeAsync(middle, closing), 10)], EstimationProfile.Implementation,
            (_, id, _) => Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : closing));
        ChangePortfolioCandidate[] altered = [.. prepared.Select(candidate => candidate with
        {
            Attribution = candidate.Attribution with { AmbiguityReasons = ["Altered canonical attribution."] },
        })];
        Assert.Throws<InvalidOperationException>(() => ChangePortfolioReconciler.Reconcile(
            Selection(closing.ObjectId), altered, EstimationProfile.Implementation));
    }

    [Fact]
    public async Task IndependentDayReceiptsPreserveEarlierDayWhenWindowExtends()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot expansion = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(2);
        InMemoryChangeSnapshot later = Snapshot(51);
        ChangePortfolioCandidate first = Candidate("expansion", await ChangeAsync(opening, expansion), 9);
        ChangePortfolioCandidate second = Candidate("reduction", await ChangeAsync(expansion, closing), 10);
        ChangePortfolioCandidate third = Candidate("later", await ChangeAsync(closing, later), 10);
        third = third with
        {
            Attribution = third.Attribution with
            {
                SelectedTimestamp = third.Attribution.SelectedTimestamp!.Value.AddDays(1),
            }
        };
        Task<IChangeSnapshot> OpenAsync(string _, string id, CancellationToken token) =>
            Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : id == closing.ObjectId ? closing : later);
        IReadOnlyList<ChangePortfolioCandidate> narrow = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(closing.ObjectId), [first, second], EstimationProfile.Implementation, OpenAsync, true);
        IReadOnlyList<ChangePortfolioCandidate> wide = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(later.ObjectId, 2), [first, second, third], EstimationProfile.Implementation, OpenAsync, true);
        ChangePortfolioReport before = ChangePortfolioReconciler.Reconcile(Selection(closing.ObjectId), narrow,
            EstimationProfile.Implementation, independentDays: true);
        ChangePortfolioReport after = ChangePortfolioReconciler.Reconcile(Selection(later.ObjectId, 2), wide,
            EstimationProfile.Implementation, independentDays: true);
        Assert.Equal(ContractJson.Serialize(before.DailyNormalization!.Days[0]),
            ContractJson.Serialize(after.DailyNormalization!.Days[0]));
        Assert.Equal(after.TotalEffort.Expected, after.Items.Sum(item => item.AllocatedExpectedHours));
        Assert.Empty(ContractValidation.Validate(after));
    }

    [Fact]
    public async Task OmittedExcludedEffectCannotPassEndpointInventoryProof()
    {
        InMemoryChangeSnapshot opening = Snapshot(1);
        InMemoryChangeSnapshot expansion = Snapshot(101);
        InMemoryChangeSnapshot closing = Snapshot(2);
        InMemoryChangeSnapshot extra = new(
            ("Demo.csproj", System.Text.Encoding.UTF8.GetString(await closing.ReadAllBytesAsync("Demo.csproj"))),
            ("Feature.cs", System.Text.Encoding.UTF8.GetString(await closing.ReadAllBytesAsync("Feature.cs"))),
            ("Generated.g.cs", "// <auto-generated/>\nnamespace Demo; public class Generated { public int Value => 42; }\n"));
        ChangePortfolioCandidate second = Candidate("reduction", await ChangeAsync(expansion, extra), 10);
        Assert.Contains(second.Report.Evidence.Paths, path => !path.Represented && path.Path == "Generated.g.cs");
        second = second with
        {
            Report = second.Report with
            {
                Evidence = second.Report.Evidence with { Paths = [.. second.Report.Evidence.Paths.Where(path => path.Represented)] },
            }
        };
        IReadOnlyList<ChangePortfolioCandidate> prepared = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(
            Selection(extra.ObjectId), [Candidate("expansion", await ChangeAsync(opening, expansion), 9), second],
            EstimationProfile.Implementation,
            (_, id, _) => Task.FromResult<IChangeSnapshot>(id == opening.ObjectId ? opening : extra));
        Assert.All(prepared, candidate => Assert.Null(candidate.FinalDelta));
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

    private static ChangePortfolioSelection Selection(string head, int days = 1) =>
        ChangeAuthorPeriodManifestIdentity.CreateReportSelection(new ChangeAuthorPeriodManifest
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UntilExclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days),
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
