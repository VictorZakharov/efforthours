using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal sealed record ManualQaCandidatePolicy
{
    public required string PolicyVersion { get; init; }

    public required string Id { get; init; }

    public required string CandidateId { get; init; }

    public required string EstimatorVersion { get; init; }

    public required string BaselineEstimatorVersion { get; init; }

    public required string FeatureContractVersion { get; init; }

    public required string EffectiveDate { get; init; }

    public required string LicenseExpression { get; init; }

    public required string Maturity { get; init; }

    public required string Basis { get; init; }

    public required string Projection { get; init; }

    public required decimal LowRatio { get; init; }

    public required decimal ExpectedRatio { get; init; }

    public required decimal HighRatio { get; init; }

    public required decimal MaximumConfidence { get; init; }

    public IReadOnlyList<EffortCategory> EligibleCategories { get; init; } = [];

    public IReadOnlyList<string> Limitations { get; init; } = [];
}
