using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Tests;

public sealed class ChangePortfolioReconcilerTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task DisjointPullRequestsAreAdditiveOrderIndependentAndRenderable()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeEstimateReport first = await ReportAsync(
            ChangeSelectionKind.PullRequest,
            11,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Alpha.cs", "namespace Demo; public sealed class Alpha { public int Value => 1; }\n")));
        ChangeEstimateReport second = await ReportAsync(
            ChangeSelectionKind.PullRequest,
            12,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Beta.cs", "namespace Demo; public sealed class Beta { public string Value => \"two\"; }\n")));
        ChangePortfolioCandidate[] candidates =
        [
            Candidate("repo", "pr:11", first),
            Candidate("repo", "pr:12", second),
        ];

        ChangePortfolioReport report = Reconcile(PullRequests(), candidates);
        ChangePortfolioReport reversed = Reconcile(PullRequests(), [.. candidates.Reverse()]);
        ChangePortfolioReport priced = ChangePortfolioReconciler.Reconcile(
            PullRequests(),
            candidates,
            EstimationProfile.Implementation,
            new RateCard
            {
                Id = "test-rate/1.0.0",
                Name = "Test rate",
                Currency = "USD",
                HourlyRate = 100m,
                Methodology = "Test-only fixed rate.",
            });
        string json = new ChangePortfolioJsonRenderer().Render(report);
        string markdown = ChangePortfolioMarkdownRenderer.Render(report);

        Assert.Equal(
            ContractJson.SerializeCompact(report),
            ContractJson.SerializeCompact(reversed));
        Assert.Equal(report.IsolatedEffort, report.TotalEffort);
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));
        Assert.Empty(report.Adjustments);
        Assert.Equal(ChangePortfolioOrderPolicy.OrderIndependent, Assert.Single(report.RepositoryGroups).OrderPolicy);
        Assert.Equal(priced.TotalEffort.Expected * 100m, priced.TotalCost!.Expected);
        Assert.Equal(
            priced.TotalCost.Expected,
            priced.Items.Sum(item => item.AllocatedExpectedCost));
        Assert.Contains("\"schemaVersion\": \"1.0.0\"", json, StringComparison.Ordinal);
        Assert.Contains("not actual labor", markdown, StringComparison.Ordinal);
        Assert.Contains("Selected changes and exact allocations", markdown, StringComparison.Ordinal);
        Assert.Contains("Immutable base contexts", markdown, StringComparison.Ordinal);
        Assert.Contains(first.Selection.Base.ObjectId[..12], markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExactCherryPickedPatchIsCountedOnceWithZeroDuplicateAllocation()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState added = State(
            ("Demo.csproj", ProjectFile),
            ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 1; }\n"));
        ChangeEstimateReport first = await ReportAsync(ChangeSelectionKind.PullRequest, 21, initial, added);
        ChangeEstimateReport second = await ReportAsync(ChangeSelectionKind.PullRequest, 22, initial, added);

        ChangePortfolioReport report = Reconcile(
            PullRequests(),
            [Candidate("repo", "pr:21", first), Candidate("repo", "pr:22", second)]);

        ChangePortfolioItemEstimate duplicate = Assert.Single(
            report.Items,
            item => item.DuplicateOfItemId is not null);
        Assert.Equal(0m, duplicate.AllocatedExpectedHours);
        Assert.Equal(first.TotalEffort, report.TotalEffort);
        ChangePortfolioAdjustment adjustment = Assert.Single(report.Adjustments);
        Assert.Equal(ChangePortfolioAdjustmentKind.ExactDuplicate, adjustment.Kind);
        Assert.Equal(
            report.TotalEffort.Expected - report.IsolatedEffort.Expected,
            adjustment.EffortDelta.Expected);
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));
    }

    [Fact]
    public async Task OverlappingPullRequestsNormalizeSharedPathsAndExposeAmbiguity()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeEstimateReport first = await ReportAsync(
            ChangeSelectionKind.PullRequest,
            31,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 1; }\n")));
        ChangeEstimateReport second = await ReportAsync(
            ChangeSelectionKind.PullRequest,
            32,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Widget.cs", "namespace Demo; public sealed class Widget { public int Value => 2; }\n")));

        ChangePortfolioReport report = Reconcile(
            PullRequests(),
            [Candidate("repo", "pr:31", first), Candidate("repo", "pr:32", second)]);

        Assert.True(report.TotalEffort.Expected < report.IsolatedEffort.Expected);
        Assert.Contains(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.Overlap);
        Assert.Contains(Assert.Single(report.RepositoryGroups).UncertaintyReasons, reason =>
            reason.Contains("order-independent maximum", StringComparison.Ordinal));
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));
    }

    [Fact]
    public async Task ExactChronologicalAddThenRevertNormalizesToZero()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState added = State(
            ("Demo.csproj", ProjectFile),
            ("Temporary.cs", "namespace Demo; public sealed class Temporary { }\n"));
        ChangeEstimateReport add = await ReportAsync(ChangeSelectionKind.Commit, 1, initial, added);
        ChangeEstimateReport revert = await ReportAsync(ChangeSelectionKind.Commit, 2, added, initial);
        DateTimeOffset first = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            AuthorPeriod(initial.ObjectId),
            [
                Candidate("repo", "commit:add", add, first),
                Candidate("repo", "commit:revert", revert, first.AddHours(1)),
            ],
            EstimationProfile.Implementation);

        Assert.True(report.IsolatedEffort.Expected > 0m);
        Assert.Equal(new EffortRange { Low = 0m, Expected = 0m, High = 0m }, report.TotalEffort);
        Assert.All(report.Items, item => Assert.Equal(0m, item.AllocatedExpectedHours));
        Assert.Contains(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.Revert);
        Assert.Equal(ChangePortfolioOrderPolicy.ChronologicalSelectedCommits,
            Assert.Single(report.RepositoryGroups).OrderPolicy);
    }

    [Fact]
    public async Task StandaloneDeletionRemainsRepresented()
    {
        ChangeState initial = State(
            ("Demo.csproj", ProjectFile),
            ("Legacy.cs", "namespace Demo; public sealed class Legacy { public int Value => 1; }\n"));
        ChangeState final = State(("Demo.csproj", ProjectFile));
        ChangeEstimateReport deletion = await ReportAsync(ChangeSelectionKind.Commit, 3, initial, final);

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            AuthorPeriod(final.ObjectId),
            [Candidate("repo", "commit:delete", deletion, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero))],
            EstimationProfile.Implementation);

        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.Equal(report.IsolatedEffort, report.TotalEffort);
        Assert.DoesNotContain(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.Revert);
    }

    [Fact]
    public async Task ReintroducedAuthorChangeIsNotSuppressedAsACherryPick()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState added = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        ChangeEstimateReport firstAdd = await ReportAsync(ChangeSelectionKind.Commit, 4, initial, added);
        ChangeEstimateReport remove = await ReportAsync(ChangeSelectionKind.Commit, 5, added, initial);
        ChangeEstimateReport secondAdd = await ReportAsync(ChangeSelectionKind.Commit, 6, initial, added);
        DateTimeOffset first = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            AuthorPeriod(added.ObjectId),
            [
                Candidate("repo", "commit:add-one", firstAdd, first),
                Candidate("repo", "commit:remove", remove, first.AddHours(1)),
                Candidate("repo", "commit:add-two", secondAdd, first.AddHours(2)),
            ],
            EstimationProfile.Implementation);

        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.All(report.Items, item => Assert.Null(item.DuplicateOfItemId));
        Assert.DoesNotContain(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.ExactDuplicate);
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));
    }

    [Fact]
    public async Task IdenticalPatchesInDifferentRepositoriesRemainAdditive()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState added = State(
            ("Demo.csproj", ProjectFile),
            ("Widget.cs", "namespace Demo; public sealed class Widget { public bool Enabled => true; }\n"));
        ChangeEstimateReport first = await ReportAsync(ChangeSelectionKind.PullRequest, 41, initial, added);
        ChangeEstimateReport second = await ReportAsync(ChangeSelectionKind.PullRequest, 42, initial, added);

        ChangePortfolioReport report = Reconcile(
            PullRequests(manifestBased: true),
            [Candidate("repo-a", "first", first), Candidate("repo-b", "second", second)]);

        Assert.Equal(2, report.RepositoryGroups.Count);
        Assert.Equal(report.IsolatedEffort, report.TotalEffort);
        Assert.DoesNotContain(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.ExactDuplicate);
    }

    private static ChangePortfolioReport Reconcile(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioCandidate> candidates) =>
        ChangePortfolioReconciler.Reconcile(
            selection,
            candidates,
            EstimationProfile.Implementation);

    private static ChangePortfolioCandidate Candidate(
        string repositoryId,
        string selectorId,
        ChangeEstimateReport report,
        DateTimeOffset? timestamp = null) => new()
        {
            RepositoryId = repositoryId,
            SelectorId = selectorId,
            Report = report,
            Attribution = new ChangePortfolioAttribution
            {
                Kind = report.Selection.Kind == ChangeSelectionKind.PullRequest
                    ? ChangePortfolioAttributionKind.PullRequest
                    : ChangePortfolioAttributionKind.DirectAuthor,
                SelectedTimestamp = timestamp,
                MergeCommit = false,
                ParentCount = report.Selection.Kind == ChangeSelectionKind.Commit ? 1 : 0,
            },
        };

    private static ChangePortfolioSelection PullRequests(bool manifestBased = false) => new()
    {
        Kind = ChangePortfolioSelectionKind.PullRequests,
        ManifestBased = manifestBased,
    };

    private static ChangePortfolioSelection AuthorPeriod(string headObjectId) => new()
    {
        Kind = ChangePortfolioSelectionKind.AuthorPeriod,
        AuthorPeriod = new ChangePortfolioAuthorPeriodSelection
        {
            Aliases = ["Contributor <contributor@example.test>"],
            SinceInclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UntilExclusive = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TimeZone = "UTC",
            DateField = ChangePortfolioDateField.Author,
            MergePolicy = ChangePortfolioMergePolicy.Exclude,
            CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
            HeadSelector = "HEAD",
            HeadObjectId = headObjectId,
        },
    };

    private static async Task<ChangeEstimateReport> ReportAsync(
        ChangeSelectionKind kind,
        int number,
        ChangeState before,
        ChangeState after) => await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "portfolio-repository",
                Selection = new ChangeSelection
                {
                    Kind = kind,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                    Commit = kind == ChangeSelectionKind.Commit ? after.ObjectId : null,
                    Parent = kind == ChangeSelectionKind.Commit ? before.ObjectId : null,
                    PullRequest = kind == ChangeSelectionKind.PullRequest
                        ? new PullRequestReference
                        {
                            Input = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            Number = number,
                        }
                        : null,
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
