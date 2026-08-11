using EffortHours.Change;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class ChangeEstimatorEfficiencyTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>";

    [Fact]
    public async Task MixedCapabilityRolesHaveDisjointCategoriesWithoutMultiplyingBudget()
    {
        ChangeState before = State(
            ("src/Demo/Demo.csproj", ProjectFile),
            ("src/Demo/ResultExtensions.cs",
                "namespace Demo; public static class ResultExtensions { public static int Value() => 1; }\n"),
            ("tests/Demo.Tests/Demo.Tests.csproj", ProjectFile));
        ChangeState after = State(
            ("src/Demo/Demo.csproj", ProjectFile),
            ("src/Demo/ResultOptions.cs",
                "namespace Demo; public sealed class ResultOptions { public int Status { get; set; } }\n"),
            ("src/Demo/ResultExtensions.cs",
                "namespace Demo; public static class ResultExtensions { public static int Value() => 2; }\n"),
            ("tests/Demo.Tests/Demo.Tests.csproj", ProjectFile),
            ("tests/Demo.Tests/ResultExtensionsTests.cs",
                "namespace Demo.Tests; public sealed class ResultExtensionsTests { " +
                "[Fact] public void ReturnsTwo() { Assert.Equal(2, ResultExtensions.Value()); } }\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        WorkItem[] capability = [.. report.WorkItems.Where(item =>
            item.Estimator.Id == "change-rule:capability-marginal" &&
            item.Title.Contains("maintained internal logic", StringComparison.OrdinalIgnoreCase))];
        WorkItem[] production = [.. capability.Where(item =>
            item.Category == EffortCategory.ProductionImplementation)];
        WorkItem[] tests = [.. capability.Where(item => item.Category == EffortCategory.UnitTesting)];

        Assert.NotEmpty(production);
        Assert.NotEmpty(tests);
        Assert.Empty(
            production.SelectMany(item => item.EvidenceIds)
                .Intersect(tests.SelectMany(item => item.EvidenceIds), StringComparer.Ordinal));
        Assert.All(
            Evidence(report, production),
            path => Assert.DoesNotContain("role:test", path.Tags));
        Assert.All(
            Evidence(report, tests),
            path => Assert.Contains("role:test", path.Tags));
        Assert.Equal(2.5m, capability.Sum(item => item.Hours.Expected));
        Assert.All(report.WorkItems, item => Assert.InRange(item.Hours.Expected, 0.01m, 1.5m));
        Assert.Equal(
            report.WorkItems.Count,
            report.WorkItems.Select(item => item.Title).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ChangeEstimator.Version, report.EstimatorVersion);
    }

    [Fact]
    public async Task RangeAnalysisEstimatesEachUniqueSnapshotOnce()
    {
        ChangeState initial = State(("Demo.csproj", ProjectFile));
        ChangeState middle = State(
            ("Demo.csproj", ProjectFile),
            ("Alpha.cs", "namespace Demo; public sealed class Alpha { }\n"));
        ChangeState final = State(
            ("Demo.csproj", ProjectFile),
            ("Alpha.cs", "namespace Demo; public sealed class Alpha { }\n"),
            ("Beta.cs", "namespace Demo; public sealed class Beta { }\n"));
        CountingEstimator repositoryEstimator = new();
        ChangeEstimator estimator = new(repositoryEstimator);

        _ = await estimator.EstimateAsync(
            Input(
                initial,
                final,
                [Component("one", initial, middle), Component("two", middle, final)]),
            EstimationProfile.Implementation);

        Assert.Equal(3, repositoryEstimator.InvocationCount);
    }

    [Fact]
    public async Task RangeAllocationRemainsExactAndNonNegativeWithManyComponents()
    {
        ChangeState before = State(("Demo.csproj", ProjectFile));
        ChangeState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        ChangeComponentInput[] components = [.. Enumerable.Range(0, 128)
            .Select(index => Component($"component-{index:D3}", before, after))];

        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(
            Input(before, after, components),
            EstimationProfile.Implementation);

        Assert.Equal(128, report.Reconciliation.Components.Count);
        Assert.All(report.Reconciliation.Components, component =>
            Assert.True(component.AllocatedExpectedHours >= 0m));
        Assert.Equal(
            report.TotalEffort.Expected,
            report.Reconciliation.Components.Sum(component => component.AllocatedExpectedHours));
    }

    private static IEnumerable<ChangePathEvidence> Evidence(
        ChangeEstimateReport report,
        IEnumerable<WorkItem> items)
    {
        HashSet<string> ids = [.. items.SelectMany(item => item.EvidenceIds)];
        return report.Evidence.Paths.Where(path => ids.Contains(path.Id));
    }

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            Input(before, after, []),
            EstimationProfile.Implementation);

    private static ChangeEstimateInput Input(
        ChangeState before,
        ChangeState after,
        ChangeComponentInput[] components) => new()
        {
            RepositoryName = "in-memory-change",
            Selection = new ChangeSelection
            {
                Kind = components.Length == 0 ? ChangeSelectionKind.BaseHead : ChangeSelectionKind.Range,
                Base = Reference("base", before.ObjectId),
                Head = Reference("head", after.ObjectId),
                Range = components.Length == 0 ? null : "base..head",
            },
            OpenBaseAsync = before.OpenAsync,
            OpenHeadAsync = after.OpenAsync,
            Components = components,
        };

    private static ChangeComponentInput Component(
        string selector,
        ChangeState before,
        ChangeState after) => new()
        {
            Selector = selector,
            BaseObjectId = before.ObjectId,
            HeadObjectId = after.ObjectId,
            OpenBaseAsync = before.OpenAsync,
            OpenHeadAsync = after.OpenAsync,
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

    private sealed class CountingEstimator : IEstimator
    {
        private readonly SeedEstimator _inner = new();

        public int InvocationCount { get; private set; }

        public EstimateReport Estimate(
            RepositoryEvidence evidence,
            EstimationProfile profile,
            RateCard? rateCard = null)
        {
            InvocationCount++;
            return _inner.Estimate(evidence, profile, rateCard);
        }
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
