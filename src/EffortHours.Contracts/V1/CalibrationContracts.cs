using System.Text.Json.Serialization;

namespace EffortHours.Contracts.V1;

public sealed record CalibrationRubricReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }
}

public sealed record CalibrationRepositoryReference
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string SourceDigest { get; init; }
}

public sealed record CalibrationSourceProvenance
{
    public required CalibrationDataClassification DataClassification { get; init; }

    public required string SourceReference { get; init; }

    public required string Revision { get; init; }

    public required string LicenseExpression { get; init; }

    public required bool RedistributionAllowed { get; init; }

    public string? Notes { get; init; }
}

public sealed record CalibrationReviewer
{
    public required string Id { get; init; }

    public required CalibrationReviewerKind Kind { get; init; }

    public required CalibrationReviewerRole Role { get; init; }

    public string? ModelId { get; init; }

    public string? ModelVersion { get; init; }
}

public sealed record CalibrationReviewProvenance
{
    public required CalibrationReviewStatus Status { get; init; }

    public required DateOnly CompletedOn { get; init; }

    public IReadOnlyList<CalibrationReviewer> Reviewers { get; init; } = [];

    public string? Notes { get; init; }
}

public sealed record CalibrationTarget
{
    public required string Id { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public IReadOnlyList<string> SourceWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public required EffortRange Hours { get; init; }

    public required string Rationale { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    public string? SizeException { get; init; }
}

public sealed record CalibrationRecord
{
    public required string Id { get; init; }

    public required CalibrationRepositoryReference Repository { get; init; }

    public ChangeCalibrationReference? Change { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string SourceEstimatorVersion { get; init; }

    public required string SourceEstimateDigest { get; init; }

    public required CalibrationSourceProvenance Source { get; init; }

    public required CalibrationReviewProvenance Review { get; init; }

    public IReadOnlyList<CalibrationTarget> Targets { get; init; } = [];
}

public sealed record CalibrationCorpus
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public IReadOnlyList<CalibrationRecord> Records { get; init; } = [];
}

public sealed record CalibrationAuthoringCandidate
{
    public required string EstimatorVersion { get; init; }

    public required string EstimateDigest { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? TotalHours { get; init; }

    public IReadOnlyList<CategoryEstimate> Categories { get; init; } = [];
}

public sealed record CalibrationAuthoringSuggestion
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? Hours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public decimal? Confidence { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Reason { get; init; }
}

public sealed record CalibrationAuthoringReviewFields
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? Hours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Rationale { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? SizeException { get; init; }
}

public sealed record CalibrationAuthoringTarget
{
    public required string Id { get; init; }

    public required string SourceCapabilityId { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public IReadOnlyList<string> SourceWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public required CalibrationAuthoringSuggestion Candidate { get; init; }

    public required CalibrationAuthoringReviewFields Review { get; init; }

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

public sealed record CalibrationAuthoringPacket
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string AuthoringVersion { get; init; }

    public required CalibrationAuthoringStatus Status { get; init; }

    public required string Warning { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public required CalibrationRepositoryReference Repository { get; init; }

    public ChangeCalibrationReference? Change { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationCandidateVisibility CandidateVisibility { get; init; }

    public required CalibrationAuthoringCandidate Candidate { get; init; }

    public IReadOnlyList<CalibrationAuthoringTarget> Targets { get; init; } = [];

    public IReadOnlyList<string> ProfessionalizationGapWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> Instructions { get; init; } = [];
}

public sealed record CalibrationReviewTargetDecision
{
    public required EffortRange Hours { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    public string? SizeException { get; init; }
}

public sealed record CalibrationCapabilityReviewDecision
{
    public required string SourceCapabilityId { get; init; }

    public required string Rationale { get; init; }

    public IReadOnlyList<CalibrationReviewTargetDecision> Targets { get; init; } = [];
}

public sealed record CalibrationReviewPlanRecord
{
    public required string Id { get; init; }

    public required CalibrationRepositoryReference Repository { get; init; }

    public ChangeCalibrationReference? Change { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string SourceEstimatorVersion { get; init; }

    public required string SourceEstimateDigest { get; init; }

    public required CalibrationSourceProvenance Source { get; init; }

    public required CalibrationReviewProvenance Review { get; init; }

    public IReadOnlyList<CalibrationCapabilityReviewDecision> Capabilities { get; init; } = [];
}

public sealed record CalibrationReviewPlan
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string CompilerVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public IReadOnlyList<CalibrationReviewPlanRecord> Records { get; init; } = [];
}

public sealed record CalibrationCorpusReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Digest { get; init; }
}

public sealed record CalibrationCorpusReviewCandidate
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? Hours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Rationale { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? SizeException { get; init; }
}

public sealed record CalibrationCorpusReviewFields
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public CalibrationCorpusReviewAction? Action { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? Hours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Rationale { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? SizeException { get; init; }
}

public sealed record CalibrationCorpusReviewTarget
{
    public required string SourceTargetId { get; init; }

    public required EffortCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public IReadOnlyList<string> SourceWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> EvidenceIds { get; init; } = [];

    public required CalibrationCorpusReviewCandidate Candidate { get; init; }

    public required CalibrationCorpusReviewFields Review { get; init; }
}

public sealed record CalibrationCorpusReviewRecord
{
    public required string SourceRecordId { get; init; }

    public required CalibrationRepositoryReference Repository { get; init; }

    public ChangeCalibrationReference? Change { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required CalibrationReviewStatus SourceReviewStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? CandidateTotalHours { get; init; }

    public IReadOnlyList<CalibrationCorpusReviewTarget> Targets { get; init; } = [];
}

public sealed record CalibrationCorpusReviewPacket
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string AuthoringVersion { get; init; }

    public required CalibrationAuthoringStatus Status { get; init; }

    public required string Warning { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required CalibrationRubricReference Rubric { get; init; }

    public required CalibrationCandidateVisibility CandidateVisibility { get; init; }

    public IReadOnlyList<CalibrationCorpusReviewRecord> Records { get; init; } = [];

    public IReadOnlyList<string> Instructions { get; init; } = [];
}

public sealed record CalibrationCorpusReviewTargetDecision
{
    public required string SourceTargetId { get; init; }

    public required CalibrationCorpusReviewAction Action { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public EffortRange? Hours { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Rationale { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? SizeException { get; init; }
}

public sealed record CalibrationCorpusReviewPlanRecord
{
    public required string SourceRecordId { get; init; }

    public required CalibrationReviewStatus ResultStatus { get; init; }

    public required DateOnly CompletedOn { get; init; }

    public IReadOnlyList<CalibrationReviewer> Reviewers { get; init; } = [];

    public required string Notes { get; init; }

    public IReadOnlyList<CalibrationCorpusReviewTargetDecision> Targets { get; init; } = [];
}

public sealed record CalibrationCorpusReviewPlan
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string CompilerVersion { get; init; }

    public required CalibrationCorpusReference SourceCorpus { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<CalibrationCorpusReviewPlanRecord> Records { get; init; } = [];
}

public sealed record CalibrationMutationCase
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public required string SourceDigest { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }
}

public sealed record CalibrationMutationAssertion
{
    public required string Id { get; init; }

    public required string Family { get; init; }

    public required string SubjectCaseId { get; init; }

    public required string ReferenceCaseId { get; init; }

    public required CalibrationMutationPoint Point { get; init; }

    public required CalibrationMutationScope Scope { get; init; }

    public EffortCategory? Category { get; init; }

    public decimal? MinimumDifferenceHours { get; init; }

    public decimal? MaximumDifferenceHours { get; init; }

    public required string Rationale { get; init; }
}

public sealed record CalibrationMutationSuite
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string MetricVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<CalibrationMutationCase> Cases { get; init; } = [];

    public IReadOnlyList<CalibrationMutationAssertion> Assertions { get; init; } = [];
}

public sealed record CalibrationMutationCaseResult
{
    public required string CaseId { get; init; }

    public required string CandidateEstimatorVersion { get; init; }

    public required string CandidateEstimateDigest { get; init; }

    public required EffortRange TotalHours { get; init; }
}

public sealed record CalibrationMutationAssertionResult
{
    public required string Id { get; init; }

    public required string Family { get; init; }

    public required string SubjectCaseId { get; init; }

    public required string ReferenceCaseId { get; init; }

    public required CalibrationMutationPoint Point { get; init; }

    public required CalibrationMutationScope Scope { get; init; }

    public EffortCategory? Category { get; init; }

    public required decimal SubjectHours { get; init; }

    public required decimal ReferenceHours { get; init; }

    public required decimal DifferenceHours { get; init; }

    public decimal? MinimumDifferenceHours { get; init; }

    public decimal? MaximumDifferenceHours { get; init; }

    public required bool Passed { get; init; }

    public required string Rationale { get; init; }
}

public sealed record CalibrationMutationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string EvaluatorVersion { get; init; }

    public required string MetricVersion { get; init; }

    public required string SuiteId { get; init; }

    public required string SuiteVersion { get; init; }

    public required int CaseCount { get; init; }

    public required int AssertionCount { get; init; }

    public required int PassedCount { get; init; }

    public required int FailedCount { get; init; }

    public required bool AllPassed { get; init; }

    public required int IgnoredCandidateCount { get; init; }

    public IReadOnlyList<string> CandidateEstimatorVersions { get; init; } = [];

    public IReadOnlyList<CalibrationMutationCaseResult> Cases { get; init; } = [];

    public IReadOnlyList<CalibrationMutationAssertionResult> Assertions { get; init; } = [];
}

public sealed record CalibrationPartitionSummary
{
    public required CalibrationPartition Partition { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }
}

public sealed record CalibrationValidationSummary
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required bool Valid { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public IReadOnlyList<CalibrationPartitionSummary> Partitions { get; init; } = [];
}

public sealed record CalibrationPointMetrics
{
    public required int SampleCount { get; init; }

    public required decimal ReviewedHours { get; init; }

    public required decimal CandidateHours { get; init; }

    public required decimal MeanAbsoluteErrorHours { get; init; }

    public required decimal MedianAbsoluteErrorHours { get; init; }

    public required decimal RootMeanSquaredErrorHours { get; init; }

    public required decimal MeanSignedErrorHours { get; init; }

    public decimal? WeightedAbsolutePercentageError { get; init; }

    public decimal? AggregateBiasRate { get; init; }
}

public sealed record CalibrationIntervalMetrics
{
    public required int SampleCount { get; init; }

    public required int ReviewedExpectedCoveredCount { get; init; }

    public decimal? ReviewedExpectedCoverage { get; init; }

    public required int ReviewedRangeFullyCoveredCount { get; init; }

    public decimal? ReviewedRangeFullyCoveredRate { get; init; }

    public required decimal MeanCandidateWidthHours { get; init; }

    public required decimal MeanReviewedWidthHours { get; init; }
}

public sealed record CalibrationRangeMetrics
{
    public required CalibrationPointMetrics Low { get; init; }

    public required CalibrationPointMetrics Expected { get; init; }

    public required CalibrationPointMetrics High { get; init; }

    public required CalibrationIntervalMetrics Interval { get; init; }
}

public sealed record CalibrationCategoryMetrics
{
    public required EffortCategory Category { get; init; }

    public required CalibrationRangeMetrics Metrics { get; init; }
}

public sealed record CalibrationMatchSummary
{
    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public decimal? TargetMatchRate { get; init; }

    public required int SourceWorkItemReferenceCount { get; init; }

    public required int MatchedSourceWorkItemReferenceCount { get; init; }

    public decimal? SourceWorkItemReferenceMatchRate { get; init; }

    public required int CandidateWorkItemCount { get; init; }

    public required int MatchedCandidateWorkItemCount { get; init; }

    public decimal? CandidateWorkItemMatchRate { get; init; }
}

public sealed record CalibrationRepositoryEvaluation
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string BaselineId { get; init; }

    public required string CandidateEstimatorVersion { get; init; }

    public required string CandidateEstimateDigest { get; init; }

    public required EffortRange ReviewedTotal { get; init; }

    public required EffortRange CandidateTotal { get; init; }

    public required decimal ExpectedAbsoluteErrorHours { get; init; }

    public required decimal ExpectedSignedErrorHours { get; init; }

    public required bool ReviewedExpectedCovered { get; init; }

    public required bool ReviewedRangeFullyCovered { get; init; }

    public required int TargetCount { get; init; }

    public required int MatchedTargetCount { get; init; }

    public required int CandidateWorkItemCount { get; init; }

    public required int MatchedCandidateWorkItemCount { get; init; }

    public IReadOnlyList<string> UnmatchedTargetIds { get; init; } = [];

    public IReadOnlyList<string> UnmatchedCandidateWorkItemIds { get; init; } = [];

    public IReadOnlyList<string> CategoryMismatchTargetIds { get; init; } = [];
}

public sealed record CalibrationEvaluationReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string EvaluatorVersion { get; init; }

    public required string MetricVersion { get; init; }

    public required string CorpusId { get; init; }

    public required string CorpusVersion { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required int RecordCount { get; init; }

    public required int RepositoryCount { get; init; }

    public required int IgnoredCandidateCount { get; init; }

    public IReadOnlyList<string> CandidateEstimatorVersions { get; init; } = [];

    public required CalibrationRangeMetrics RepositoryTotals { get; init; }

    public IReadOnlyList<CalibrationCategoryMetrics> Categories { get; init; } = [];

    public required CalibrationRangeMetrics WorkItems { get; init; }

    public required CalibrationMatchSummary Match { get; init; }

    public IReadOnlyList<CalibrationRepositoryEvaluation> Repositories { get; init; } = [];
}
