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

public static class SchemaNames
{
    public const string CalibrationAuthoringPacket = "calibration-authoring-packet.schema.json";
    public const string CalibrationCorpus = "calibration-corpus.schema.json";
    public const string CalibrationCorpusReviewPacket = "calibration-corpus-review-packet.schema.json";
    public const string CalibrationCorpusReviewPlan = "calibration-corpus-review-plan.schema.json";
    public const string CalibrationEvaluation = "calibration-evaluation.schema.json";
    public const string CalibrationMutationReport = "calibration-mutation-report.schema.json";
    public const string CalibrationMutationSuite = "calibration-mutation-suite.schema.json";
    public const string CalibrationReviewPlan = "calibration-review-plan.schema.json";
    public const string CalibrationValidation = "calibration-validation.schema.json";
    public const string ChangeEstimateExplanation = "change-estimate-explanation.schema.json";
    public const string ChangeEstimateReport = "change-estimate-report.schema.json";
    public const string ChangeEvidence = "change-evidence.schema.json";
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
    public const string CalibrationEvaluation = "urn:eh:schema:v1:calibration-evaluation";
    public const string CalibrationMutationReport = "urn:eh:schema:v1:calibration-mutation-report";
    public const string CalibrationMutationSuite = "urn:eh:schema:v1:calibration-mutation-suite";
    public const string CalibrationReviewPlan = "urn:eh:schema:v1:calibration-review-plan";
    public const string CalibrationValidation = "urn:eh:schema:v1:calibration-validation";
    public const string ChangeEstimateExplanation = "urn:eh:schema:v1:change-estimate-explanation";
    public const string ChangeEstimateReport = "urn:eh:schema:v1:change-estimate-report";
    public const string ChangeEvidence = "urn:eh:schema:v1:change-evidence";
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
    public const string ApiSurface = "api-surface";
    public const string BackgroundWork = "background-work";
    public const string BuildConfiguration = "build-configuration";
    public const string CiConfiguration = "ci-configuration";
    public const string Component = "component";
    public const string ContainerConfiguration = "container-configuration";
    public const string Coverage = "coverage";
    public const string DataAccess = "data-access";
    public const string Documentation = "documentation";
    public const string DotNetProject = "dotnet-project";
    public const string DotNetSolution = "dotnet-solution";
    public const string DotNetTest = "dotnet-test";
    public const string EntryPoint = "entry-point";
    public const string ExcludedContent = "excluded-content";
    public const string File = "file";
    public const string Infrastructure = "infrastructure";
    public const string Integration = "integration";
    public const string JavaScriptPackage = "javascript-package";
    public const string JavaScriptConfiguration = "javascript-configuration";
    public const string JavaScriptTest = "javascript-test";
    public const string JavaScriptWorkspace = "javascript-workspace";
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
    public const string UserInterface = "user-interface";
    public const string Validation = "validation";
}
