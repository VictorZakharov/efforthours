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
    public const string WorkItem = "work-item.schema.json";
}

public static class SchemaIds
{
    public const string Diagnostic = "urn:fairbill:schema:v1:diagnostic";
    public const string EstimateReport = "urn:fairbill:schema:v1:estimate-report";
    public const string RateCard = "urn:fairbill:schema:v1:rate-card";
    public const string RepositoryEvidence = "urn:fairbill:schema:v1:repository-evidence";
    public const string RepositoryScanCache = "urn:fairbill:schema:v1:repository-scan-cache";
    public const string WorkItem = "urn:fairbill:schema:v1:work-item";
}

public static class EvidenceKinds
{
    public const string BuildConfiguration = "build-configuration";
    public const string CiConfiguration = "ci-configuration";
    public const string Component = "component";
    public const string ContainerConfiguration = "container-configuration";
    public const string Coverage = "coverage";
    public const string Documentation = "documentation";
    public const string ExcludedContent = "excluded-content";
    public const string File = "file";
    public const string Infrastructure = "infrastructure";
    public const string Integration = "integration";
    public const string Language = "language";
    public const string RepositoryInventory = "repository-inventory";
    public const string TestSuite = "test-suite";
}
