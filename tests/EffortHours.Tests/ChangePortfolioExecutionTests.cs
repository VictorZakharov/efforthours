using EffortHours.Change;
using EffortHours.Cli;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioCommandTests
{
    [Fact]
    public void ExecutionTelemetryReportsEachStartedPhaseOnceAndOnlyReturnsActivePhases()
    {
        List<string> started = [];
        List<ChangePortfolioProgress> progress = [];
        ChangePortfolioExecutionTelemetry telemetry = new(started.Add, progress.Add);

        using (telemetry.Measure(ChangePortfolioExecutionPhases.StaticAnalysis))
        {
        }
        using (telemetry.Measure(ChangePortfolioExecutionPhases.StaticAnalysis))
        {
        }
        telemetry.Start(ChangePortfolioExecutionPhases.Selection);
        telemetry.Add(ChangePortfolioExecutionPhases.Allocation, TimeSpan.FromMilliseconds(2));
        telemetry.ReportProgress(
            ChangePortfolioExecutionPhases.StaticAnalysis,
            processedUnits: 16,
            totalUnits: 32,
            analysisCacheRequests: 20,
            analysisCacheHits: 4);

        Assert.Equal(
            [
                ChangePortfolioExecutionPhases.StaticAnalysis,
                ChangePortfolioExecutionPhases.Selection,
                ChangePortfolioExecutionPhases.Allocation,
            ],
            started);
        Assert.Equal(
            [
                ChangePortfolioExecutionPhases.Selection,
                ChangePortfolioExecutionPhases.StaticAnalysis,
                ChangePortfolioExecutionPhases.Allocation,
            ],
            telemetry.GetTimings().Select(timing => timing.Phase));
        Assert.DoesNotContain(
            telemetry.GetTimings(),
            timing => timing.Phase == ChangePortfolioExecutionPhases.ManifestValidation);
        ChangePortfolioProgress reported = Assert.Single(progress);
        Assert.Equal(16, reported.ProcessedUnits);
        Assert.Equal(32, reported.TotalUnits);
        Assert.Equal(4, reported.AnalysisCacheHits);
        Assert.Equal(ChangePortfolioExecutionPhases.StaticAnalysis, telemetry.GetLastProgress()!.Phase);
    }

    [Fact]
    public void PortfolioSessionRetainsParentMetadataForTheDefaultHistoryBoundary()
    {
        Assert.Equal(
            GitPortfolioPlannerOptions.DefaultMaximumHistoryCommits,
            GitSnapshotSession.MaximumRememberedFirstParents);
        Assert.Equal(
            ChangeAuthorPeriodManifestLimits.MaximumRepositories *
                GitPortfolioPlannerOptions.DefaultMaximumHistoryCommits,
            GitPortfolioPlannerOptions.DefaultMaximumSelectedItems);
        Assert.True(GitSnapshotSession.MaximumRememberedFirstParents > 1_700);
        Assert.True(GitPortfolioPlannerOptions.DefaultMaximumSelectedItems > 1_700);
        Assert.Equal(64 * 1024 * 1024, GitSnapshotDeltaBatch.MaximumBatchOutputBytes);
    }

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
    public async Task CancelledManifestCommandEmitsPrivacySafeLastPhaseProgress()
    {
        ChangePortfolioCommand command = new(
            new ChangeEstimator(),
            (_, _, _, _, _) => throw new InvalidOperationException("Pull-request planning was not expected."),
            (_, _, _, _) => throw new InvalidOperationException("Author planning was not expected."),
            (_, _) => throw new InvalidOperationException("PR manifest loading was not expected."),
            (_, telemetry, cancellationToken) =>
            {
                telemetry.Start(ChangePortfolioExecutionPhases.StaticAnalysis);
                telemetry.ReportProgress(
                    ChangePortfolioExecutionPhases.StaticAnalysis,
                    processedUnits: 16,
                    totalUnits: 32,
                    analysisCacheRequests: 20,
                    analysisCacheHits: 4);
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Cancellation was not observed.");
            });
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        StringWriter stderr = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteAsync(
            ["--author-period-manifest", "portfolio.json", "--no-rate"],
            new StringWriter(),
            stderr,
            cancellation.Token));

        Assert.Contains("phase=static-analysis", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("processed=16/32", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("remaining=16", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("analysis-cache=4/20", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("observed-peak-working-set=", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("portfolio.json", stderr.ToString(), StringComparison.Ordinal);
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
    public async Task PortfolioCandidateBatchScopesEqualObjectsAndBoundsRepositoryConcurrency()
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
        List<ChangePortfolioProgress> progress = [];
        ChangePortfolioExecutionTelemetry telemetry = new(progressReported: progress.Add);

        ChangePortfolioEstimateBatch batch =
            await new ChangeEstimator(repositoryEstimator)
                .EstimatePortfolioCandidatesWithStatisticsAsync(
                    [first, second],
                    EstimationProfile.Implementation,
                    telemetry);

        Assert.Equal(4, repositoryEstimator.InvocationCount);
        Assert.Equal(2, batch.Statistics.RepositoryCount);
        Assert.Equal(2, batch.Statistics.MaximumActiveRepositories);
        Assert.Equal(0, batch.Statistics.SnapshotAnalysisHits);
        Assert.Equal([1, 2], progress.Select(item => item.ProcessedUnits));
        Assert.Equal(4, progress[^1].AnalysisCacheRequests);
        Assert.All(progress, item =>
            Assert.InRange(item.AnalysisCacheHits, 0, item.AnalysisCacheRequests));
    }

    [Fact]
    public async Task ThreadSafeRepositoryEstimatorRunsIndependentSnapshotsConcurrently()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        GitChangePlan[] plans =
        [
            CommitPlan("first", before, after) with { RepositoryPath = "repository-a" },
            CommitPlan("second", before, after) with { RepositoryPath = "repository-b" },
        ];
        using ConcurrentProbeEstimator repositoryEstimator = new();

        _ = await new ChangeEstimator(repositoryEstimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(
                plans,
                EstimationProfile.Implementation);

        Assert.True(repositoryEstimator.MaximumActive >= 2);
    }

    [Fact]
    public async Task PortfolioProducerOpensNextPlanWhilePriorPlanIsAnalyzed()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState firstAfter = State(
            ("Demo.csproj", ProjectFile),
            ("First.cs", "namespace Demo; public sealed class First { }\n"));
        SnapshotState secondAfter = State(
            ("Demo.csproj", ProjectFile),
            ("Second.cs", "namespace Demo; public sealed class Second { }\n"));
        using ManualResetEventSlim analysisStarted = new();
        using ManualResetEventSlim secondPlanOpened = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        GitChangePlan first = CommitPlan("first", before, firstAfter);
        GitChangePlan second = CommitPlan("second", before, secondAfter) with
        {
            OpenBaseAsync = async cancellationToken =>
            {
                if (!analysisStarted.Wait(TimeSpan.FromSeconds(5), cancellationToken))
                {
                    throw new TimeoutException(
                        "The prior plan was not analyzed while the next plan was produced.");
                }

                secondPlanOpened.Set();
                return await before.OpenAsync(cancellationToken).ConfigureAwait(false);
            },
        };
        ProducerOverlapEstimator repositoryEstimator = new(
            analysisStarted,
            secondPlanOpened);

        ChangePortfolioEstimateBatch batch = await new ChangeEstimator(repositoryEstimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(
                [first, second],
                EstimationProfile.Implementation,
                cancellationToken: timeout.Token);

        Assert.Equal(2, batch.Reports.Count);
        Assert.True(analysisStarted.IsSet);
        Assert.True(secondPlanOpened.IsSet);
    }

    [Fact]
    public async Task UnmarkedRepositoryEstimatorRemainsSerialized()
    {
        SnapshotState before = State(("Demo.csproj", ProjectFile));
        SnapshotState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        GitChangePlan[] plans =
        [
            CommitPlan("first", before, after) with { RepositoryPath = "repository-a" },
            CommitPlan("second", before, after) with { RepositoryPath = "repository-b" },
        ];
        SerializedProbeEstimator repositoryEstimator = new();

        _ = await new ChangeEstimator(repositoryEstimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(
                plans,
                EstimationProfile.Implementation);

        Assert.Equal(1, repositoryEstimator.MaximumActive);
    }

    private sealed class ConcurrentProbeEstimator : IThreadSafeEstimator, IDisposable
    {
        private readonly SeedEstimator _inner = new();
        private readonly ManualResetEventSlim _overlap = new();
        private int _active;
        private int _maximumActive;

        public int MaximumActive => Volatile.Read(ref _maximumActive);

        public EstimateReport Estimate(
            RepositoryEvidence evidence,
            EstimationProfile profile,
            RateCard? rateCard = null)
        {
            int active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            if (active >= 2)
            {
                _overlap.Set();
            }

            try
            {
                if (!_overlap.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException(
                        "Independent thread-safe estimator calls did not overlap.");
                }

                return _inner.Estimate(evidence, profile, rateCard);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public void Dispose() => _overlap.Dispose();

        private void UpdateMaximum(int active)
        {
            int observed;
            while (active > (observed = Volatile.Read(ref _maximumActive)) &&
                Interlocked.CompareExchange(ref _maximumActive, active, observed) != observed)
            {
            }
        }
    }

    private sealed class SerializedProbeEstimator : IEstimator
    {
        private readonly SeedEstimator _inner = new();
        private int _active;
        private int _maximumActive;

        public int MaximumActive => Volatile.Read(ref _maximumActive);

        public EstimateReport Estimate(
            RepositoryEvidence evidence,
            EstimationProfile profile,
            RateCard? rateCard = null)
        {
            int active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            try
            {
                Thread.Sleep(10);
                return _inner.Estimate(evidence, profile, rateCard);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdateMaximum(int active)
        {
            int observed;
            while (active > (observed = Volatile.Read(ref _maximumActive)) &&
                Interlocked.CompareExchange(ref _maximumActive, active, observed) != observed)
            {
            }
        }
    }

    private sealed class ProducerOverlapEstimator(
        ManualResetEventSlim analysisStarted,
        ManualResetEventSlim secondPlanOpened) : IThreadSafeEstimator
    {
        private readonly SeedEstimator _inner = new();
        private int _invocations;

        public EstimateReport Estimate(
            RepositoryEvidence evidence,
            EstimationProfile profile,
            RateCard? rateCard = null)
        {
            if (Interlocked.Increment(ref _invocations) == 1)
            {
                analysisStarted.Set();
                if (!secondPlanOpened.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException(
                        "The next plan was not produced while the prior plan was analyzed.");
                }
            }

            return _inner.Estimate(evidence, profile, rateCard);
        }
    }
}
