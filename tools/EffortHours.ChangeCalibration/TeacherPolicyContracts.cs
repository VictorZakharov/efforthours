using EffortHours.Contracts.V1;

namespace EffortHours.ChangeCalibration;

internal sealed record TeacherPolicy
{
    public required string SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Description { get; init; }

    public required DateOnly CompletedOn { get; init; }

    public required CalibrationReviewer Reviewer { get; init; }

    public required string ReviewNotes { get; init; }

    public IReadOnlyList<TeacherCasePolicy> Cases { get; init; } = [];
}

internal sealed record TeacherCasePolicy
{
    public required string Id { get; init; }

    public required string Rationale { get; init; }

    public IReadOnlyList<TeacherCategoryDecision> Categories { get; init; } = [];
}

internal sealed record TeacherCategoryDecision
{
    public required EffortCategory Category { get; init; }

    public required TeacherCategoryAction Action { get; init; }

    public EffortRange? Hours { get; init; }

    public int? TargetCount { get; init; }

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];
}

internal enum TeacherCategoryAction
{
    Target,
    Exclude,
}
