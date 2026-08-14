using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioCommandTests
{
    [Fact]
    public async Task PortfolioCandidateBatchReusesNonAdjacentSnapshotsWithinBound()
    {
        SnapshotState shared = State(("Demo.csproj", ProjectFile));
        SnapshotState first = State(
            ("Demo.csproj", ProjectFile),
            ("First.cs", "namespace Demo; public sealed class First { }\n"));
        SnapshotState unrelatedBase = State(
            ("Demo.csproj", ProjectFile),
            ("Unrelated.cs", "namespace Demo; public sealed class Unrelated { }\n"));
        SnapshotState unrelatedHead = State(
            ("Demo.csproj", ProjectFile),
            ("Unrelated.cs", "namespace Demo; public sealed class Unrelated { public int Value => 1; }\n"));
        SnapshotState final = State(
            ("Demo.csproj", ProjectFile),
            ("Final.cs", "namespace Demo; public sealed class Final { }\n"));
        GitChangePlan[] plans =
        [
            CommitPlan("first", shared, first),
            CommitPlan("unrelated", unrelatedBase, unrelatedHead),
            CommitPlan("final", shared, final),
        ];
        CountingEstimator repositoryEstimator = new();

        ChangePortfolioEstimateBatch batch =
            await new ChangeEstimator(repositoryEstimator)
                .EstimatePortfolioCandidatesWithStatisticsAsync(
                    plans,
                    EstimationProfile.Implementation);

        Assert.Equal(5, repositoryEstimator.InvocationCount);
        Assert.Equal(6, batch.Statistics.SnapshotAnalysisRequests);
        Assert.Equal(1, batch.Statistics.SnapshotAnalysisHits);
        Assert.Equal(5, batch.Statistics.PeakRetainedSnapshotAnalyses);
        Assert.Equal(
            ChangeEstimator.PortfolioSnapshotAnalysisRetentionLimit,
            batch.Statistics.SnapshotAnalysisRetentionLimit);
    }

    [Fact]
    public async Task PortfolioCandidateBatchEvictsSnapshotAnalysesAtRetentionLimit()
    {
        GitChangePlan[] plans = [.. Enumerable.Range(0, 10).Select(index =>
            CommitPlan(
                $"commit-{index}",
                State(
                    ("Demo.csproj", ProjectFile),
                    ($"Before{index}.cs", $"namespace Demo; public sealed class Before{index} {{ }}\n")),
                State(
                    ("Demo.csproj", ProjectFile),
                    ($"After{index}.cs", $"namespace Demo; public sealed class After{index} {{ }}\n"))))];
        CountingEstimator repositoryEstimator = new();

        ChangePortfolioEstimateBatch batch =
            await new ChangeEstimator(repositoryEstimator)
                .EstimatePortfolioCandidatesWithStatisticsAsync(
                    plans,
                    EstimationProfile.Implementation);

        Assert.Equal(20, repositoryEstimator.InvocationCount);
        Assert.Equal(20, batch.Statistics.SnapshotAnalysisRequests);
        Assert.Equal(0, batch.Statistics.SnapshotAnalysisHits);
        Assert.Equal(4, batch.Statistics.SnapshotAnalysisEvictions);
        Assert.Equal(
            ChangeEstimator.PortfolioSnapshotAnalysisRetentionLimit,
            batch.Statistics.PeakRetainedSnapshotAnalyses);
    }

    [Fact]
    public async Task PortfolioCandidateCancellationDisposesRepositorySessionBeforeOpeningSnapshots()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        GitSnapshotSession session = new(
            "virtual-repository",
            (_, objectId, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return string.Equals(objectId, before.ObjectId, StringComparison.Ordinal)
                    ? before.OpenAsync(cancellationToken)
                    : after.OpenAsync(cancellationToken);
            });
        GitChangePlan plan = CommitPlan("cancelled", before, after) with
        {
            SnapshotSession = session,
        };
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChangeEstimator().EstimatePortfolioCandidatesAsync(
                [plan],
                EstimationProfile.Implementation,
                cancellation.Token));

        Assert.True(session.IsDisposed);
    }

    [Fact]
    public async Task IndividualPortfolioItemEstimateDoesNotConsumeSiblingSession()
    {
        SnapshotState shared = State(("Demo.csproj", ProjectFile));
        SnapshotState first = State(
            ("Demo.csproj", ProjectFile),
            ("First.cs", "namespace Demo; public sealed class First { }\n"));
        SnapshotState second = State(
            ("Demo.csproj", ProjectFile),
            ("Second.cs", "namespace Demo; public sealed class Second { }\n"));
        Dictionary<string, SnapshotState> snapshots = new(StringComparer.Ordinal)
        {
            [shared.ObjectId] = shared,
            [first.ObjectId] = first,
            [second.ObjectId] = second,
        };
        GitSnapshotSession session = new(
            "virtual-repository",
            (_, objectId, cancellationToken) => snapshots[objectId].OpenAsync(cancellationToken));
        GitChangePlan firstPlan = CommitPlan("first", shared, first) with
        {
            SnapshotSession = session,
        };
        GitChangePlan secondPlan = CommitPlan("second", shared, second) with
        {
            SnapshotSession = session,
        };

        _ = await new ChangeEstimator().EstimateAsync(
            firstPlan,
            EstimationProfile.Implementation);
        Assert.False(session.IsDisposed);

        ChangePortfolioEstimateBatch sibling =
            await new ChangeEstimator().EstimatePortfolioCandidatesWithStatisticsAsync(
                [secondPlan],
                EstimationProfile.Implementation);

        Assert.Single(sibling.Reports);
        Assert.True(session.IsDisposed);
    }

    [Fact]
    public async Task PortfolioCandidateBatchScopesEqualObjectsByRepositoryAndRunsOneAtATime()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        GitChangePlan first = CommitPlan("first", before, after) with
        {
            RepositoryPath = "repository-a",
        };
        GitChangePlan second = CommitPlan("second", before, after) with
        {
            RepositoryPath = "repository-b",
        };
        CountingEstimator repositoryEstimator = new();

        ChangePortfolioEstimateBatch batch =
            await new ChangeEstimator(repositoryEstimator)
                .EstimatePortfolioCandidatesWithStatisticsAsync(
                    [first, second],
                    EstimationProfile.Implementation);

        Assert.Equal(4, repositoryEstimator.InvocationCount);
        Assert.Equal(2, batch.Statistics.RepositoryCount);
        Assert.Equal(1, batch.Statistics.MaximumActiveRepositories);
        Assert.Equal(0, batch.Statistics.SnapshotAnalysisHits);
    }
}
