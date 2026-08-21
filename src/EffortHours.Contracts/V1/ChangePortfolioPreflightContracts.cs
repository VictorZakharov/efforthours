namespace EffortHours.Contracts.V1;

public static class ChangePortfolioPreflightPolicies
{
    public const string ProtocolV1 = "author-period-scope-preflight/1.0.0";

    public const string CandidateLedgerChargeV1 =
        "author-period-candidate-ledger-charge/1.0.0";

    // Presentation guidance only. It never removes a selected change from the
    // calculation or changes reconciliation.
    public const int DetailedReportReviewThreshold = 2_000;
}

public enum ChangePortfolioPreflightStatus
{
    Ready,
    SummaryRecommended,
    Blocked,
}

public enum ChangePortfolioPreflightAction
{
    RunNormally,
    RunCheckpointedSummary,
    ResolveResourceBudget,
    ReviewEmptySelection,
}

public sealed record ChangePortfolioPreflightReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string Protocol { get; init; } = ChangePortfolioPreflightPolicies.ProtocolV1;

    public required string CliVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required ChangePortfolioSelection Selection { get; init; }

    public required ChangePortfolioPreflightStatus Status { get; init; }

    public required ChangePortfolioPreflightTotals Totals { get; init; }

    public IReadOnlyList<ChangePortfolioPreflightRepository> Repositories { get; init; } = [];

    public required ChangePortfolioPreflightResources Resources { get; init; }

    public required ChangePortfolioPreflightOutputPolicy OutputPolicy { get; init; }

    public required ChangePortfolioPreflightRecommendation Recommendation { get; init; }

    public required ChangePortfolioPreflightVerification Verification { get; init; }
}

public sealed record ChangePortfolioPreflightTotals
{
    public int RepositoryCount { get; init; }

    public int HeadCount { get; init; }

    public int ContributorCount { get; init; }

    public long CandidateCount { get; init; }

    public bool CandidateCountIsLowerBound { get; init; }

    public long? SelectedChangeCount { get; init; }

    public long? SharedContributorChangeCount { get; init; }

    public long? ProjectedSnapshotRequests { get; init; }
}

public sealed record ChangePortfolioPreflightRepository
{
    public required string RepositoryId { get; init; }

    public int HeadCount { get; init; }

    public int CandidateCount { get; init; }

    public bool CandidateCountIsLowerBound { get; init; }

    public int? SelectedChangeCount { get; init; }

    public int? SharedContributorChangeCount { get; init; }

    public long? ProjectedSnapshotRequests { get; init; }

    public int SelectionChunkCount { get; init; }

    public int? AnalysisChunkCount { get; init; }

    public long ChargedCandidateLedgerBytes { get; init; }

    public long MaximumCandidateLedgerBytes { get; init; }

    public string? BlockingResource { get; init; }

    public IReadOnlyList<ChangePortfolioPreflightContributor> Contributors { get; init; } = [];
}

public sealed record ChangePortfolioPreflightContributor
{
    public required string ContributorId { get; init; }

    public int CandidateCount { get; init; }

    public int DirectAuthorCandidateCount { get; init; }

    public int CoauthorCandidateCount { get; init; }

    public int? SelectedChangeCount { get; init; }
}

public sealed record ChangePortfolioPreflightResources
{
    public string? BlockingResource { get; init; }

    public required string CandidateLedgerChargePolicy { get; init; }

    public long ChargedCandidateLedgerBytes { get; init; }

    public long MaximumCandidateLedgerBytesPerRepository { get; init; }

    public int SelectionChunkCount { get; init; }

    public int SelectionChunkSize { get; init; }

    public int? AnalysisChunkCount { get; init; }

    public int AnalysisChunkSize { get; init; }

    public int MaximumConcurrentRepositories { get; init; }

    public int MaximumConcurrentChangesPerRepository { get; init; }

    public int MaximumConcurrentCpuWorkItems { get; init; }

    public int MaximumConcurrentGitTreeReads { get; init; }

    public int MaximumPendingFileInspections { get; init; }

    public int MaximumBufferedFileBytes { get; init; }

    public int EmergencyMaximumCandidatesPerRepository { get; init; }

    public int EmergencyMaximumSelectedChanges { get; init; }

    public long MaximumCheckpointBytesPerRepository { get; init; }

    public long MaximumRenderedOutputBytes { get; init; }
}

public sealed record ChangePortfolioPreflightOutputPolicy
{
    public int DetailedReportReviewThreshold { get; init; }

    public bool FullCalculationIncludesEverySelectedChange { get; init; }

    public bool SummaryMayOmitDetailRows { get; init; }

    public required string DetailContract { get; init; }
}

public sealed record ChangePortfolioPreflightRecommendation
{
    public required ChangePortfolioPreflightAction Action { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string> Steps { get; init; } = [];
}

public sealed record ChangePortfolioPreflightVerification
{
    public required string ManifestDigest { get; init; }

    public bool CompleteScope { get; init; }

    public bool CalculationPerformed { get; init; }

    public bool RepositoryPathsExcluded { get; init; }

    public bool RawAliasesExcluded { get; init; }
}
