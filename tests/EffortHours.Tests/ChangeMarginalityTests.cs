using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeMarginalityTests
{
    [Fact]
    public async Task SmallCompatibilityUpgradeUsesOneMarginalBudgetPerChangedConcern()
    {
        ChangeState before = State(CompatibilityFiles(useUpdatedApi: false, updateLockfile: false));
        ChangeState after = State(CompatibilityFiles(useUpdatedApi: true, updateLockfile: true));
        ChangeState afterWithoutLockfileDelta = State(
            CompatibilityFiles(useUpdatedApi: true, updateLockfile: false));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        ChangeEstimateReport withoutLockfileDelta = await EstimateAsync(
            before,
            afterWithoutLockfileDelta);

        Assert.Equal("change-seed/0.4.0+seed-rules/0.3.0", report.EstimatorVersion);
        Assert.InRange(report.TotalEffort.Expected, 3m, 8m);
        Assert.InRange(report.WorkItems.Count, 4, 8);
        Assert.DoesNotContain(report.Categories, category => category.Category is
            EffortCategory.RepositoryAndSolutionSetup or
            EffortCategory.ArchitectureAndTechnicalDesign or
            EffortCategory.UiImplementationAndRepresentedUxDecisions or
            EffortCategory.PackagingDeploymentAndReleaseArtifacts);
        Assert.Single(report.WorkItems, item =>
            item.Estimator.Id == "change-rule:change-comprehension");
        Assert.Single(report.WorkItems, item =>
            item.Estimator.Id == "change-rule:change-validation");
        Assert.Single(report.WorkItems, item =>
            item.Estimator.Id == "change-rule:change-review");

        ChangePathEvidence lockfile = Assert.Single(report.Evidence.Paths, path =>
            path.Path == "package-lock.json");
        Assert.Equal(ChangePathClassification.Lockfile, lockfile.Classification);
        Assert.False(lockfile.Represented);
        Assert.Equal(withoutLockfileDelta.TotalEffort, report.TotalEffort);
        Assert.Equal(
            withoutLockfileDelta.Categories.Select(CategoryHours),
            report.Categories.Select(CategoryHours));
    }

    private static (EffortCategory Category, EffortRange Hours) CategoryHours(
        CategoryEstimate category) => (category.Category, category.Hours);

    private static Task<ChangeEstimateReport> EstimateAsync(
        ChangeState before,
        ChangeState after) => new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "synthetic-compatibility-upgrade",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.Evidence,
    };

    private static ChangeState State((string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private static (string Path, string Content)[] CompatibilityFiles(
        bool useUpdatedApi,
        bool updateLockfile) =>
    [
        (
            "package.json",
            useUpdatedApi
                ? "{\"name\":\"dashboard-shell\",\"private\":true,\"type\":\"module\",\"dependencies\":{\"react\":\"19.2.0\",\"chart-kit\":\"8.0.0\"}}\n"
                : "{\"name\":\"dashboard-shell\",\"private\":true,\"type\":\"module\",\"dependencies\":{\"react\":\"19.1.0\",\"chart-kit\":\"5.0.0\"}}\n"),
        (
            "packages/chart-panel/package.json",
            useUpdatedApi
                ? "{\"name\":\"chart-panel\",\"version\":\"0.0.2\",\"type\":\"module\",\"dependencies\":{\"chart-kit\":\"8.0.0\"}}\n"
                : "{\"name\":\"chart-panel\",\"version\":\"0.0.1\",\"type\":\"module\",\"dependencies\":{\"chart-kit\":\"5.0.0\"}}\n"),
        (
            "packages/chart-panel/src/chart-panel.tsx",
            useUpdatedApi
                ? "import { Chart, registerAdapters } from 'chart-kit';\nregisterAdapters();\nexport function ChartPanel({ values }: { values: number[] }) { return <Chart data={values} />; }\n"
                : "import { LegacyChart } from 'chart-kit';\nexport function ChartPanel({ values }: { values: number[] }) { return <LegacyChart data={values} />; }\n"),
        (
            "packages/chart-panel/src/chart-provider.ts",
            useUpdatedApi
                ? "import { Chart } from 'chart-kit';\nexport const chartProvider = Chart;\n"
                : "import { LegacyChartModule } from 'chart-kit';\nexport const chartProvider = LegacyChartModule;\n"),
        (
            "tsconfig.json",
            useUpdatedApi
                ? "{\"compilerOptions\":{\"target\":\"ES2022\",\"jsx\":\"react-jsx\",\"paths\":{\"chart-kit\":[\"./node_modules/chart-kit\"]}}}\n"
                : "{\"compilerOptions\":{\"target\":\"ES2022\",\"jsx\":\"react-jsx\"}}\n"),
        (
            "package-lock.json",
            updateLockfile
                ? "{\"lockfileVersion\":3,\"packages\":{\"node_modules/chart-kit\":{\"version\":\"8.0.0\"}}}\n"
                : "{\"lockfileVersion\":3,\"packages\":{\"node_modules/chart-kit\":{\"version\":\"5.0.0\"}}}\n"),
    ];

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
