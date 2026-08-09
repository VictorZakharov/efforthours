namespace EffortHours.Contracts.V1;

public sealed record HostReviewPayloadTotals
{
    public required long Utf8Bytes { get; init; }

    public required long CharacterCount { get; init; }

    public required long ApproximateTokens { get; init; }
}

public sealed record HostReviewPayloadMeasurement
{
    public required string Digest { get; init; }

    public required HostReviewPayloadTotals Size { get; init; }
}

public sealed record HostReviewQueryMeasurement
{
    public required HostReviewQueryKind Kind { get; init; }

    public required bool ContainsSourceExcerpt { get; init; }

    public required HostReviewPayloadMeasurement Payload { get; init; }
}

public sealed record HostReviewAdditionalInputMeasurement
{
    public required bool SizeReported { get; init; }

    public required HostReviewPayloadTotals Size { get; init; }

    public required string Basis { get; init; }
}

public sealed record HostReviewProviderTokenUsage
{
    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? CachedInputTokens { get; init; }

    public string? Basis { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record HostReviewElapsedTelemetry
{
    public long? Milliseconds { get; init; }

    public string? Basis { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record HostReviewCostTelemetry
{
    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    public string? Basis { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record HostReviewDecisionMeasurement
{
    public required string TargetId { get; init; }

    public required HostReviewDecision Decision { get; init; }

    public required EffortCategory BaselineCategory { get; init; }

    public required EffortRange BaselineHours { get; init; }

    public required EffortCategory ReviewedCategory { get; init; }

    public required EffortRange ReviewedHours { get; init; }
}

public sealed record HostReviewCategoryEffort
{
    public required EffortCategory Category { get; init; }

    public required EffortRange Hours { get; init; }
}

public sealed record HostReviewMeasurementPrivacy
{
    public required bool RepositoryIdentityCopied { get; init; }

    public required bool PromptTextCopied { get; init; }

    public required bool SourceTextCopied { get; init; }

    public required bool QuerySelectorsCopied { get; init; }

    public required bool CallerSuppliedTextRetained { get; init; }

    public required string DisclosureNotice { get; init; }
}

public sealed record HostReviewSessionConditions
{
    public required string SessionId { get; init; }

    public required bool BroaderSourceAvailableBeforeDecision { get; init; }

    public required bool ReferenceReviewAvailableBeforeDecision { get; init; }

    public required bool IndependentOfPairedReview { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record HostReviewMeasurement
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string MeasurementVersion { get; init; } = HostReviewMeasurementVersions.V1;

    public required string SubjectId { get; init; }

    public required HostReviewContextMode Context { get; init; }

    public required string ProtocolVersion { get; init; }

    public required string InputDigest { get; init; }

    public required string EstimatorVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required HostReviewModelIdentity ReviewerModel { get; init; }

    public required HostReviewPayloadMeasurement PacketPayload { get; init; }

    public required HostReviewPayloadMeasurement AdjustmentPayload { get; init; }

    public IReadOnlyList<HostReviewQueryMeasurement> Queries { get; init; } = [];

    public required HostReviewAdditionalInputMeasurement AdditionalInput { get; init; }

    public required HostReviewPayloadTotals ObservedInput { get; init; }

    public required bool ObservedInputComplete { get; init; }

    public required HostReviewProviderTokenUsage ProviderTokens { get; init; }

    public required HostReviewElapsedTelemetry Elapsed { get; init; }

    public required HostReviewCostTelemetry Cost { get; init; }

    public required HostReviewSessionConditions Conditions { get; init; }

    public IReadOnlyList<HostReviewDecisionMeasurement> Decisions { get; init; } = [];

    public IReadOnlyList<HostReviewCategoryEffort> BaselineCategories { get; init; } = [];

    public IReadOnlyList<HostReviewCategoryEffort> ReviewedCategories { get; init; } = [];

    public required EffortRange BaselineTotal { get; init; }

    public required EffortRange ReviewedTotal { get; init; }

    public required HostReviewMeasurementPrivacy Privacy { get; init; }
}

public sealed record HostReviewPointAgreementMetrics
{
    public required int SampleCount { get; init; }

    public required decimal ReferenceHours { get; init; }

    public required decimal CandidateHours { get; init; }

    public required decimal AbsoluteErrorHours { get; init; }

    public required decimal MeanAbsoluteErrorHours { get; init; }

    public required decimal SignedErrorHours { get; init; }

    public decimal? WeightedAbsolutePercentageError { get; init; }

    public decimal? AggregateBiasRate { get; init; }
}

public sealed record HostReviewIntervalAgreementMetrics
{
    public required int SampleCount { get; init; }

    public required int ReferenceExpectedCoveredCount { get; init; }

    public decimal? ReferenceExpectedCoverage { get; init; }

    public required int ReferenceRangeFullyCoveredCount { get; init; }

    public decimal? ReferenceRangeFullyCoveredRate { get; init; }

    public required int RangeOverlapCount { get; init; }

    public decimal? RangeOverlapRate { get; init; }
}

public sealed record HostReviewRangeAgreementMetrics
{
    public required HostReviewPointAgreementMetrics Low { get; init; }

    public required HostReviewPointAgreementMetrics Expected { get; init; }

    public required HostReviewPointAgreementMetrics High { get; init; }

    public required HostReviewIntervalAgreementMetrics Interval { get; init; }
}

public sealed record HostReviewLevelComparison
{
    public required HostReviewComparisonLevel Level { get; init; }

    public required HostReviewRangeAgreementMetrics BaselineAgreement { get; init; }

    public required HostReviewRangeAgreementMetrics CompactAgreement { get; init; }

    public required decimal BaselineToCompactAbsoluteExpectedCorrectionHours { get; init; }

    public required decimal BaselineToReferenceAbsoluteExpectedCorrectionHours { get; init; }

    public required decimal ExpectedAbsoluteErrorReductionHours { get; init; }

    public decimal? ExpectedAbsoluteErrorReductionRate { get; init; }
}

public sealed record HostReviewContextTelemetry
{
    public required HostReviewPayloadTotals ObservedInput { get; init; }

    public required bool ObservedInputComplete { get; init; }

    public required int QueryCount { get; init; }

    public required int SelectedSourceQueryCount { get; init; }

    public required HostReviewProviderTokenUsage ProviderTokens { get; init; }

    public required HostReviewElapsedTelemetry Elapsed { get; init; }

    public required HostReviewCostTelemetry Cost { get; init; }
}

public sealed record HostReviewTelemetryComparison
{
    public required HostReviewContextTelemetry Compact { get; init; }

    public required HostReviewContextTelemetry BroaderSource { get; init; }

    public decimal? ObservedInputByteRatio { get; init; }

    public decimal? ApproximateInputTokenRatio { get; init; }

    public decimal? ProviderInputTokenRatio { get; init; }

    public decimal? ElapsedTimeRatio { get; init; }

    public decimal? MonetaryCostRatio { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

public sealed record HostReviewSubjectBenchmark
{
    public required string SubjectId { get; init; }

    public required string InputDigest { get; init; }

    public required string EstimatorVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public required string CompactMeasurementDigest { get; init; }

    public required string BroaderSourceMeasurementDigest { get; init; }

    public required HostReviewLevelComparison CapabilityItems { get; init; }

    public required HostReviewLevelComparison Categories { get; init; }

    public required HostReviewLevelComparison RepositoryTotal { get; init; }

    public required HostReviewTelemetryComparison Telemetry { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public sealed record HostReviewBudgetDecision
{
    public required bool DefaultBudgetSelected { get; init; }

    public required string Reason { get; init; }
}

public sealed record HostReviewBenchmarkReport
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public string MeasurementVersion { get; init; } = HostReviewMeasurementVersions.V1;

    public string MetricVersion { get; init; } = HostReviewMeasurementVersions.MetricsV1;

    public required int SubjectCount { get; init; }

    public required int MeasurementCount { get; init; }

    public IReadOnlyList<HostReviewSubjectBenchmark> Subjects { get; init; } = [];

    public required HostReviewLevelComparison CapabilityItems { get; init; }

    public required HostReviewLevelComparison Categories { get; init; }

    public required HostReviewLevelComparison RepositoryTotals { get; init; }

    public required HostReviewBudgetDecision BudgetDecision { get; init; }

    public IReadOnlyList<string> Limitations { get; init; } = [];
}
