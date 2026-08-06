namespace Fairbill.Contracts.V1;

public enum EvidenceSourceKind
{
    Observed,
    Measured,
    DeclaredAssumed,
    Inferred,
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public enum EstimationProfile
{
    Implementation,
    Recreation,
}

public enum EstimateViewKind
{
    Repository,
    Category,
    Scope,
    WorkItem,
    Review,
}

public enum ExplanationMatchKind
{
    WorkItem,
    Capability,
}

public enum ComplexityLevel
{
    Routine,
    Moderate,
    High,
    Exceptional,
}

public enum EstimatorKind
{
    Rule,
    LocalMl,
    HostAi,
    Human,
}

public enum CalibrationPartition
{
    Development,
    Validation,
    Test,
}

public enum CalibrationReviewStatus
{
    TeacherEstimate,
    Reviewed,
    Adjudicated,
}

public enum CalibrationReviewerKind
{
    Human,
    HostAi,
}

public enum CalibrationReviewerRole
{
    Teacher,
    Reviewer,
    Adjudicator,
}

public enum CalibrationDataClassification
{
    PublicRedistributable,
    Private,
    Synthetic,
}

public enum CalibrationAuthoringStatus
{
    Unreviewed,
}

public enum CalibrationCandidateVisibility
{
    Reference,
    Blind,
}

public enum CalibrationCorpusReviewAction
{
    Accept,
    Replace,
}

public enum CalibrationMutationPoint
{
    Low,
    Expected,
    High,
}

public enum CalibrationMutationScope
{
    RepositoryTotal,
    Category,
}

public enum VerificationMode
{
    StaticAssumed,
    Executed,
}

public enum WorkingState
{
    AssumedWorking,
    VerifiedWorking,
    KnownIssues,
}

public enum EffortCategory
{
    SpecificationComprehensionAndDomainLearning,
    RepositoryAndSolutionSetup,
    ArchitectureAndTechnicalDesign,
    ProductionImplementation,
    UiImplementationAndRepresentedUxDecisions,
    DataModelingPersistenceAndMigrations,
    ExternalIntegrationsAndProtocols,
    UnitTesting,
    IntegrationContractAndComponentTesting,
    EndToEndAndUiTesting,
    ManualValidationDebuggingAndHardening,
    Documentation,
    BuildConfigurationAndDeveloperTooling,
    CiCdAndInfrastructureAsCode,
    SecurityAndAccessibility,
    PackagingDeploymentAndReleaseArtifacts,
    SelfReviewAndSystemIntegration,
}
