namespace EffortHours.Contracts.V1;

/// <summary>
/// Identifies the immutable final delta reviewed by a Change EHE calibration record.
/// Selection metadata is provenance only and never an effort multiplier.
/// </summary>
public sealed record ChangeCalibrationReference
{
    public required string Id { get; init; }

    public required ChangeSelectionKind SelectionKind { get; init; }

    public required string BaseObjectId { get; init; }

    public required string HeadObjectId { get; init; }

    public required string BaseEvidenceDigest { get; init; }

    public required string HeadEvidenceDigest { get; init; }

    /// <summary>
    /// Content-derived identity for the normalized base/head delta. This must equal
    /// the containing calibration repository reference's source digest.
    /// </summary>
    public required string FinalDeltaDigest { get; init; }

    /// <summary>
    /// Review-coverage labels such as tests, deletion, overlap, or delivery. Tags
    /// select corpus strata; they do not affect candidate effort.
    /// </summary>
    public IReadOnlyList<string> CoverageTags { get; init; } = [];
}
