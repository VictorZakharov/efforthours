namespace EffortHours.RepositoryCalibration;

internal sealed record LogicalCandidateModel
{
    public required string ModelVersion { get; init; }

    public required string Id { get; init; }

    public required string CandidateId { get; init; }

    public required string EstimatorVersion { get; init; }

    public required string BaselineEstimatorVersion { get; init; }

    public required string FeatureContractVersion { get; init; }

    public required string EffectiveDate { get; init; }

    public required string LicenseExpression { get; init; }

    public required LogicalCandidateTraining Training { get; init; }

    public required LogicalCandidatePointModel Point { get; init; }

    public required LogicalCandidateRangeModel Range { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = [];
}

internal sealed record LogicalCandidateTraining
{
    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required string CorpusDigest { get; init; }

    public required string ImplementationCommit { get; init; }

    public required int RepositoryCount { get; init; }

    public required int TargetCount { get; init; }

    public IReadOnlyList<LogicalCandidateTrainingSource> Sources { get; init; } = [];
}

internal sealed record LogicalCandidateTrainingSource
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required string EstimateDigest { get; init; }

    public required string EvidenceDigest { get; init; }
}

internal sealed record LogicalCandidatePointModel
{
    public required string ScorerVersion { get; init; }

    public IReadOnlyList<string> Features { get; init; } = [];

    public IReadOnlyList<string> SizeBands { get; init; } = [];

    public required decimal MinimumFactor { get; init; }

    public required decimal MaximumFactor { get; init; }

    public IReadOnlyList<LogicalCandidatePointFactorCeiling> MaximumFactorOverrides { get; init; } = [];

    public decimal SeedAnchorFactor { get; init; }

    public decimal SeedAnchorMaximumLogicalHours { get; init; }

    public IReadOnlyList<string> SeedAnchorWorkItemKinds { get; init; } = [];

    public required string UnknownGroupBehavior { get; init; }

    public IReadOnlyList<LogicalCandidatePointFactor> Factors { get; init; } = [];
}

internal sealed record LogicalCandidatePointFactorCeiling
{
    public required string WorkItemKind { get; init; }

    public required decimal MaximumFactor { get; init; }
}

internal sealed record LogicalCandidatePointFactor
{
    public required string WorkItemKind { get; init; }

    public required string LogicalSizeBand { get; init; }

    public required int SampleCount { get; init; }

    public required decimal LogicalExpectedHours { get; init; }

    public decimal SeedExpectedHours { get; init; }

    public required decimal ReviewedExpectedHours { get; init; }

    public required decimal Factor { get; init; }

    public string? SampleSource { get; init; }
}

internal sealed record LogicalCandidateRangeModel
{
    public IReadOnlyList<string> Features { get; init; } = [];

    public required decimal LowerQuantile { get; init; }

    public required decimal UpperQuantile { get; init; }

    public required int MinimumExactGroupSamples { get; init; }

    public required string SparseGroupFallback { get; init; }

    public required string UnknownGroupBehavior { get; init; }

    public IReadOnlyList<LogicalCandidateRangeFactor> Factors { get; init; } = [];
}

internal sealed record LogicalCandidateRangeFactor
{
    public required string WorkItemKind { get; init; }

    public required string ExpectedSizeBand { get; init; }

    public required string SampleSource { get; init; }

    public required int SampleCount { get; init; }

    public required decimal LowFactor { get; init; }

    public required decimal HighFactor { get; init; }
}

internal sealed record LogicalCandidateDevelopmentInput
{
    public required string DirectoryPath { get; init; }

    public required string EstimateJson { get; init; }

    public required string EvidenceJson { get; init; }

    public required EffortHours.Contracts.V1.EstimateReport Estimate { get; init; }

    public required EffortHours.Contracts.V1.RepositoryEvidence Evidence { get; init; }

    public required string EstimateDigest { get; init; }

    public required string EvidenceDigest { get; init; }
}
