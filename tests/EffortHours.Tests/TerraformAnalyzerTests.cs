using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class TerraformAnalyzerTests
{
    private const string Analyzer = "efforthours.terraform-analyzer";

    [Fact]
    public async Task TerraformModulesProduceBoundedSemanticAndEffortEvidence()
    {
        InMemoryRepository repository = RichRepository();

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact infrastructure = AnalyzerFact(evidence, EvidenceKinds.Infrastructure, ".");
        EvidenceFact integration = AnalyzerFact(evidence, EvidenceKinds.Integration, ".");
        EvidenceFact security = AnalyzerFact(evidence, EvidenceKinds.SecurityConfiguration, ".");
        EvidenceFact validation = AnalyzerFact(evidence, EvidenceKinds.Validation, ".");
        EvidenceFact tests = AnalyzerFact(evidence, EvidenceKinds.EcosystemTest, ".");

        Assert.Contains("analysis-status:analyzed", Language(evidence, "terraform").Tags);
        Assert.Contains("analysis-depth:token-backed", Language(evidence, "hcl").Tags);
        Assert.Equal("0.1.0", infrastructure.Provenance.AnalyzerVersion);
        Assert.Equal(2m, Measurement(infrastructure, "resources"));
        Assert.Equal(2m, Measurement(infrastructure, "distinct-resource-types"));
        Assert.Equal(1m, Measurement(infrastructure, "data-sources"));
        Assert.Equal(2m, Measurement(infrastructure, "module-calls"));
        Assert.Equal(1m, Measurement(infrastructure, "variables"));
        Assert.Equal(1m, Measurement(infrastructure, "outputs"));
        Assert.True(Measurement(infrastructure, "local-values") >= 1m);
        Assert.True(Measurement(infrastructure, "providers") >= 1m);
        Assert.True(Measurement(infrastructure, "backends") >= 1m);
        Assert.True(Measurement(infrastructure, "lifecycle-blocks") >= 1m);
        Assert.True(Measurement(infrastructure, "dynamic-blocks") >= 1m);
        Assert.True(Measurement(infrastructure, "dependency-expressions") >= 1m);
        Assert.True(Measurement(infrastructure, "expression-complexity-units") >= 1m);
        Assert.True(Measurement(infrastructure, "infrastructure-units") > 1m);
        Assert.Contains("technology:terraform-provider-aws", integration.Tags);
        Assert.Contains("technology:terraform-backend-s3", integration.Tags);
        Assert.Contains("technology:terraform-module-registry", integration.Tags);
        Assert.True(Measurement(security, "security-surfaces") >= 1m);
        Assert.True(Measurement(validation, "validation-rules") >= 1m);
        Assert.Equal(1m, Measurement(tests, "test-cases"));
        Assert.True(Measurement(tests, "assertions") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Documentation &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BuildConfiguration &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DeliveryAutomation &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Tags.Contains("reference:resolved", StringComparer.Ordinal));
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.CiCdAndInfrastructureAsCode);
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.SecurityAndAccessibility);
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.IntegrationContractAndComponentTesting);
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task ExternalAndDynamicModulesStayUnresolvedWithoutSourceDisclosure()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "main.tf",
            """
            module "registry" {
              source = "example/network/aws"
            }
            module "git" {
              source = "git::https://example.invalid/private.git"
            }
            module "dynamic" {
              source = var.module_source
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);
        EvidenceFact integration = AnalyzerFact(evidence, EvidenceKinds.Integration, ".");

        Assert.Contains("technology:terraform-module-registry", integration.Tags);
        Assert.Contains("technology:terraform-module-git", integration.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8603");
        Assert.DoesNotContain("example.invalid", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private.git", json, StringComparison.Ordinal);
        Assert.DoesNotContain("module_source", json, StringComparison.Ordinal);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference);
    }

    [Fact]
    public async Task GenericHclRemainsVisibleWithoutGuessedTerraformUnits()
    {
        InMemoryRepository repository = new();
        repository.WriteText("service.nomad.hcl", "job \"worker\" { group \"api\" { count = 2 } }");
        repository.WriteText("custom.hcl", "application \"demo\" { endpoint = \"local\" }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Equal(2, evidence.Facts.Count(fact => fact.Kind == EvidenceKinds.TerraformArtifact));
        Assert.All(evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.TerraformArtifact), fact =>
            Assert.Contains("terraform-semantics:not-assumed", fact.Tags));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.Infrastructure &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8603");
    }

    [Fact]
    public async Task NestedTerraformBlockNamesakesDoNotBecomeTopLevelResources()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "main.tf",
            "application \"local\" { resource \"aws_s3_bucket\" \"namesake\" {} }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact infrastructure = AnalyzerFact(evidence, EvidenceKinds.Infrastructure, ".");

        Assert.Equal(0m, Measurement(infrastructure, "resources"));
        Assert.Equal(0m, Measurement(infrastructure, "distinct-resource-types"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Provenance.Analyzer == Analyzer);
    }

    [Fact]
    public async Task ExactDuplicatesAndRepeatedResourcesAreDiminishing()
    {
        const string one = "resource \"aws_s3_bucket\" \"a\" { bucket = \"a\" }\n";
        string repeated = string.Concat(Enumerable.Range(0, 12).Select(index =>
            $"resource \"aws_s3_bucket\" \"b{index}\" {{ bucket = \"b{index}\" }}\n"));
        InMemoryRepository single = new();
        single.WriteText("main.tf", one);
        InMemoryRepository duplicate = new();
        duplicate.WriteText("main.tf", one);
        duplicate.WriteText("copy.tf", one);
        InMemoryRepository many = new();
        many.WriteText("main.tf", repeated);

        RepositoryEvidence singleEvidence = await ScanAsync(single);
        RepositoryEvidence duplicateEvidence = await ScanAsync(duplicate);
        RepositoryEvidence manyEvidence = await ScanAsync(many);
        decimal singleUnits = InfrastructureUnits(singleEvidence);
        decimal duplicateUnits = InfrastructureUnits(duplicateEvidence);
        decimal manyUnits = InfrastructureUnits(manyEvidence);

        Assert.Equal(singleUnits, duplicateUnits);
        Assert.Contains(duplicateEvidence.Facts, fact => fact.Kind == EvidenceKinds.ExcludedContent &&
            fact.Provenance.Analyzer == Analyzer &&
            fact.Tags.Contains("classification:exact-duplicate", StringComparer.Ordinal));
        Assert.True(manyUnits > singleUnits);
        Assert.True(manyUnits < singleUnits * 12m);
    }

    [Fact]
    public async Task MixedRepositoryKeepsTerraformAndSourceAnalyzersIndependent()
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteText("Program.cs", "internal static class Program { public static void Main() { } }");
        repository.WriteText("main.tf", "resource \"aws_s3_bucket\" \"assets\" { bucket = \"assets\" }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.dotnet-analyzer");
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.ProductionImplementation);
        Assert.Contains(estimate.Categories, category =>
            category.Category == EffortCategory.CiCdAndInfrastructureAsCode);
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    private static InMemoryRepository RichRepository()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "main.tf",
            """
            terraform {
              backend "s3" { bucket = "state" }
              required_providers { aws = { source = "hashicorp/aws" } }
            }
            provider "aws" { region = var.region }
            variable "region" {
              description = "Deployment region"
              type = string
              validation {
                condition = length(var.region) > 2
                error_message = "invalid"
              }
            }
            locals { name = "${var.region}-service" }
            data "aws_caller_identity" "current" {}
            resource "aws_s3_bucket" "assets" {
              bucket = local.name
              lifecycle { prevent_destroy = true }
              depends_on = [data.aws_caller_identity.current]
              dynamic "cors_rule" {
                for_each = var.rules
                content { allowed_methods = ["GET"] }
              }
            }
            resource "aws_iam_policy" "access" { policy = "{}" }
            module "network" { source = "./modules/network" }
            module "external" { source = "example/network/aws" }
            output "bucket" { description = "Bucket name" value = aws_s3_bucket.assets.id sensitive = true }
            """);
        repository.WriteText("modules/network/main.tf", "resource \"aws_vpc\" \"main\" { cidr_block = \"10.0.0.0/16\" }\n");
        repository.WriteText(
            "main.tftest.hcl",
            """
            run "plan" {
              command = plan
              assert {
                condition = output.bucket != ""
                error_message = "missing"
              }
            }
            """);
        repository.WriteText("production.tfvars", "region = \"ca-central-1\"\n");
        repository.WriteText(".terraformrc", "plugin_cache_dir = \"./cache\"\n");
        return repository;
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EvidenceFact AnalyzerFact(RepositoryEvidence evidence, string kind, string scope) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind && fact.Scope == scope &&
            fact.Provenance.Analyzer == Analyzer);

    private static EvidenceFact Language(RepositoryEvidence evidence, string language) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains($"language:{language}", StringComparer.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static decimal InfrastructureUnits(RepositoryEvidence evidence) => evidence.Facts
        .Where(fact => fact.Kind == EvidenceKinds.Infrastructure && fact.Provenance.Analyzer == Analyzer)
        .Sum(fact => Measurement(fact, "infrastructure-units"));
}
