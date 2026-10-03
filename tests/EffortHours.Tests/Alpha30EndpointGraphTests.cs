using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class Alpha30EndpointGraphTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForkMergeAndNonMonotonicDatesProveTheSameFinalDelta(bool independentDays)
    {
        InMemoryChangeSnapshot root = Snapshot(1, 1);
        InMemoryChangeSnapshot expanded = Snapshot(101, 1);
        InMemoryChangeSnapshot left = Snapshot(2, 1);
        InMemoryChangeSnapshot right = Snapshot(1, 7);
        InMemoryChangeSnapshot merged = Snapshot(2, 7);
        ChangePortfolioCandidate[] selected =
        [
            Candidate("expand", await Change(root, expanded), 9),
            Candidate("reduce", await Change(expanded, left), 8),
            Candidate("branch", await Change(root, right), 10),
            Candidate("merge", await Change(left, merged), 7, 2),
        ];
        var snapshots = new[] { root, expanded, left, right, merged }.ToDictionary(snapshot => snapshot.ObjectId);
        ChangePortfolioSelection selection = Selection(merged.ObjectId);
        async Task<IReadOnlyList<ChangePortfolioCandidate>> Prepare(ChangePortfolioCandidate[] candidates) =>
            await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(selection, candidates,
                EstimationProfile.Implementation, (_, id, _) => Task.FromResult<IChangeSnapshot>(snapshots[id]), independentDays);
        IReadOnlyList<ChangePortfolioCandidate> prepared = await Prepare(selected);
        ChangePortfolioFinalDelta receipt = Assert.Single(prepared, candidate => candidate.FinalDelta is not null).FinalDelta!;
        ChangeEstimateReport endpoint = await Change(root, merged);
        Assert.Equal(root.ObjectId, receipt.Report.Selection.Base.ObjectId);
        Assert.Equal(merged.ObjectId, receipt.Report.Selection.Head.ObjectId);
        Assert.Equal(endpoint.TotalEffort, receipt.Report.TotalEffort);
        Assert.Equal(ContractJson.Serialize(endpoint.Categories), ContractJson.Serialize(receipt.Report.Categories));
        Assert.True(receipt.Matches(prepared.ToDictionary(candidate => candidate.SelectorId)));
        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(selection, prepared,
            EstimationProfile.Implementation, independentDays: independentDays);
        ChangePortfolioReport reordered = ChangePortfolioReconciler.Reconcile(selection,
            await Prepare([.. selected.Reverse()]), EstimationProfile.Implementation, independentDays: independentDays);
        Assert.Equal(ContractJson.Serialize(report), ContractJson.Serialize(reordered));
        Assert.Equal(endpoint.TotalEffort, report.TotalEffort);
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioReport, ContractJson.Serialize(report)).IsValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5336");
        Assert.DoesNotContain(report.Diagnostics, diagnostic => diagnostic.Code == "FB5337");
    }

    [Fact]
    public async Task RejectionsIdentifyCompositionInventoryAndUnsupportedPathsWithoutDisclosingThem()
    {
        InMemoryChangeSnapshot root = Snapshot(1, 1);
        InMemoryChangeSnapshot expanded = Snapshot(101, 1);
        InMemoryChangeSnapshot closing = Snapshot(2, 1);
        ChangePortfolioCandidate first = Candidate("first", await Change(root, expanded), 9);
        ChangePortfolioCandidate second = Candidate("second", await Change(expanded, closing), 10);
        var snapshots = new[] { root, expanded, closing }.ToDictionary(snapshot => snapshot.ObjectId);
        async Task<ChangePortfolioFinalDeltaRejection> Reject(ChangePortfolioCandidate candidate)
        {
            var result = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(Selection(closing.ObjectId),
                [first, candidate], EstimationProfile.Implementation,
                (_, id, _) => Task.FromResult<IChangeSnapshot>(snapshots[id]));
            Assert.All(result, item => Assert.Null(item.FinalDelta));
            var rejection = Assert.Single(result, item => item.FinalDeltaRejection is not null).FinalDeltaRejection!;
            Assert.StartsWith("sha256:", rejection.InputDigest);
            Assert.DoesNotContain("Feature.cs", ContractJson.Serialize(rejection), StringComparison.Ordinal);
            return rejection;
        }
        var missing = second with { Report = second.Report with { Evidence = second.Report.Evidence with { Paths = [] } } };
        Assert.Equal("inventory-mismatch", (await Reject(missing)).Code);
        var branch = Candidate("branch", await Change(root, closing), 10);
        Assert.Equal("composition-unproven", (await Reject(branch)).Code);
        var unsupported = second with
        {
            Report = second.Report with
            {
                Evidence = second.Report.Evidence with
                {
                    Paths = [.. second.Report.Evidence.Paths.Select(path => path with { Classification = ChangePathClassification.Unsupported })],
                }
            }
        };
        Assert.Equal("unsupported-mode", (await Reject(unsupported)).Code);
    }

    [Fact]
    public async Task SuppressedRewriteWithDifferentExcludedEffectsCannotEstablishAnEndpoint()
    {
        InMemoryChangeSnapshot root = Snapshot(1, 1);
        InMemoryChangeSnapshot middle = Snapshot(2, 1);
        InMemoryChangeSnapshot final = Snapshot(3, 1);
        ChangePortfolioCandidate original = Candidate("original", await Change(root, middle), 9);
        ChangePortfolioCandidate rewrite = original with
        {
            SelectorId = "rewrite",
            Attribution = original.Attribution with { HeadIds = ["retained"], SelectedTimestamp = original.Attribution.SelectedTimestamp!.Value.AddMinutes(1) },
            Report = original.Report with
            {
                Evidence = original.Report.Evidence with
                {
                    Paths = [.. original.Report.Evidence.Paths, new ChangePathEvidence
                {
                    Id = "excluded-extra", Path = "private-generated.g.cs", Status = ChangePathStatus.Added,
                    HeadObjectId = "extra", Classification = ChangePathClassification.Generated, Represented = false, Reason = "Generated",
                }],
                }
            },
        };
        var result = await new ChangeEstimator().PreparePortfolioFinalDeltasAsync(Selection(final.ObjectId),
            [original, rewrite, Candidate("next", await Change(middle, final), 10)], EstimationProfile.Implementation,
            (_, _, _) => throw new InvalidOperationException("Unproven raw suppression must not open snapshots."));
        Assert.All(result, candidate => Assert.Null(candidate.FinalDelta));
        Assert.Equal("suppressed-raw-mismatch", Assert.Single(result, candidate => candidate.FinalDeltaRejection is not null).FinalDeltaRejection!.Code);
    }

    [Fact]
    public async Task LargeAnchorPopulationUsesOnlyTheOriginalPairAndRejectsExtraInventory()
    {
        InMemoryChangeSnapshot root = Snapshot(1, 1);
        InMemoryChangeSnapshot final = Snapshot(2, 1);
        ChangeEstimateReport report = await Change(root, final);
        ChangePortfolioCandidate[] candidates = [.. Enumerable.Range(0, 9).Select(index =>
            Candidate("selector-" + index, report with
            {
                Selection = report.Selection with
                {
                    Base = index == 0 ? report.Selection.Base : Reference("different-base-" + index),
                },
            }, 9))];
        int opens = 0;
        var result = await ChangePortfolioEndpointSearch.FindAsync("synthetic", candidates,
            new Dictionary<string, ChangePortfolioPathEffect>(), (_, id, _) =>
            {
                opens++;
                return Task.FromResult<IChangeSnapshot>(id == root.ObjectId ? root : final);
            }, null, CancellationToken.None);
        Assert.Null(result.Pair);
        Assert.Equal("anchor-bound", result.Code);
        Assert.Equal(2, opens);
    }

    private static InMemoryChangeSnapshot Snapshot(int left, int right) => new(
        ("Demo.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n"),
        ("Feature.cs", Source("Feature", left)), ("Side.cs", Source("Side", right)));

    private static string Source(string name, int count) => $"namespace Demo; public sealed class {name} {{\n" +
        string.Join('\n', Enumerable.Range(0, count).Select(index => $"public int Operation{index}(int input) => input + {index};")) + "\n}\n";

    private static Task<ChangeEstimateReport> Change(InMemoryChangeSnapshot before, InMemoryChangeSnapshot after) =>
        new ChangeEstimator().EstimateAsync(new ChangeEstimateInput
        {
            RepositoryName = "synthetic",
            Selection = new ChangeSelection
            {
                Kind = ChangeSelectionKind.Commit,
                Commit = after.ObjectId,
                Parent = before.ObjectId,
                Base = Reference(before.ObjectId),
                Head = Reference(after.ObjectId),
            },
            OpenBaseAsync = _ => Task.FromResult<IChangeSnapshot>(before),
            OpenHeadAsync = _ => Task.FromResult<IChangeSnapshot>(after),
        }, EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string id) => new() { Selector = id, ObjectId = id, Kind = ChangeSnapshotKind.GitTree };

    private static ChangePortfolioCandidate Candidate(string id, ChangeEstimateReport report, int hour, int parents = 1) => new()
    {
        RepositoryId = "synthetic",
        SelectorId = id,
        Report = report,
        Attribution = new ChangePortfolioAttribution
        {
            Kind = ChangePortfolioAttributionKind.DirectAuthor,
            ParentCount = parents,
            MergeCommit = parents > 1,
            HeadIds = ["default"],
            ContributorMatches = [new ChangePortfolioContributorMatch { ContributorId = "contributor", Kind = ChangePortfolioContributorMatchKind.DirectAuthor }],
            SelectedTimestamp = new DateTimeOffset(2026, 1, 1, hour, 0, 0, TimeSpan.Zero),
        },
    };

    private static ChangePortfolioSelection Selection(string head) => ChangeAuthorPeriodManifestIdentity.CreateReportSelection(new ChangeAuthorPeriodManifest
    {
        Selection = new ChangeAuthorPeriodManifestSelection
        {
            SinceInclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UntilExclusive = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            TimeZone = "UTC",
            DateField = ChangePortfolioDateField.Committer,
            MergePolicy = ChangePortfolioMergePolicy.FirstParent,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
        },
        Contributors = [new ChangeAuthorPeriodManifestContributor { Id = "contributor", Aliases = ["synthetic@example.invalid"] }],
        Repositories = [new ChangeAuthorPeriodManifestRepository { Id = "synthetic", RepositoryPath = "virtual",
            Heads = [new ChangeAuthorPeriodManifestHead { Id = "default", ObjectId = head }] }],
    });
}
