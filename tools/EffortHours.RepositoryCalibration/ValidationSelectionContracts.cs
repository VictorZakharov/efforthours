namespace EffortHours.RepositoryCalibration;

internal sealed record ValidationSelectionArtifact
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Digest { get; init; }
}

internal sealed record ValidationProjectionArtifact
{
    public required string RecordId { get; init; }

    public required string RepositoryId { get; init; }

    public required string SourceDigest { get; init; }

    public required string SeedEstimateDigest { get; init; }

    public required string CandidateEstimateDigest { get; init; }

    public required string CandidateArtifactDigest { get; init; }
}

internal sealed record ValidationSelectionInputs
{
    public required ValidationSelectionArtifact SamplingPlan { get; init; }

    public required ValidationSelectionArtifact Opening { get; init; }

    public required ValidationSelectionArtifact Corpus { get; init; }

    public required ValidationSelectionArtifact CandidateManifest { get; init; }

    public required ValidationSelectionArtifact CandidateModel { get; init; }

    public required ValidationSelectionArtifact SeedEvaluation { get; init; }

    public required ValidationSelectionArtifact CandidateEvaluation { get; init; }

    public IReadOnlyList<ValidationProjectionArtifact> Projections { get; init; } = [];
}

internal sealed record ValidationSelectionBoundary
{
    public required string ValidationAccess { get; init; }

    public required int ValidationFamilyCount { get; init; }

    public required int ValidationTargetCount { get; init; }

    public required string TestAccess { get; init; }

    public required bool TestSourceAccessed { get; init; }

    public required bool TestLabelsAuthored { get; init; }

    public required bool TestCandidateOutputsGenerated { get; init; }
}

internal sealed record ValidationSelectionCandidate
{
    public required int Ordinal { get; init; }

    public required string Id { get; init; }

    public required string CandidateKind { get; init; }

    public required string EstimatorVersion { get; init; }

    public required bool Eligible { get; init; }

    public required CandidatePreflightMetrics Metrics { get; init; }

    public IReadOnlyList<CandidatePreflightGate> BoundaryGates { get; init; } = [];

    public IReadOnlyList<CandidatePreflightGate> ValidationGates { get; init; } = [];

    public IReadOnlyList<CandidatePreflightGate> FrozenOperationalGates { get; init; } = [];

    public IReadOnlyList<string> RejectionReasons { get; init; } = [];
}

internal sealed record ValidationSelectionBaseline
{
    public required string EstimatorVersion { get; init; }

    public required ValidationSelectionArtifact Evaluation { get; init; }

    public required decimal RepositoryExpectedWape { get; init; }

    public required decimal AbsoluteAggregateBias { get; init; }

    public required decimal MedianRepositoryAbsoluteErrorHours { get; init; }

    public required decimal RepositoryExpectedCoverage { get; init; }

    public required decimal MeanRepositoryNormalizedWidth { get; init; }

    public required decimal P90RepositoryNormalizedWidth { get; init; }
}

internal sealed record ValidationSelectionRule
{
    public required string PrimaryMetric { get; init; }

    public required string Direction { get; init; }

    public required decimal AbsoluteWapeTieTolerance { get; init; }

    public IReadOnlyList<string> TieBreakers { get; init; } = [];
}

internal sealed record ValidationSelectionDecision
{
    public required string Status { get; init; }

    public string? SelectedCandidateId { get; init; }

    public required bool TestAuthorized { get; init; }

    public required bool CandidateAdmitted { get; init; }

    public required string ShippedEstimatorVersion { get; init; }

    public required string Rationale { get; init; }

    public required string NextBoundary { get; init; }
}

internal sealed record ValidationSelectionReport
{
    public string SelectionVersion { get; init; } = "repository-validation-selection/1.0.0";

    public string PolicyVersion { get; init; } = "repository-model-admission/1.0.0";

    public required string Status { get; init; }

    public required string EvaluationImplementationCommit { get; init; }

    public required ValidationSelectionInputs Inputs { get; init; }

    public required ValidationSelectionBoundary Boundary { get; init; }

    public required ValidationSelectionBaseline Baseline { get; init; }

    public IReadOnlyList<ValidationSelectionCandidate> Challengers { get; init; } = [];

    public required ValidationSelectionRule SelectionRule { get; init; }

    public required ValidationSelectionDecision Decision { get; init; }
}
