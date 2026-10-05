using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private EstimateReport? TryReuseFullyAnalyzedStock(RepositoryEvidence evidence,
        IChangeSnapshot snapshot, SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace, ChangeAnalysisScope? analysisScope, CancellationToken cancellationToken)
    {
        if (_repositoryEstimator is not SeedEstimator || analysisScope is null ||
            snapshot is not GitSnapshotFileSystem git ||
            !git.TryGetFirstParentAnalysis(out string parentDigest, out _) ||
            !snapshotAnalyses.TryGetCompleted(cacheNamespace, parentDigest, analysisScope.Id,
                out SnapshotAnalysis previous) ||
            !RepositoryStockEvidenceEquality.Equivalent(previous.Evidence, evidence, cancellationToken))
            return null;
        snapshotAnalyses.RecordSeedStockReuse();
        return RefreshDerivedEstimate(previous.Estimate, evidence);
    }
}
