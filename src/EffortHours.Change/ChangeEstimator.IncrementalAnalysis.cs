using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private async Task<SnapshotAnalysis?> TryDeriveSnapshotAnalysisAsync(
        string repositoryName,
        IChangeSnapshot snapshot,
        EstimationProfile profile,
        SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace,
        ChangeAnalysisScope? analysisScope,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        bool ensureParentAnalysis,
        CancellationToken cancellationToken)
    {
        if (analysisScope is null ||
            snapshot is not GitSnapshotFileSystem gitSnapshot ||
            !gitSnapshot.TryGetFirstParentAnalysis(
                out string parentInventoryDigest,
                out IReadOnlyList<string> changedPaths) ||
            string.Equals(
                parentInventoryDigest,
                gitSnapshot.InventoryDigest,
                StringComparison.Ordinal))
        {
            return null;
        }

        if (!snapshotAnalyses.TryGetExistingAsync(
                cacheNamespace,
                parentInventoryDigest,
                analysisScope.Id,
                out Task<SnapshotAnalysis>? previousTask))
        {
            if (!ensureParentAnalysis)
            {
                return null;
            }

            await EnsureParentSnapshotAnalysesAsync(
                repositoryName,
                gitSnapshot,
                profile,
                snapshotAnalyses,
                cacheNamespace,
                analysisScope,
                executionTelemetry,
                cancellationToken).ConfigureAwait(false);
            if (!snapshotAnalyses.TryGetExistingAsync(
                cacheNamespace,
                parentInventoryDigest,
                analysisScope.Id,
                out previousTask))
            {
                return null;
            }
        }

        SnapshotAnalysis previous = await previousTask.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        string[] changedScopePaths =
        [
            .. changedPaths.Where(analysisScope.Paths.Contains).Order(StringComparer.Ordinal),
        ];
        RepositoryEvidence? evidence = await RepositoryEvidenceIncrementalDeriver.TryDeriveAsync(
            previous.Evidence,
            snapshot.FileSystem,
            snapshot.RootPath,
            changedScopePaths,
            cancellationToken).ConfigureAwait(false);
        if (evidence is null)
        {
            return null;
        }

        evidence = ApplyAnalysisScope(evidence, snapshot, analysisScope);
        evidence = RenameRepository(evidence, repositoryName);
        EstimateReport estimate = RefreshDerivedEstimate(previous.Estimate, evidence);
        return new SnapshotAnalysis(evidence, estimate);
    }

    internal static EstimateReport RefreshDerivedEstimate(
        EstimateReport previous,
        RepositoryEvidence evidence) => previous with
        {
            Repository = evidence.Repository,
            Diagnostics =
            [
                .. previous.Diagnostics
                    .Where(diagnostic => diagnostic.Code != "FB5205")
                    .Concat(evidence.Diagnostics.Where(diagnostic => diagnostic.Code == "FB5205"))
                    .Distinct()
                    .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal),
            ],
        };

    private async Task EnsureParentSnapshotAnalysesAsync(
        string repositoryName,
        GitSnapshotFileSystem snapshot,
        EstimationProfile profile,
        SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace,
        ChangeAnalysisScope analysisScope,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        List<GitSnapshotFileSystem> missing = [];
        GitSnapshotFileSystem cursor = snapshot;
        try
        {
            while (cursor.TryCreateFirstParentSnapshot(out GitSnapshotFileSystem? parent))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(
                    parent.InventoryDigest,
                    cursor.InventoryDigest,
                    StringComparison.Ordinal))
                {
                    await parent.DisposeAsync().ConfigureAwait(false);
                    break;
                }

                if (snapshotAnalyses.TryGetExistingAsync(
                    cacheNamespace,
                    parent.InventoryDigest,
                    analysisScope.Id,
                    out Task<SnapshotAnalysis>? existing))
                {
                    await existing.WaitAsync(cancellationToken).ConfigureAwait(false);
                    await parent.DisposeAsync().ConfigureAwait(false);
                    break;
                }

                missing.Add(parent);
                cursor = parent;
            }

            for (int index = missing.Count - 1; index >= 0; index--)
            {
                _ = await AnalyzeSnapshotAsync(
                    repositoryName,
                    missing[index],
                    profile,
                    snapshotAnalyses,
                    cacheNamespace,
                    analysisScope,
                    executionTelemetry,
                    cancellationToken,
                    ensureParentAnalysis: false,
                    recordCacheRequest: false).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (GitSnapshotFileSystem missingSnapshot in missing)
            {
                await missingSnapshot.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
