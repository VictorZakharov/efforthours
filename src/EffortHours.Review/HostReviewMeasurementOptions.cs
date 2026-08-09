using EffortHours.Contracts.V1;

namespace EffortHours.Review;

public sealed record HostReviewQueryPayloadInput
{
    public required HostReviewQueryResult Result { get; init; }

    public required string PayloadText { get; init; }
}

public sealed record HostReviewMeasurementOptions
{
    public required string SubjectId { get; init; }

    public required string SessionId { get; init; }

    public required HostReviewContextMode Context { get; init; }

    public IReadOnlyList<HostReviewQueryPayloadInput> Queries { get; init; } = [];

    public long? ElapsedMilliseconds { get; init; }

    public string? ElapsedBasis { get; init; }

    public long? ProviderInputTokens { get; init; }

    public long? ProviderOutputTokens { get; init; }

    public long? ProviderCachedInputTokens { get; init; }

    public string? TokenBasis { get; init; }

    public decimal? CostAmount { get; init; }

    public string? CostCurrency { get; init; }

    public string? CostBasis { get; init; }

    public long AdditionalInputBytes { get; init; }

    public long AdditionalInputCharacters { get; init; }

    public string? AdditionalInputBasis { get; init; }

    public bool AdditionalInputSizeReported { get; init; }

    public bool ObservedInputComplete { get; init; }

    public bool BroaderSourceAvailableBeforeDecision { get; init; }

    public bool ReferenceReviewAvailableBeforeDecision { get; init; }

    public bool IndependentOfPairedReview { get; init; }

    public IReadOnlyList<string> ConditionNotes { get; init; } = [];
}
