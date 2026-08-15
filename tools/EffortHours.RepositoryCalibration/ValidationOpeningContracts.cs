namespace EffortHours.RepositoryCalibration;

internal sealed record ValidationOpeningManifest
{
    public string SchemaVersion { get; init; } = "repository-validation-opening/1.0.0";

    public string Id { get; init; } = "efforthours-public-readiness-validation";

    public string Version { get; init; } = "1.3.0";

    public string Status { get; init; } =
        "strict-blind-validation-packets-generated-candidate-values-unavailable";

    public string PolicyVersion { get; init; } = "repository-model-admission/1.0.0";

    public string ToolVersion { get; init; } = ValidationOpeningRunner.ToolVersion;

    public string JsonArtifactDigestPolicy { get; init; } = JsonArtifactDigest.Policy;

    public required string OpeningImplementationCommit { get; init; }

    public required FrozenValidationArtifact CandidateManifest { get; init; }

    public required FrozenValidationArtifact SamplingPlan { get; init; }

    public required FrozenValidationArtifact ReproductionManifest { get; init; }

    public required FrozenValidationArtifact HoldoutCustody { get; init; }

    public ValidationOpeningBoundary Boundary { get; init; } = new();

    public IReadOnlyList<ReproductionFamily> Families { get; init; } = [];

    public IReadOnlyList<string> ContaminatedFamilies { get; init; } = [];

    public IReadOnlyList<string> Failures { get; init; } = [];
}

internal sealed record FrozenValidationArtifact
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Digest { get; init; }
}

internal sealed record ValidationOpeningBoundary
{
    public int ValidationFamilyCount { get; init; } = 9;

    public int TestFamilyCount { get; init; } = 9;

    public string ValidationAccess { get; init; } =
        "source verified; seed evidence and strict-blind packet generated";

    public bool ValidationCandidateOutputsGenerated { get; init; }

    public bool ValidationLabelsAuthored { get; init; }

    public string TestSourceAccess { get; init; } = "not-performed";

    public bool TestCandidateOutputsGenerated { get; init; }

    public bool TestLabelsAuthored { get; init; }

    public string NextBoundary { get; init; } =
        "Author and compile all nine validation records from the blind packets and verified source before generating any frozen-challenger validation output.";
}

internal sealed record VerifiedValidationBoundary
{
    public required SamplingPlan Plan { get; init; }

    public required ReproductionManifest Reproduction { get; init; }

    public required HoldoutCustody Custody { get; init; }

    public required string CandidateManifestDigest { get; init; }

    public required string PlanDigest { get; init; }

    public required string ReproductionDigest { get; init; }

    public required string CustodyDigest { get; init; }

    public IReadOnlyList<SamplingFamily> ValidationFamilies { get; init; } = [];

    public IReadOnlyList<SamplingFamily> TestFamilies { get; init; } = [];
}
