namespace EffortHours.Contracts.V1;

public static class ContractVersions
{
    public const string V1 = "1.0.0";
}

public static class HostReviewProtocolVersions
{
    public const string V1 = "host-review/1.0.0";
}

public static class HostReviewMeasurementVersions
{
    public const string V1 = "host-review-measurement/1.0.0";

    public const string MetricsV1 = "host-review-comparison-metrics/1.0.0";
}

public static class EligibleCodingEffortVersions
{
    public const string V1 = "eligible-coding-effort/1.0.0";

    public static IReadOnlyList<EffortCategory> Categories { get; } =
        Array.AsReadOnly<EffortCategory>(
        [
        EffortCategory.ProductionImplementation,
        EffortCategory.UiImplementationAndRepresentedUxDecisions,
        EffortCategory.DataModelingPersistenceAndMigrations,
        EffortCategory.ExternalIntegrationsAndProtocols,
        EffortCategory.UnitTesting,
        EffortCategory.IntegrationContractAndComponentTesting,
        EffortCategory.EndToEndAndUiTesting,
        EffortCategory.BuildConfigurationAndDeveloperTooling,
        EffortCategory.CiCdAndInfrastructureAsCode,
        EffortCategory.SecurityAndAccessibility,
        EffortCategory.PackagingDeploymentAndReleaseArtifacts,
        ]);
}

public static class ManualQaReviewVersions
{
    public const string PolicyV1 = "manual-qa-development-review-policy/1.0.0";

    public const string AuthoringV1 = "manual-qa-development-review-authoring/1.0.0";

    public const string ManifestV1 = "manual-qa-development-review-manifest/1.0.0";

    public const string RubricId = "manual-qa-work-item";

    public const string RubricV1 = "1.0.0";
}

public static class CalibrationUncertaintyVersions
{
    public const string FeatureContractV1 = "repository-uncertainty-features/1.0.0";

    public const string FeatureContractDigestV1 =
        "sha256:a2fea34b25d0c963bb9e96d8c538e130f6b2b23d4d98d851584a7fbe69916077";

    public const string ProjectorV1 = "uncertainty-feature-projector/1.0.0";

    public const string StructuralFeatureContractV1 =
        "repository-uncertainty-structural-features/1.0.0";

    public const string StructuralFeatureContractDigestV1 =
        "sha256:a186c6e61ef7fcbd294ca4de27ed8504b313599e96246d8bcc7321eda04204ab";

    public const string StructuralProjectorV1 =
        "uncertainty-structural-feature-projector/1.0.0";

    public const string GraphFeatureContractV1 =
        "repository-uncertainty-graph-features/1.0.0";

    public const string GraphFeatureContractDigestV1 =
        "sha256:3b41238130578b02e3c1b3426103cbbc5f1b6656efafe50fe53c336b94570200";

    public const string GraphProjectorV1 =
        "uncertainty-graph-feature-projector/1.0.0";

    public const string GraphEvaluatorV1 =
        "uncertainty-graph-feature-evaluator/1.0.0";

    public const string GraphEvaluationProtocolV1 =
        "uncertainty-graph-feature-evaluation/1.0.0";

    public const string GraphEvaluationPolicyV1 =
        "uncertainty-graph-evaluation-policy/1.0.0";

    public const string GraphEvaluationPolicyDigestV1 =
        "sha256:f742039c129f02e6e423c31cf486f0c33e4b2e20b329da27294786cc9385c0db";

    public const string StructuralEvaluatorV1 =
        "uncertainty-structural-feature-evaluator/1.0.0";

    public const string StructuralEvaluationProtocolV1 =
        "uncertainty-structural-feature-evaluation/1.0.0";

    public const string StructuralEvaluationPolicyV1 =
        "uncertainty-structural-evaluation-policy/1.0.0";

    public const string StructuralEvaluationPolicyDigestV1 =
        "sha256:2f3dc2417747ac8557744eb2d59c3b2e816158620eb26086f383d3809610f610";

    public const string IntervalPolicyV1 = "symmetric-planning-interval/1.0.0";

    public const string EvaluationProtocolV1 = "uncertainty-feature-evaluation/1.0.0";

    public const string EvaluationMetricV1 = "uncertainty-feature-metrics/1.0.0";

    public const string SupportPopulationV1 = "uncertainty-support-population/1.0.0";

    public const string SupportPolicyV1 = "uncertainty-support-policy/1.0.0";

    public const string SupportProfilerV1 = "uncertainty-support-profiler/1.0.0";

    public const string SupportEvaluationV1 = "uncertainty-support-evaluation/1.0.0";

    public const string SupportEvaluationMetricV1 = "uncertainty-support-metrics/1.0.0";

    public const string SupportTargetAggregationV1 =
        "uncertainty-support-target-aggregation/1.0.0";
}

public static class SchemaNames
{
    public const string CalibrationAuthoringPacket = "calibration-authoring-packet.schema.json";
    public const string CalibrationCorpus = "calibration-corpus.schema.json";
    public const string CalibrationCorpusReviewPacket = "calibration-corpus-review-packet.schema.json";
    public const string CalibrationCorpusReviewPlan = "calibration-corpus-review-plan.schema.json";
    public const string CalibrationDiagnostic = "calibration-diagnostic.schema.json";
    public const string CalibrationEvaluation = "calibration-evaluation.schema.json";
    public const string CalibrationUncertaintyFeatures =
        "calibration-uncertainty-features.schema.json";
    public const string CalibrationUncertaintyStructuralFeatures =
        "calibration-uncertainty-structural-features.schema.json";
    public const string CalibrationUncertaintyGraphFeatures =
        "calibration-uncertainty-graph-features.schema.json";
    public const string CalibrationUncertaintyGraphEvaluation =
        "calibration-uncertainty-graph-evaluation.schema.json";
    public const string CalibrationUncertaintyStructuralEvaluation =
        "calibration-uncertainty-structural-evaluation.schema.json";
    public const string CalibrationUncertaintyEvaluation =
        "calibration-uncertainty-evaluation.schema.json";
    public const string CalibrationUncertaintySupportPopulation =
        "calibration-uncertainty-support-population.schema.json";
    public const string CalibrationUncertaintySupportProfile =
        "calibration-uncertainty-support-profile.schema.json";
    public const string CalibrationUncertaintySupportEvaluation =
        "calibration-uncertainty-support-evaluation.schema.json";
    public const string CalibrationMutationReport = "calibration-mutation-report.schema.json";
    public const string CalibrationMutationSuite = "calibration-mutation-suite.schema.json";
    public const string CalibrationManualQaReviewManifest =
        "calibration-manual-qa-review-manifest.schema.json";
    public const string CalibrationManualQaReviewPacket =
        "calibration-manual-qa-review-packet.schema.json";
    public const string CalibrationManualQaReviewPolicy =
        "calibration-manual-qa-review-policy.schema.json";
    public const string CalibrationReviewPlan = "calibration-review-plan.schema.json";
    public const string CalibrationValidation = "calibration-validation.schema.json";
    public const string ChangeEstimateExplanation = "change-estimate-explanation.schema.json";
    public const string ChangeEstimateReport = "change-estimate-report.schema.json";
    public const string ChangeEvidence = "change-evidence.schema.json";
    public const string ChangeAuthorPeriodManifest = "change-author-period-manifest.schema.json";
    public const string ChangePortfolioManifest = "change-portfolio-manifest.schema.json";
    public const string ChangePortfolioReport = "change-portfolio-report.schema.json";
    public const string Diagnostic = "diagnostic.schema.json";
    public const string EstimateExplanation = "estimate-explanation.schema.json";
    public const string EstimateReport = "estimate-report.schema.json";
    public const string EstimateView = "estimate-view.schema.json";
    public const string HostReviewAdjustment = "host-review-adjustment.schema.json";
    public const string HostReviewBenchmark = "host-review-benchmark.schema.json";
    public const string HostReviewMeasurement = "host-review-measurement.schema.json";
    public const string HostReviewPacket = "host-review-packet.schema.json";
    public const string HostReviewQueryResult = "host-review-query-result.schema.json";
    public const string HostReviewValidation = "host-review-validation.schema.json";
    public const string RateCard = "rate-card.schema.json";
    public const string RateCardModel = "rate-card-model.schema.json";
    public const string RepositoryEvidence = "repository-evidence.schema.json";
    public const string RepositoryScanCache = "repository-scan-cache.schema.json";
    public const string SeedRuleModel = "seed-rule-model.schema.json";
    public const string WorkItem = "work-item.schema.json";
}

public static class SchemaIds
{
    public const string CalibrationAuthoringPacket = "urn:eh:schema:v1:calibration-authoring-packet";
    public const string CalibrationCorpus = "urn:eh:schema:v1:calibration-corpus";
    public const string CalibrationCorpusReviewPacket = "urn:eh:schema:v1:calibration-corpus-review-packet";
    public const string CalibrationCorpusReviewPlan = "urn:eh:schema:v1:calibration-corpus-review-plan";
    public const string CalibrationDiagnostic = "urn:eh:schema:v1:calibration-diagnostic";
    public const string CalibrationEvaluation = "urn:eh:schema:v1:calibration-evaluation";
    public const string CalibrationUncertaintyFeatures =
        "urn:eh:schema:v1:calibration-uncertainty-features";
    public const string CalibrationUncertaintyStructuralFeatures =
        "urn:eh:schema:v1:calibration-uncertainty-structural-features";
    public const string CalibrationUncertaintyGraphFeatures =
        "urn:eh:schema:v1:calibration-uncertainty-graph-features";
    public const string CalibrationUncertaintyGraphEvaluation =
        "urn:eh:schema:v1:calibration-uncertainty-graph-evaluation";
    public const string CalibrationUncertaintyStructuralEvaluation =
        "urn:eh:schema:v1:calibration-uncertainty-structural-evaluation";
    public const string CalibrationUncertaintyEvaluation =
        "urn:eh:schema:v1:calibration-uncertainty-evaluation";
    public const string CalibrationUncertaintySupportPopulation =
        "urn:eh:schema:v1:calibration-uncertainty-support-population";
    public const string CalibrationUncertaintySupportProfile =
        "urn:eh:schema:v1:calibration-uncertainty-support-profile";
    public const string CalibrationUncertaintySupportEvaluation =
        "urn:eh:schema:v1:calibration-uncertainty-support-evaluation";
    public const string CalibrationMutationReport = "urn:eh:schema:v1:calibration-mutation-report";
    public const string CalibrationMutationSuite = "urn:eh:schema:v1:calibration-mutation-suite";
    public const string CalibrationManualQaReviewManifest =
        "urn:eh:schema:v1:calibration-manual-qa-review-manifest";
    public const string CalibrationManualQaReviewPacket =
        "urn:eh:schema:v1:calibration-manual-qa-review-packet";
    public const string CalibrationManualQaReviewPolicy =
        "urn:eh:schema:v1:calibration-manual-qa-review-policy";
    public const string CalibrationReviewPlan = "urn:eh:schema:v1:calibration-review-plan";
    public const string CalibrationValidation = "urn:eh:schema:v1:calibration-validation";
    public const string ChangeEstimateExplanation = "urn:eh:schema:v1:change-estimate-explanation";
    public const string ChangeEstimateReport = "urn:eh:schema:v1:change-estimate-report";
    public const string ChangeEvidence = "urn:eh:schema:v1:change-evidence";
    public const string ChangeAuthorPeriodManifest = "urn:eh:schema:v1:change-author-period-manifest";
    public const string ChangePortfolioManifest = "urn:eh:schema:v1:change-portfolio-manifest";
    public const string ChangePortfolioReport = "urn:eh:schema:v1:change-portfolio-report";
    public const string Diagnostic = "urn:eh:schema:v1:diagnostic";
    public const string EstimateExplanation = "urn:eh:schema:v1:estimate-explanation";
    public const string EstimateReport = "urn:eh:schema:v1:estimate-report";
    public const string EstimateView = "urn:eh:schema:v1:estimate-view";
    public const string HostReviewAdjustment = "urn:eh:schema:v1:host-review-adjustment";
    public const string HostReviewBenchmark = "urn:eh:schema:v1:host-review-benchmark";
    public const string HostReviewMeasurement = "urn:eh:schema:v1:host-review-measurement";
    public const string HostReviewPacket = "urn:eh:schema:v1:host-review-packet";
    public const string HostReviewQueryResult = "urn:eh:schema:v1:host-review-query-result";
    public const string HostReviewValidation = "urn:eh:schema:v1:host-review-validation";
    public const string RateCard = "urn:eh:schema:v1:rate-card";
    public const string RateCardModel = "urn:eh:schema:v1:rate-card-model";
    public const string RepositoryEvidence = "urn:eh:schema:v1:repository-evidence";
    public const string RepositoryScanCache = "urn:eh:schema:v1:repository-scan-cache";
    public const string SeedRuleModel = "urn:eh:schema:v1:seed-rule-model";
    public const string WorkItem = "urn:eh:schema:v1:work-item";
}

public static class EvidenceKinds
{
    public const string Accessibility = "accessibility";
    public const string ApiSurface = "api-surface";
    public const string BackgroundWork = "background-work";
    public const string BuildConfiguration = "build-configuration";
    public const string CiConfiguration = "ci-configuration";
    public const string Component = "component";
    public const string ContainerConfiguration = "container-configuration";
    public const string Coverage = "coverage";
    public const string DataAccess = "data-access";
    public const string DeliveryAutomation = "delivery-automation";
    public const string Documentation = "documentation";
    public const string DotNetProject = "dotnet-project";
    public const string DotNetSolution = "dotnet-solution";
    public const string DotNetTest = "dotnet-test";
    public const string EntryPoint = "entry-point";
    public const string EcosystemPackage = "ecosystem-package";
    public const string EcosystemTest = "ecosystem-test";
    public const string ExcludedContent = "excluded-content";
    public const string File = "file";
    public const string Infrastructure = "infrastructure";
    public const string Integration = "integration";
    public const string JavaScriptPackage = "javascript-package";
    public const string JavaScriptConfiguration = "javascript-configuration";
    public const string JavaScriptTest = "javascript-test";
    public const string JavaScriptWorkspace = "javascript-workspace";
    public const string JupyterNotebook = "jupyter-notebook";
    public const string Language = "language";
    public const string PackageReference = "package-reference";
    public const string ProjectReference = "project-reference";
    public const string RepositoryInventory = "repository-inventory";
    public const string SecurityConfiguration = "security-configuration";
    public const string SqlArtifact = "sql-artifact";
    public const string SqlDelivery = "sql-delivery";
    public const string SqlRepository = "sql-repository";
    public const string SqlTest = "sql-test";
    public const string SourceStructure = "source-structure";
    public const string TestSuite = "test-suite";
    public const string TerraformArtifact = "terraform-artifact";
    public const string TerraformRepository = "terraform-repository";
    public const string UserInterface = "user-interface";
    public const string Validation = "validation";
}
