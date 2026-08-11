using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangePortfolioCandidate
{
    public required string RepositoryId { get; init; }

    public required string SelectorId { get; init; }

    public required ChangeEstimateReport Report { get; init; }

    public required ChangePortfolioAttribution Attribution { get; init; }
}

internal sealed class ChangePortfolioItemDraft
{
    public required ChangePortfolioCandidate Candidate { get; init; }

    public required string Id { get; init; }

    public required string BaseContextId { get; init; }

    public required string EvidenceDigest { get; init; }

    public required string PatchDigest { get; init; }

    public required IReadOnlyDictionary<string, ChangePortfolioPathEffect> Effects { get; init; }

    public required IReadOnlyDictionary<(EffortCategory Category, string Path), EffortRange>
        PathCategoryHours
    { get; init; }

    public required IReadOnlyDictionary<EffortCategory, EffortRange> PathlessCategoryHours { get; init; }

    public string? DuplicateOfItemId { get; set; }

    public decimal AllocationWeight { get; set; }

    public decimal AllocatedExpectedHours { get; set; }

    public HashSet<string> UncertaintyReasons { get; } = new(StringComparer.Ordinal);
}

internal sealed record ChangePortfolioPathEffect(
    string Path,
    string? BaseState,
    string? HeadState);

internal sealed record ChangePortfolioAdjustmentCause(
    ChangePortfolioAdjustmentKind Kind,
    decimal Weight,
    string Reason,
    IReadOnlyList<string> ItemIds,
    int AffectedPathCount);

internal sealed record ChangePortfolioGroupResult(
    ChangePortfolioRepositoryGroup Group,
    IReadOnlyList<ChangePortfolioAdjustment> Adjustments);
