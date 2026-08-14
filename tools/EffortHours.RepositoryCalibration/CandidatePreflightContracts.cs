using System.Text.Json.Serialization;

namespace EffortHours.RepositoryCalibration;

internal sealed record CandidatePreflightArtifactReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Digest { get; init; }
}

internal sealed record CandidatePreflightSourceEstimate
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required string EstimateDigest { get; init; }

    public string? EvidenceDigest { get; init; }
}

internal sealed record CandidatePreflightInputs
{
    public required CandidatePreflightArtifactReference SamplingPlan { get; init; }

    public required CandidatePreflightArtifactReference Corpus { get; init; }

    public required CandidatePreflightArtifactReference SeedEvaluation { get; init; }

    public CandidatePreflightArtifactReference? CandidateModel { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CandidatePreflightArtifactReference? NumericalPreflight { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CandidatePreflightArtifactReference? MeasuredOperationalPreflight { get; init; }

    public IReadOnlyList<CandidatePreflightSourceEstimate> DevelopmentEstimates { get; init; } = [];
}

internal sealed record CandidatePreflightRangeConfiguration
{
    public decimal? LowFactor { get; init; }

    public decimal? HighFactor { get; init; }

    public IReadOnlyList<string>? Features { get; init; }

    public decimal? LowerQuantile { get; init; }

    public decimal? UpperQuantile { get; init; }

    public int? MinimumExactGroupSamples { get; init; }

    public string? SparseGroupFallback { get; init; }
}

internal sealed record CandidatePreflightConfiguration
{
    public required string Kind { get; init; }

    public required string ImplementationCommit { get; init; }

    public required string BaselineEstimatorVersion { get; init; }

    public required string FeatureContractVersion { get; init; }

    public IReadOnlyList<string> Features { get; init; } = [];

    public IReadOnlyList<string> ExactZeroRules { get; init; } = [];

    public IReadOnlyList<string> DiscountRules { get; init; } = [];

    public required decimal DiscountFactor { get; init; }

    public required CandidatePreflightRangeConfiguration Range { get; init; }

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public required string LicenseExpression { get; init; }

    public required string RuntimeBoundary { get; init; }

    public required string FallbackEstimatorVersion { get; init; }

    public required string ExplanationForm { get; init; }
}

internal sealed record CandidatePreflightMetrics
{
    public required decimal RepositoryExpectedWape { get; init; }

    public required decimal RelativeWapeImprovement { get; init; }

    public required decimal AbsoluteAggregateBias { get; init; }

    public required decimal MedianRepositoryAbsoluteErrorHours { get; init; }

    public required decimal FamilyMaximumErrorPassRate { get; init; }

    public required decimal FamilyOrdinaryErrorPassRate { get; init; }

    public required decimal LowWape { get; init; }

    public required decimal HighWape { get; init; }

    public required decimal RepositoryExpectedCoverage { get; init; }

    public required decimal MeanRepositoryNormalizedWidth { get; init; }

    public required decimal P90RepositoryNormalizedWidth { get; init; }

    public required decimal MeanWidthRelativeToSeed { get; init; }

    public required decimal MeanWidthRelativeToReviewed { get; init; }

    public required decimal MatchedTargetExpectedCoverage { get; init; }

    public required decimal MatchedTargetMeanNormalizedWidth { get; init; }

    public required decimal TargetMatchRate { get; init; }

    public required decimal SourceReferenceMatchRate { get; init; }

    public required decimal CandidateItemMatchRate { get; init; }

    public required decimal CategoryMismatchRate { get; init; }
}

internal sealed record CandidatePreflightGate
{
    public required string Id { get; init; }

    public required string Status { get; init; }

    public required bool Passed { get; init; }

    public required string Requirement { get; init; }

    public string? Observed { get; init; }

    public required string Rationale { get; init; }
}

internal sealed record CandidatePreflightDesign
{
    public required string Id { get; init; }

    public required string Status { get; init; }

    public required CandidatePreflightConfiguration Configuration { get; init; }

    public required CandidatePreflightMetrics DevelopmentMetrics { get; init; }

    public IReadOnlyList<CandidatePreflightGate> Gates { get; init; } = [];

    public IReadOnlyList<string> RejectionReasons { get; init; } = [];
}

internal sealed record CandidatePreflightHoldoutBoundary
{
    public required string Validation { get; init; }

    public required string Test { get; init; }

    public required bool CandidateOutputsGenerated { get; init; }

    public required bool LabelsAuthored { get; init; }
}

internal sealed record CandidatePreflightDecision
{
    public required string Status { get; init; }

    public required bool CandidateManifestFrozen { get; init; }

    public required bool ValidationAuthorized { get; init; }

    public required string Rationale { get; init; }

    public required string NextBoundary { get; init; }
}

internal sealed record CandidatePreflightReport
{
    public required string PreflightVersion { get; init; }

    public required string PolicyVersion { get; init; }

    public required string Status { get; init; }

    public required string Partition { get; init; }

    public required CandidatePreflightInputs Inputs { get; init; }

    public required CandidatePreflightDesign Candidate { get; init; }

    public required CandidatePreflightHoldoutBoundary Holdouts { get; init; }

    public required CandidatePreflightDecision Decision { get; init; }
}
