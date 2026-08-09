using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeSafeguardTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "<ImplicitUsings>enable</ImplicitUsings>" +
        "</PropertyGroup></Project>";

    private static readonly EffortCategory[] SpecializedCategories =
    [
        EffortCategory.DataModelingPersistenceAndMigrations,
        EffortCategory.ExternalIntegrationsAndProtocols,
        EffortCategory.CiCdAndInfrastructureAsCode,
        EffortCategory.PackagingDeploymentAndReleaseArtifacts,
    ];

    [Fact]
    public async Task MigrationIntegrationAndDeliveryChangesStayInTheirIntendedCategories()
    {
        ChangeState baseline = State(("Demo.csproj", ProjectFile));
        ChangeEstimateReport migration = await EstimateAsync(
            baseline,
            State(
                ("Demo.csproj", ProjectFile),
                (
                    "Migrations/AddOrders.cs",
                    "using Microsoft.EntityFrameworkCore.Migrations; namespace Demo; " +
                    "public sealed class AddOrders : Migration { }\n")));
        ChangeEstimateReport integration = await EstimateAsync(
            baseline,
            State(
                ("Demo.csproj", ProjectFile),
                (
                    "RemoteStatusClient.cs",
                    "namespace Demo; public sealed class RemoteStatusClient(HttpClient client) " +
                    "{ public Task<HttpResponseMessage> SendAsync(CancellationToken token) " +
                    "=> client.SendAsync(new HttpRequestMessage(), token); }\n")));
        ChangeEstimateReport continuousIntegration = await EstimateAsync(
            baseline,
            State(
                ("Demo.csproj", ProjectFile),
                (".github/workflows/ci.yml", "name: ci\non: [push]\njobs: { test: { runs-on: ubuntu-latest } }\n")));
        ChangeEstimateReport containerDelivery = await EstimateAsync(
            baseline,
            State(
                ("Demo.csproj", ProjectFile),
                ("Dockerfile", "FROM mcr.microsoft.com/dotnet/runtime:10.0\nCOPY . /app\n")));

        AssertSpecializedCategory(
            migration,
            EffortCategory.DataModelingPersistenceAndMigrations);
        AssertSpecializedCategory(
            integration,
            EffortCategory.ExternalIntegrationsAndProtocols);
        AssertSpecializedCategory(
            continuousIntegration,
            EffortCategory.CiCdAndInfrastructureAsCode);
        AssertSpecializedCategory(
            containerDelivery,
            EffortCategory.PackagingDeploymentAndReleaseArtifacts);
    }

    [Fact]
    public async Task MigrationRemovalIsPositiveBoundedAndTraceableSimplificationWork()
    {
        ChangeState before = State(
            ("Demo.csproj", ProjectFile),
            (
                "Migrations/RemoveLegacy.cs",
                "using Microsoft.EntityFrameworkCore.Migrations; namespace Demo; " +
                "public sealed class RemoveLegacy : Migration { }\n"));
        ChangeState after = State(("Demo.csproj", ProjectFile));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        CategoryEstimate category = Assert.Single(report.Categories, candidate =>
            candidate.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        WorkItem[] removalItems =
        [
            .. report.WorkItems.Where(item =>
                item.Category == EffortCategory.DataModelingPersistenceAndMigrations),
        ];

        AssertRange(category.Hours);
        Assert.NotEmpty(removalItems);
        Assert.All(removalItems, item =>
        {
            Assert.StartsWith("change-rule:capability-removal", item.Estimator.Id, StringComparison.Ordinal);
            Assert.NotEmpty(item.EvidenceIds);
            Assert.Contains("removal", item.Reason, StringComparison.OrdinalIgnoreCase);
        });
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);
        Assert.Equal(ChangePathStatus.Removed, path.Status);
        Assert.True(path.Represented);
        Assert.Contains(removalItems.SelectMany(item => item.EvidenceIds), id => id == path.Id);
    }

    [Fact]
    public async Task PreCancelledEstimateDoesNotOpenSnapshots()
    {
        ChangeState before = State(("Demo.csproj", ProjectFile));
        ChangeState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        int openCount = 0;
        ChangeEstimateInput input = Input(before, after) with
        {
            OpenBaseAsync = token =>
            {
                openCount++;
                return before.OpenAsync(token);
            },
            OpenHeadAsync = token =>
            {
                openCount++;
                return after.OpenAsync(token);
            },
        };
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChangeEstimator().EstimateAsync(
                input,
                EstimationProfile.Implementation,
                cancellationToken: cancellation.Token));

        Assert.Equal(0, openCount);
    }

    [Fact]
    public async Task CancellationInterruptsSnapshotOpeningPromptly()
    {
        ChangeState before = State(("Demo.csproj", ProjectFile));
        ChangeState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        TaskCompletionSource enteredHeadFactory = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ChangeEstimateInput input = Input(before, after) with
        {
            OpenHeadAsync = async token =>
            {
                enteredHeadFactory.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                throw new InvalidOperationException("The cancellation token was ignored.");
            },
        };
        using CancellationTokenSource cancellation = new();
        Task<ChangeEstimateReport> estimate = new ChangeEstimator().EstimateAsync(
            input,
            EstimationProfile.Implementation,
            cancellationToken: cancellation.Token);
        await enteredHeadFactory.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await estimate.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    private static void AssertSpecializedCategory(
        ChangeEstimateReport report,
        EffortCategory expected)
    {
        CategoryEstimate category = Assert.Single(
            report.Categories,
            candidate => candidate.Category == expected);
        AssertRange(category.Hours);
        Assert.Contains(report.WorkItems, item => item.Category == expected);
        Assert.All(
            SpecializedCategories.Where(categoryName => categoryName != expected),
            categoryName => Assert.DoesNotContain(
                report.Categories,
                candidate => candidate.Category == categoryName));
        Assert.All(report.WorkItems, item =>
        {
            AssertRange(item.Hours);
            Assert.NotEmpty(item.EvidenceIds);
        });
    }

    private static void AssertRange(EffortRange range)
    {
        Assert.True(range.Low > 0m);
        Assert.True(range.Low <= range.Expected);
        Assert.True(range.Expected <= range.High);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(
        ChangeState before,
        ChangeState after) => new ChangeEstimator().EstimateAsync(
            Input(before, after),
            EstimationProfile.Implementation);

    private static ChangeEstimateInput Input(ChangeState before, ChangeState after) => new()
    {
        RepositoryName = "in-memory-change-safeguards",
        Selection = new ChangeSelection
        {
            Kind = ChangeSelectionKind.BaseHead,
            Base = Reference("base", before.ObjectId),
            Head = Reference("head", after.ObjectId),
        },
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

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
