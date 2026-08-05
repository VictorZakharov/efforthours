namespace Fairbill.Contracts.V1;

public static class ContractVersions
{
    public const string V1 = "1.0.0";
}

public static class SchemaNames
{
    public const string Diagnostic = "diagnostic.schema.json";
    public const string EstimateReport = "estimate-report.schema.json";
    public const string RateCard = "rate-card.schema.json";
    public const string RepositoryEvidence = "repository-evidence.schema.json";
    public const string RepositoryScanCache = "repository-scan-cache.schema.json";
    public const string SeedRuleModel = "seed-rule-model.schema.json";
    public const string WorkItem = "work-item.schema.json";
}

public static class SchemaIds
{
    public const string Diagnostic = "urn:fairbill:schema:v1:diagnostic";
    public const string EstimateReport = "urn:fairbill:schema:v1:estimate-report";
    public const string RateCard = "urn:fairbill:schema:v1:rate-card";
    public const string RepositoryEvidence = "urn:fairbill:schema:v1:repository-evidence";
    public const string RepositoryScanCache = "urn:fairbill:schema:v1:repository-scan-cache";
    public const string SeedRuleModel = "urn:fairbill:schema:v1:seed-rule-model";
    public const string WorkItem = "urn:fairbill:schema:v1:work-item";
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
    public const string SourceStructure = "source-structure";
    public const string TestSuite = "test-suite";
    public const string UserInterface = "user-interface";
    public const string Validation = "validation";
}
