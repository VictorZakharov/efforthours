using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private static async Task<SnapshotAnalysis?> TryDeriveSnapshotAnalysisAsync(
        string repositoryName,
        IChangeSnapshot snapshot,
        SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace,
        ChangeAnalysisScope? analysisScope,
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

        if (!snapshotAnalyses.TryGetCompleted(
                cacheNamespace,
                parentInventoryDigest,
                analysisScope.Id,
                out SnapshotAnalysis previous))
        {
            return null;
        }

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

}
