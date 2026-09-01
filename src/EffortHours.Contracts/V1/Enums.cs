namespace EffortHours.Contracts.V1;

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

public enum ChangeSelectionKind
{
    BaseHead,
    Commit,
    Range,
    PullRequest,
}

public enum PullRequestComparisonBasePolicy
{
    ProviderBaseHeadMergeBase,
}

public enum PullRequestObjectAcquisition
{
    LocalReuse,
    ExplicitFetch,
    ManagedCacheReuse,
    ManagedCacheFetch,
}

public enum PullRequestPathCountStatus
{
    Match,
    Mismatch,
    ProviderUnavailable,
}

public enum ChangeSnapshotKind
{
    GitCommit,
    GitTree,
    EmptyTree,
    Directory,
    Evidence,
}

public enum ChangePathStatus
{
    Added,
    Modified,
    Removed,
    Moved,
}

public enum ChangePathClassification
{
    Represented,
    FormattingOnly,
    ExactMove,
    Generated,
    Vendored,
    Minified,
    Binary,
    Lockfile,
    BuildOutput,
    ExactDuplicate,
    Unsupported,
}

public enum ChangeComponentKind
{
    FinalDelta,
    Commit,
}

public enum ChangeAdjustmentKind
{
    SharedSetup,
    Overlap,
    Revert,
    Interaction,
}

public enum ChangeNormalizationStatus
{
    Calculated,
    NotApplicableZeroGross,
}

public enum ChangePortfolioSelectionKind
{
    PullRequests,
    AuthorPeriod,
}

public enum ChangePortfolioDateField
{
    Author,
    Committer,
}

public enum ChangePortfolioMergePolicy
{
    Exclude,
    FirstParent,
}

public enum ChangePortfolioCoauthorPolicy
{
    Include,
    Exclude,
}

public enum ChangePortfolioAttributionKind
{
    PullRequest,
    DirectAuthor,
    Coauthor,
}

public enum ChangePortfolioContributorMatchKind
{
    DirectAuthor,
    Coauthor,
}

public enum ChangePortfolioContributorGroupKind
{
    SingleContributor,
    SharedContributors,
}

public enum ChangePortfolioHeadGroupKind
{
    SingleHead,
    SharedHeads,
}

public enum ChangePortfolioOrderPolicy
{
    OrderIndependent,
    ChronologicalSelectedCommits,
}

public enum ChangePortfolioAdjustmentKind
{
    ExactDuplicate,
    SharedContext,
    Overlap,
    Revert,
    Interaction,
}

public enum ChangePortfolioComparisonStatus
{
    Complete,
    Incomplete,
}

public enum ChangePortfolioBucketPolicyKind
{
    CalendarMonth,
    CalendarWeek,
    Custom,
}

public enum ChangePortfolioComparisonView
{
    Trend,
    Findings,
}

public enum ChangePortfolioContributorNormalization
{
    Joint,
    Isolated,
}

public enum ChangePortfolioSeriesKind
{
    Portfolio,
    ContributorExclusive,
    ContributorIsolated,
    SharedContributors,
}

public enum ChangePortfolioRepositoryExecutionStatus
{
    Complete,
    Reused,
    Failed,
}

public enum ChangePortfolioCheckpointDisposition
{
    Disabled,
    Hit,
    MissWritten,
    MissFailed,
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

public enum HostReviewQueryKind
{
    Capability,
    Evidence,
    Scope,
    SelectedSource,
}

public enum HostReviewDecision
{
    Affirm,
    Replace,
}

public enum HostReviewContextMode
{
    Compact,
    BroaderSource,
}

public enum HostReviewComparisonLevel
{
    CapabilityItem,
    Category,
    RepositoryTotal,
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

public enum ManualQaDecisionPlanStatus
{
    Unreviewed,
    Completed,
}

public enum ManualQaDecisionDisposition
{
    Estimate,
    Exclude,
    Duplicate,
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
