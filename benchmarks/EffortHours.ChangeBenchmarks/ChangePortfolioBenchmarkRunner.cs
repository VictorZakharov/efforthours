using System.Diagnostics;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal sealed record ChangePortfolioBenchmarkExecution
{
    public required int SelectedChanges { get; init; }

    public required int CombinedSnapshotAnalyses { get; init; }

    public required int IndependentSnapshotAnalyses { get; init; }

    public required int IndependentInvocations { get; init; }

    public required int EmptyIndependentInvocations { get; init; }

    public required int IndependentSelectedRows { get; init; }

    public required int UniqueIndependentChanges { get; init; }

    public required int IndependentObjectReaders { get; init; }

    public required TimeSpan CombinedElapsed { get; init; }

    public required TimeSpan CombinedCpu { get; init; }

    public required TimeSpan IndependentElapsed { get; init; }

    public required TimeSpan IndependentCpu { get; init; }

    public required bool IndependentReportsEquivalent { get; init; }

    public required bool ManualBaselineEquivalent { get; init; }

    public required bool ReorderedReportBytesEquivalent { get; init; }

    public required bool RepositoryScopedSharedObject { get; init; }

    public required bool FullyOverlappingHeadsPreserved { get; init; }

    public required bool EmptyContributorPreserved { get; init; }

    public required bool PrivacyBoundaryPreserved { get; init; }

    public required decimal ExpectedEffort { get; init; }

    public required ChangePortfolioExecutionStatistics Statistics { get; init; }
}

internal static class ChangePortfolioBenchmarkRunner
{
    public static async Task<ChangePortfolioBenchmarkExecution> RunAsync(
        GitPortfolioBenchmarkFixture fixture,
        CancellationToken cancellationToken)
    {
        CountingEstimator combinedEstimator = new();
        TimeSpan combinedCpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        Stopwatch combinedTimer = Stopwatch.StartNew();
        CombinedRun combined = await RunCombinedAsync(
            fixture.Manifest,
            fixture.RepositoryPaths,
            combinedEstimator,
            cancellationToken).ConfigureAwait(false);
        combinedTimer.Stop();
        TimeSpan combinedCpu = Process.GetCurrentProcess().TotalProcessorTime - combinedCpuBefore;

        IndependentRun independent = await RunIndependentAsync(fixture, cancellationToken)
            .ConfigureAwait(false);
        bool independentReportsEquivalent = CombinedRowsEquivalent(combined, independent.Reports);
        bool manualBaselineEquivalent = ManualBaselineEquivalent(combined, independent.Reports);

        CountingEstimator reorderedEstimator = new();
        CombinedRun reordered = await RunCombinedAsync(
            fixture.ReorderedManifest(),
            fixture.RepositoryPaths,
            reorderedEstimator,
            cancellationToken).ConfigureAwait(false);
        ChangePortfolioAggregation aggregation = combined.Report.Aggregation ??
            throw new InvalidOperationException("Manifest benchmark report omitted aggregate attribution.");
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
            SelectedChanges = combined.Plan.Items.Count,
            CombinedSnapshotAnalyses = combinedEstimator.InvocationCount,
            IndependentSnapshotAnalyses = independent.SnapshotAnalyses,
            IndependentInvocations = independent.Invocations,
            EmptyIndependentInvocations = independent.EmptyInvocations,
            IndependentSelectedRows = independent.SelectedRows,
            UniqueIndependentChanges = independent.Reports.Count,
            IndependentObjectReaders = independent.ObjectReaders,
            CombinedElapsed = combinedTimer.Elapsed,
            CombinedCpu = combinedCpu,
            IndependentElapsed = independent.Elapsed,
            IndependentCpu = independent.Cpu,
            IndependentReportsEquivalent = independentReportsEquivalent,
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
            ExpectedEffort = combined.Report.TotalEffort.Expected,
            Statistics = combined.Estimate.Statistics,
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
                cancellationToken).ConfigureAwait(false);
        ChangePortfolioCandidate[] candidates = Candidates(plan, estimate.Reports);
        IReadOnlyList<Diagnostic> diagnostics =
            [.. plan.Diagnostics, estimate.Statistics.CreateDiagnostic()];
        ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
            plan.Selection,
            candidates,
            EstimationProfile.Implementation,
            planningDiagnostics: diagnostics);
        return new CombinedRun(
            plan,
            estimate,
            report,
            ContractJson.Serialize(report),
            diagnostics);
    }

    private static async Task<IndependentRun> RunIndependentAsync(
        GitPortfolioBenchmarkFixture fixture,
        CancellationToken cancellationToken)
    {
        CountingEstimator repositoryEstimator = new();
        Dictionary<string, ChangeEstimateReport> reports = new(StringComparer.Ordinal);
        int invocations = 0;
        int emptyInvocations = 0;
        int selectedRows = 0;
        int objectReaders = 0;
        TimeSpan cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        Stopwatch timer = Stopwatch.StartNew();
        foreach (PortfolioBenchmarkRepository repository in fixture.Repositories
            .OrderBy(repository => repository.Id, StringComparer.Ordinal))
        {
            foreach (ChangeAuthorPeriodManifestHead head in repository.Heads
                .OrderBy(head => head.Id, StringComparer.Ordinal))
            {
                foreach (ChangeAuthorPeriodManifestContributor contributor in fixture.Manifest.Contributors
                    .OrderBy(contributor => contributor.Id, StringComparer.Ordinal))
                {
                    invocations++;
                    GitAuthorPeriodPortfolioPlan plan;
                    try
                    {
                        plan = await new GitPortfolioPlanner().PlanAuthorPeriodAsync(
                            repository.Path,
                            Options(fixture.Manifest.Selection, contributor, head),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException exception) when (exception.Message.StartsWith(
                        "No commits matched",
                        StringComparison.Ordinal))
                    {
                        emptyInvocations++;
                        continue;
                    }

                    ChangePortfolioEstimateBatch estimate = await new ChangeEstimator(repositoryEstimator)
                        .EstimatePortfolioCandidatesWithStatisticsAsync(
                            [.. plan.Items.Select(item => item.Plan)],
                            EstimationProfile.Implementation,
                            cancellationToken).ConfigureAwait(false);
                    selectedRows += estimate.Reports.Count;
                    objectReaders += estimate.Statistics.ObjectDatabaseReaders;
                    foreach (ChangeEstimateReport report in estimate.Reports)
                    {
                        string key = Key(repository.Id, report.Selection.Head.ObjectId);
                        if (reports.TryGetValue(key, out ChangeEstimateReport? existing) &&
                            !string.Equals(
                                ContractJson.Serialize(existing),
                                ContractJson.Serialize(report),
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Independent report '{key}' changed across equivalent selectors.");
                        }

                        reports.TryAdd(key, report);
                    }
                }
            }
        }

        timer.Stop();
        TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        return new IndependentRun(
            reports,
            invocations,
            emptyInvocations,
            selectedRows,
            objectReaders,
            repositoryEstimator.InvocationCount,
            timer.Elapsed,
            cpu);
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

    private static GitAuthorPeriodPortfolioOptions Options(
        ChangeAuthorPeriodManifestSelection selection,
        ChangeAuthorPeriodManifestContributor contributor,
        ChangeAuthorPeriodManifestHead head) => new()
        {
            Aliases = contributor.Aliases,
            SinceInclusive = selection.SinceInclusive,
            UntilExclusive = selection.UntilExclusive,
            TimeZone = selection.TimeZone,
            DateField = selection.DateField,
            MergePolicy = selection.MergePolicy,
            CoauthorPolicy = selection.CoauthorPolicy,
            HeadRevision = head.ObjectId,
        };

    private static string Key(string repositoryId, string objectId) => $"{repositoryId}:{objectId}";

    private sealed record CombinedRun(
        GitAuthorPeriodManifestPortfolioPlan Plan,
        ChangePortfolioEstimateBatch Estimate,
        ChangePortfolioReport Report,
        string Json,
        IReadOnlyList<Diagnostic> Diagnostics);

    private sealed record IndependentRun(
        IReadOnlyDictionary<string, ChangeEstimateReport> Reports,
        int Invocations,
        int EmptyInvocations,
        int SelectedRows,
        int ObjectReaders,
        int SnapshotAnalyses,
        TimeSpan Elapsed,
        TimeSpan Cpu);
}
