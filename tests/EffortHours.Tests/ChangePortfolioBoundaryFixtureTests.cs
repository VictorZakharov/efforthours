using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangePortfolioBoundaryFixtureTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task MechanicallySplitSequentialEditsNormalizeOneRepresentedPath()
    {
        ChangeState initial = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", Source(0)));
        ChangeState first = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", Source(1)));
        ChangeState second = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", Source(2)));
        DateTimeOffset timestamp = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            AuthorPeriod(second.ObjectId),
            [
                Candidate("commit:first", await ReportAsync(1, initial, first), timestamp),
                Candidate("commit:second", await ReportAsync(2, first, second), timestamp.AddMinutes(10)),
            ],
            EstimationProfile.Implementation);

        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.True(report.TotalEffort.Expected < report.IsolatedEffort.Expected);
        Assert.Contains(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.Overlap &&
            adjustment.AffectedPathCount == 1);
        Assert.DoesNotContain(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.Revert);
        Assert.Contains(Assert.Single(report.RepositoryGroups).UncertaintyReasons, reason =>
            reason.Contains("Sequential selected commits", StringComparison.Ordinal));
        AssertExactAllocation(report);
    }

    [Fact]
    public async Task SeparateImplementationTestAndDocumentationRowsRemainAdditive()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeEstimateReport implementation = await ReportAsync(
            11,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Feature.cs", Source(1))));
        ChangeEstimateReport quality = await ReportAsync(
            12,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("tests/FeatureTests.cs", TestSource()),
                ("docs/feature.md", "# Feature\n\nThe feature returns its configured value.\n")));

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            PullRequests(),
            [Candidate("pr:11", implementation), Candidate("pr:12", quality)],
            EstimationProfile.Implementation);

        Assert.Equal(report.IsolatedEffort, report.TotalEffort);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ProductionImplementation);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.UnitTesting);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.Documentation);
        Assert.Empty(report.Adjustments);
        AssertExactAllocation(report);
    }

    [Fact]
    public async Task SharedTestsAndDocumentationNormalizeWithoutErasingIndependentCode()
    {
        ChangeState initial = State(
            ("Demo.csproj", ProjectFile),
            ("tests/SharedTests.cs", "namespace Demo.Tests; public sealed class SharedTests { }\n"),
            ("docs/features.md", "# Features\n"));
        ChangeEstimateReport alpha = await ReportAsync(
            21,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Alpha.cs", "namespace Demo; public sealed class Alpha { }\n"),
                ("tests/SharedTests.cs", TestSource()),
                ("docs/features.md", "# Features\n\nAlpha and beta are covered.\n")));
        ChangeEstimateReport beta = await ReportAsync(
            22,
            initial,
            State(
                ("Demo.csproj", ProjectFile),
                ("Beta.cs", "namespace Demo; public sealed class Beta { }\n"),
                ("tests/SharedTests.cs", TestSource()),
                ("docs/features.md", "# Features\n\nAlpha and beta are covered.\n")));

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            PullRequests(),
            [Candidate("pr:21", alpha), Candidate("pr:22", beta)],
            EstimationProfile.Implementation);

        Assert.True(report.TotalEffort.Expected < report.IsolatedEffort.Expected);
        Assert.True(report.TotalEffort.Expected > Math.Max(
            alpha.TotalEffort.Expected,
            beta.TotalEffort.Expected));
        Assert.Contains(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.Overlap &&
            adjustment.AffectedPathCount == 2);
        Assert.Contains(report.Adjustments, adjustment =>
            adjustment.Kind == ChangePortfolioAdjustmentKind.SharedContext &&
            adjustment.ItemIds.Count == 2);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.UnitTesting);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.Documentation);
        Assert.Equal(
            Sum(Category(alpha, EffortCategory.ProductionImplementation),
                Category(beta, EffortCategory.ProductionImplementation)),
            Category(report, EffortCategory.ProductionImplementation));
        AssertExactAllocation(report);
    }

    [Fact]
    public async Task RevertedComponentReceivesNoAllocationFromIndependentWork()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState temporary = State(
            ("Demo.csproj", ProjectFile),
            ("Temporary.cs", "namespace Demo; public sealed class Temporary { }\n"));
        ChangeState final = State(
            ("Demo.csproj", ProjectFile),
            ("Retained.cs", "namespace Demo; public sealed class Retained { }\n"));
        DateTimeOffset timestamp = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            AuthorPeriod(final.ObjectId),
            [
                Candidate("commit:add", await ReportAsync(1, initial, temporary), timestamp),
                Candidate("commit:revert", await ReportAsync(2, temporary, initial), timestamp.AddMinutes(10)),
                Candidate("commit:retained", await ReportAsync(3, initial, final), timestamp.AddMinutes(20)),
            ],
            EstimationProfile.Implementation);

        ChangePortfolioItemEstimate add = Assert.Single(report.Items, item => item.SelectorId == "commit:add");
        ChangePortfolioItemEstimate revert = Assert.Single(report.Items, item => item.SelectorId == "commit:revert");
        ChangePortfolioItemEstimate retained = Assert.Single(report.Items, item => item.SelectorId == "commit:retained");
        Assert.Equal(0m, add.AllocatedExpectedHours);
        Assert.Equal(0m, revert.AllocatedExpectedHours);
        Assert.Equal(report.TotalEffort.Expected, retained.AllocatedExpectedHours);
        AssertExactAllocation(report);
    }

    private static void AssertExactAllocation(ChangePortfolioReport report) =>
        Assert.Equal(report.TotalEffort.Expected, report.Items.Sum(item => item.AllocatedExpectedHours));

    private static EffortRange Category(ChangeEstimateReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;

    private static EffortRange Category(ChangePortfolioReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;

    private static EffortRange Sum(EffortRange left, EffortRange right) => new()
    {
        Low = left.Low + right.Low,
        Expected = left.Expected + right.Expected,
        High = left.High + right.High,
    };

    private static string Source(int value) =>
        $"namespace Demo; public sealed class Feature {{ public int Value => {value}; }}\n";

    private static string TestSource() =>
        "namespace Demo.Tests; public sealed class SharedTests " +
        "{ [Xunit.Fact] public void FeatureWorks() { Xunit.Assert.True(true); } }\n";

    private static ChangePortfolioCandidate Candidate(
        string selectorId,
        ChangeEstimateReport report,
        DateTimeOffset? timestamp = null) => new()
        {
            RepositoryId = "repository",
            SelectorId = selectorId,
            Report = report,
            Attribution = new ChangePortfolioAttribution
            {
                Kind = timestamp is null
                    ? ChangePortfolioAttributionKind.PullRequest
                    : ChangePortfolioAttributionKind.DirectAuthor,
                SelectedTimestamp = timestamp,
                ParentCount = timestamp is null ? 0 : 1,
            },
        };

    private static async Task<ChangeEstimateReport> ReportAsync(
        int number,
        ChangeState before,
        ChangeState after) => await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "portfolio-boundary-fixture",
                Selection = new ChangeSelection
                {
                    Kind = number < 10 ? ChangeSelectionKind.Commit : ChangeSelectionKind.PullRequest,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                    Commit = number < 10 ? after.ObjectId : null,
                    Parent = number < 10 ? before.ObjectId : null,
                    PullRequest = number < 10
                        ? null
                        : new PullRequestReference
                        {
                            Input = number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            Number = number,
                        },
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangePortfolioSelection PullRequests() => new()
    {
        Kind = ChangePortfolioSelectionKind.PullRequests,
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
