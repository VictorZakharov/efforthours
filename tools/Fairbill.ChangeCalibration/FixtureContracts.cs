using Fairbill.Contracts.V1;

namespace Fairbill.ChangeCalibration;

internal sealed record FixtureSuite
{
    public required string SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<FixtureCase> Cases { get; init; } = [];
}

internal sealed record FixtureCase
{
    public required string Id { get; init; }

    public required string RepositoryFamilyId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string Ecosystem { get; init; }

    public required ChangeSelectionKind SelectionKind { get; init; }

    public required string BaseStateId { get; init; }

    public required string HeadStateId { get; init; }

    public IReadOnlyList<string> CoverageTags { get; init; } = [];

    public IReadOnlyList<FixtureState> States { get; init; } = [];

    public IReadOnlyList<FixtureComponent> Components { get; init; } = [];
}

internal sealed record FixtureState
{
    public required string Id { get; init; }

    public IReadOnlyDictionary<string, string> Files { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

internal sealed record FixtureComponent
{
    public required string Selector { get; init; }

    public required string BaseStateId { get; init; }

    public required string HeadStateId { get; init; }
}

internal sealed record GeneratedFixtureIndex
{
    public required string SchemaVersion { get; init; }

    public required string GeneratorVersion { get; init; }

    public required string SuiteId { get; init; }

    public required string SuiteVersion { get; init; }

    public required string SuiteDigest { get; init; }

    public IReadOnlyList<GeneratedFixtureCase> Cases { get; init; } = [];
}

internal sealed record GeneratedFixtureCase
{
    public required string Id { get; init; }

    public required string RepositoryFamilyId { get; init; }

    public required CalibrationPartition Partition { get; init; }

    public required string Ecosystem { get; init; }

    public required ChangeSelectionKind SelectionKind { get; init; }

    public required string EstimatePath { get; init; }

    public required string BlindPacketPath { get; init; }

    public required string EstimateDigest { get; init; }

    public required string FinalDeltaDigest { get; init; }

    public required int WorkItemCount { get; init; }

    public IReadOnlyList<string> CoverageTags { get; init; } = [];
}
