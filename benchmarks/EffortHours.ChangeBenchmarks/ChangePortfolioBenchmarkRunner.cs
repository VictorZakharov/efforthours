using System.Diagnostics;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal sealed record ChangePortfolioBenchmarkExecution
{
    public required int SelectedChanges { get; init; }

    public required int CombinedSnapshotAnalyses { get; init; }

    public required int IsolatedManifestSnapshotAnalyses { get; init; }

    public required int IsolatedManifestInvocations { get; init; }

    public required int IsolatedManifestSelectedRows { get; init; }

    public required int UniqueIsolatedManifestChanges { get; init; }

    public required int IsolatedManifestObjectReaders { get; init; }

    public required TimeSpan CombinedElapsed { get; init; }

    public required TimeSpan CombinedCpu { get; init; }

    public required TimeSpan InitialCombinedWarmupElapsed { get; init; }

    public required TimeSpan InitialCombinedWarmupCpu { get; init; }

    public required TimeSpan IsolatedManifestElapsed { get; init; }

    public required TimeSpan IsolatedManifestCpu { get; init; }

    public required bool IsolatedManifestReportsEquivalent { get; init; }

    public required bool ManualBaselineEquivalent { get; init; }

    public required bool ReorderedReportBytesEquivalent { get; init; }

    public required bool RepositoryScopedSharedObject { get; init; }

    public required bool FullyOverlappingHeadsPreserved { get; init; }

    public required bool EmptyContributorPreserved { get; init; }

    public required bool PrivacyBoundaryPreserved { get; init; }

    public required decimal ExpectedEffort { get; init; }

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }

    public required ChangePortfolioExecutionStatistics IsolatedManifestStatistics { get; init; }

    public IReadOnlyList<ChangePortfolioPhaseTiming> CombinedPhaseTimings { get; init; } = [];
}

internal static class ChangePortfolioBenchmarkRunner
{
    public static async Task<ChangePortfolioBenchmarkExecution> RunAsync(
        GitPortfolioBenchmarkFixture fixture,
        CancellationToken cancellationToken)
    {
        CountingEstimator combinedEstimator = new();
        TimeSpan warmupCpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        Stopwatch warmupTimer = Stopwatch.StartNew();
        CombinedRun combined = await RunCombinedAsync(
            fixture.Manifest,
            fixture.RepositoryPaths,
            combinedEstimator,
            cancellationToken).ConfigureAwait(false);
        warmupTimer.Stop();
        TimeSpan warmupCpu = Process.GetCurrentProcess().TotalProcessorTime - warmupCpuBefore;

        ChangePortfolioAggregation aggregation = combined.Report.Aggregation ??
            throw new InvalidOperationException("Manifest benchmark report omitted aggregate attribution.");
        HashSet<string> measuredContributorIds = aggregation.Contributors
            .Where(contributor => !contributor.NoSelectedCommits)
            .Select(contributor => contributor.ContributorId)
            .ToHashSet(StringComparer.Ordinal);
        IsolatedManifestRun isolated = await RunIsolatedManifestsAsync(
            fixture,
            measuredContributorIds,
            cancellationToken)
            .ConfigureAwait(false);
        bool isolatedManifestReportsEquivalent = CombinedRowsEquivalent(combined, isolated.Reports);
        bool manualBaselineEquivalent = ManualBaselineEquivalent(combined, isolated.Reports);

        CountingEstimator reorderedEstimator = new();
        TimeSpan combinedCpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        Stopwatch combinedTimer = Stopwatch.StartNew();
        CombinedRun reordered = await RunCombinedAsync(
            fixture.ReorderedManifest(),
            fixture.RepositoryPaths,
            reorderedEstimator,
            cancellationToken).ConfigureAwait(false);
        combinedTimer.Stop();
        TimeSpan combinedCpu = Process.GetCurrentProcess().TotalProcessorTime - combinedCpuBefore;
        bool overlappingHeads = aggregation.Repositories.All(repository =>
        {
            ChangePortfolioHeadSummary shared = repository.Heads.Single(head => head.HeadId == "shared");
            ChangePortfolioHeadSummary overlap = repository.Heads.Single(
                head => head.HeadId == "fully-overlapping");
            return shared.ReachableSelectedCommitCount == overlap.ReachableSelectedCommitCount &&
                shared.SharedSelectedCommitCount == overlap.SharedSelectedCommitCount;
        });
        bool emptyContributor = aggregation.Contributors.Single(
            contributor => contributor.ContributorId == "contributor-empty").NoSelectedCommits;
        string combinedJson = combined.Json;
        bool privacy = fixture.Repositories.All(repository =>
                !combinedJson.Contains(repository.Path, StringComparison.OrdinalIgnoreCase)) &&
            fixture.Manifest.Contributors.SelectMany(contributor => contributor.Aliases).All(alias =>
                !combinedJson.Contains(alias, StringComparison.OrdinalIgnoreCase));

        return new ChangePortfolioBenchmarkExecution
        {
            SelectedChanges = reordered.Plan.Items.Count,
            CombinedSnapshotAnalyses = reorderedEstimator.InvocationCount,
            IsolatedManifestSnapshotAnalyses = isolated.SnapshotAnalyses,
            IsolatedManifestInvocations = isolated.Invocations,
            IsolatedManifestSelectedRows = isolated.SelectedRows,
            UniqueIsolatedManifestChanges = isolated.Reports.Count,
            IsolatedManifestObjectReaders = isolated.Statistics.ObjectDatabaseReaders,
            CombinedElapsed = combinedTimer.Elapsed,
            CombinedCpu = combinedCpu,
            InitialCombinedWarmupElapsed = warmupTimer.Elapsed,
            InitialCombinedWarmupCpu = warmupCpu,
            IsolatedManifestElapsed = isolated.Elapsed,
            IsolatedManifestCpu = isolated.Cpu,
            IsolatedManifestReportsEquivalent = isolatedManifestReportsEquivalent,
            ManualBaselineEquivalent = manualBaselineEquivalent,
            ReorderedReportBytesEquivalent = string.Equals(
                combined.Json,
                reordered.Json,
                StringComparison.Ordinal),
            RepositoryScopedSharedObject = combined.Plan.Items.Count(item =>
                string.Equals(
                    item.Plan.Selection.Head.ObjectId,
                    fixture.SharedObjectId,
                    StringComparison.Ordinal)) == fixture.Repositories.Count,
            FullyOverlappingHeadsPreserved = overlappingHeads,
            EmptyContributorPreserved = emptyContributor,
            PrivacyBoundaryPreserved = privacy,
            ExpectedEffort = reordered.Report.TotalEffort.Expected,
            Statistics = reordered.Estimate.Statistics,
            IsolatedManifestStatistics = isolated.Statistics,
            CombinedPhaseTimings = reordered.Telemetry.GetTimings(),
        };
    }

    private static async Task<CombinedRun> RunCombinedAsync(
        ChangeAuthorPeriodManifest manifest,
        IReadOnlyDictionary<string, string> repositoryPaths,
        CountingEstimator repositoryEstimator,
        CancellationToken cancellationToken)
    {
        string digest = ChangeAuthorPeriodManifestIdentity.ComputeDigest(manifest);
        GitAuthorPeriodManifestPortfolioPlan plan = await new GitPortfolioPlanner()
            .PlanAuthorPeriodManifestAsync(
                manifest,
                digest,
                repositoryPaths,
                cancellationToken).ConfigureAwait(false);
        ChangePortfolioEstimateBatch estimate = await new ChangeEstimator(repositoryEstimator)
            .EstimatePortfolioCandidatesWithStatisticsAsync(
                [.. plan.Items.Select(item => item.Plan)],
                EstimationProfile.Implementation,
                plan.ExecutionTelemetry,
                cancellationToken).ConfigureAwait(false);
        ChangePortfolioCandidate[] candidates = Candidates(plan, estimate.Reports);
        IReadOnlyList<Diagnostic> diagnostics =
            [.. plan.Diagnostics, estimate.Statistics.CreateDiagnostic()];
        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            plan.Selection,
            candidates,
            EstimationProfile.Implementation,
            planningDiagnostics: diagnostics,
            executionTelemetry: plan.ExecutionTelemetry);
        return new CombinedRun(
            plan,
            estimate,
            report,
            ContractJson.Serialize(report),
            diagnostics,
            plan.ExecutionTelemetry);
    }

    private static async Task<IsolatedManifestRun> RunIsolatedManifestsAsync(
        GitPortfolioBenchmarkFixture fixture,
        HashSet<string> measuredContributorIds,
        CancellationToken cancellationToken)
    {
        Dictionary<string, ChangeEstimateReport> reports = new(StringComparer.Ordinal);
        int invocations = 0;
        int selectedRows = 0;
        int snapshotAnalyses = 0;
        List<ChangePortfolioExecutionStatistics> statistics = [];
        TimeSpan cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        Stopwatch timer = Stopwatch.StartNew();
        foreach (ChangeAuthorPeriodManifestContributor contributor in fixture.Manifest.Contributors
            .Where(contributor => measuredContributorIds.Contains(contributor.Id))
            .OrderBy(contributor => contributor.Id, StringComparer.Ordinal))
        {
            invocations++;
            CountingEstimator repositoryEstimator = new();
            ChangeAuthorPeriodManifest isolatedManifest = fixture.Manifest with
            {
                Contributors = [contributor],
            };
            CombinedRun run = await RunCombinedAsync(
                isolatedManifest,
                fixture.RepositoryPaths,
                repositoryEstimator,
                cancellationToken).ConfigureAwait(false);
            selectedRows += run.Estimate.Reports.Count;
            snapshotAnalyses += repositoryEstimator.InvocationCount;
            statistics.Add(run.Estimate.Statistics);
            foreach ((GitAuthorPeriodManifestPortfolioItem item, int index) in
                run.Plan.Items.Select((item, index) => (item, index)))
            {
                ChangeEstimateReport report = run.Estimate.Reports[index];
                string key = Key(item.RepositoryId, report.Selection.Head.ObjectId);
                if (reports.TryGetValue(key, out ChangeEstimateReport? existing) &&
                    !string.Equals(
                        ContractJson.Serialize(existing),
                        ContractJson.Serialize(report),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Isolated manifest report '{key}' changed across equivalent selectors.");
                }

                reports.TryAdd(key, report);
            }
        }

        timer.Stop();
        TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        return new IsolatedManifestRun(
            reports,
            invocations,
            selectedRows,
            snapshotAnalyses,
            timer.Elapsed,
            cpu,
            CombineStatistics(statistics));
    }

    private static bool CombinedRowsEquivalent(
        CombinedRun combined,
        IReadOnlyDictionary<string, ChangeEstimateReport> independent) =>
        combined.Plan.Items.Select((item, index) => (item, index)).All(entry =>
            independent.TryGetValue(
                Key(entry.item.RepositoryId, entry.item.Plan.Selection.Head.ObjectId),
                out ChangeEstimateReport? report) &&
            string.Equals(
                ContractJson.Serialize(combined.Estimate.Reports[entry.index]),
                ContractJson.Serialize(report),
                StringComparison.Ordinal));

    private static bool ManualBaselineEquivalent(
        CombinedRun combined,
        IReadOnlyDictionary<string, ChangeEstimateReport> independent)
    {
        ChangeEstimateReport[] reports = [.. combined.Plan.Items.Select(item =>
            independent[Key(item.RepositoryId, item.Plan.Selection.Head.ObjectId)])];
        ChangePortfolioReport manual = ChangePortfolioReconciler.Reconcile(
            combined.Plan.Selection,
            Candidates(combined.Plan, reports),
            EstimationProfile.Implementation,
            planningDiagnostics: combined.Diagnostics);
        return string.Equals(
            combined.Json,
            ContractJson.Serialize(manual),
            StringComparison.Ordinal);
    }

    private static ChangePortfolioCandidate[] Candidates(
        GitAuthorPeriodManifestPortfolioPlan plan,
        IReadOnlyList<ChangeEstimateReport> reports) => [.. plan.Items.Select((item, index) =>
            new ChangePortfolioCandidate
            {
                RepositoryId = item.RepositoryId,
                SelectorId = item.SelectorId,
                Report = reports[index],
                Attribution = item.Attribution,
            })];

    private static ChangePortfolioExecutionStatistics CombineStatistics(
        IReadOnlyList<ChangePortfolioExecutionStatistics> statistics) => new()
        {
            RepositoryCount = statistics.Sum(value => value.RepositoryCount),
            MaximumActiveRepositories = statistics.Max(value => value.MaximumActiveRepositories),
            MaximumConcurrentFileAnalysesPerRepository = statistics.Max(
                value => value.MaximumConcurrentFileAnalysesPerRepository),
            SnapshotAnalysisRequests = statistics.Sum(value => value.SnapshotAnalysisRequests),
            SnapshotAnalysisHits = statistics.Sum(value => value.SnapshotAnalysisHits),
            UniqueSnapshotAnalysisKeys = statistics.Sum(value => value.UniqueSnapshotAnalysisKeys),
            SnapshotAnalysisRevisitMisses = statistics.Sum(value => value.SnapshotAnalysisRevisitMisses),
            AnalysisArtifactRequests = statistics.Sum(value => value.AnalysisArtifactRequests),
            AnalysisArtifactHits = statistics.Sum(value => value.AnalysisArtifactHits),
            UniqueAnalysisArtifactKeys = statistics.Sum(value => value.UniqueAnalysisArtifactKeys),
            AnalysisArtifactRevisitMisses = statistics.Sum(value => value.AnalysisArtifactRevisitMisses),
            AnalysisArtifactEvictions = statistics.Sum(value => value.AnalysisArtifactEvictions),
            PeakRetainedAnalysisArtifacts = statistics.Max(value => value.PeakRetainedAnalysisArtifacts),
            SnapshotAnalysisEvictions = statistics.Sum(value => value.SnapshotAnalysisEvictions),
            PeakRetainedSnapshotAnalyses = statistics.Max(value => value.PeakRetainedSnapshotAnalyses),
            SnapshotInventoryRequests = statistics.Sum(value => value.SnapshotInventoryRequests),
            SnapshotInventoryHits = statistics.Sum(value => value.SnapshotInventoryHits),
            UniqueSnapshotInventoryObjects = statistics.Sum(value => value.UniqueSnapshotInventoryObjects),
            SnapshotInventoryRevisitMisses = statistics.Sum(value => value.SnapshotInventoryRevisitMisses),
            FullSnapshotInventoryLoads = statistics.Sum(value => value.FullSnapshotInventoryLoads),
            IncrementalSnapshotInventoryLoads = statistics.Sum(value => value.IncrementalSnapshotInventoryLoads),
            BatchedIncrementalSnapshotInventoryLoads = statistics.Sum(
                value => value.BatchedIncrementalSnapshotInventoryLoads),
            SnapshotInventoryEvictions = statistics.Sum(value => value.SnapshotInventoryEvictions),
            PeakRetainedSnapshotInventories = statistics.Max(value => value.PeakRetainedSnapshotInventories),
            PeakRetainedSnapshotInventoryRoots = statistics.Max(
                value => value.PeakRetainedSnapshotInventoryRoots),
            ObjectDatabaseReaders = statistics.Sum(value => value.ObjectDatabaseReaders),
            ObjectMetadataReaders = statistics.Sum(value => value.ObjectMetadataReaders),
            ObjectMetadataRequests = statistics.Sum(value => value.ObjectMetadataRequests),
            ObjectMetadataCacheHits = statistics.Sum(value => value.ObjectMetadataCacheHits),
            UniqueObjectMetadataObjects = statistics.Sum(value => value.UniqueObjectMetadataObjects),
            ObjectMetadataCacheEvictions = statistics.Sum(value => value.ObjectMetadataCacheEvictions),
            PeakCachedObjectMetadataLengthsPerRepository = statistics.Max(
                value => value.PeakCachedObjectMetadataLengthsPerRepository),
            BlobRequests = statistics.Sum(value => value.BlobRequests),
            BlobCacheHits = statistics.Sum(value => value.BlobCacheHits),
            BlobCacheEvictions = statistics.Sum(value => value.BlobCacheEvictions),
            UniqueBlobObjects = statistics.Sum(value => value.UniqueBlobObjects),
            BlobRequestedBytes = statistics.Sum(value => value.BlobRequestedBytes),
            BlobCacheHitBytes = statistics.Sum(value => value.BlobCacheHitBytes),
            BlobReadBytes = statistics.Sum(value => value.BlobReadBytes),
            UniqueBlobBytes = statistics.Sum(value => value.UniqueBlobBytes),
            RetainedBlobBytesPerRepository = statistics.Max(value => value.RetainedBlobBytesPerRepository),
            PeakCachedBlobBytesPerRepository = statistics.Max(value => value.PeakCachedBlobBytesPerRepository),
            SnapshotAnalysisRetentionLimit = statistics.Max(value => value.SnapshotAnalysisRetentionLimit),
            AnalysisArtifactRetentionLimit = statistics.Max(value => value.AnalysisArtifactRetentionLimit),
            SnapshotInventoryRetentionLimit = statistics.Max(value => value.SnapshotInventoryRetentionLimit),
            SnapshotInventoryRootRetentionLimit = statistics.Max(
                value => value.SnapshotInventoryRootRetentionLimit),
            SnapshotDeltaBatchOutputByteLimit = statistics.Max(
                value => value.SnapshotDeltaBatchOutputByteLimit),
            BlobCacheByteLimitPerRepository = statistics.Max(value => value.BlobCacheByteLimitPerRepository),
            ObjectMetadataCacheEntryLimitPerRepository = statistics.Max(
                value => value.ObjectMetadataCacheEntryLimitPerRepository),
        };

    private static string Key(string repositoryId, string objectId) => $"{repositoryId}:{objectId}";

    private sealed record CombinedRun(
        GitAuthorPeriodManifestPortfolioPlan Plan,
        ChangePortfolioEstimateBatch Estimate,
        ChangePortfolioReport Report,
        string Json,
        IReadOnlyList<Diagnostic> Diagnostics,
        ChangePortfolioExecutionTelemetry Telemetry);

    private sealed record IsolatedManifestRun(
        IReadOnlyDictionary<string, ChangeEstimateReport> Reports,
        int Invocations,
        int SelectedRows,
        int SnapshotAnalyses,
        TimeSpan Elapsed,
        TimeSpan Cpu,
        ChangePortfolioExecutionStatistics Statistics);
}
