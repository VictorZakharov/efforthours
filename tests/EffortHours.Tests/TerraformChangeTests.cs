using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class TerraformChangeTests
{
    [Fact]
    public async Task WhitespaceOnlyHclChangeHasZeroEffort()
    {
        const string before = "resource \"aws_s3_bucket\" \"assets\" {\n  bucket=\"assets\"\n}\n";
        const string after = "resource   \"aws_s3_bucket\"   \"assets\" {\n    bucket = \"assets\"\n}\n";

        ChangeEstimateReport report = await EstimateAsync(State(("main.tf", before)), State(("main.tf", after)));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData(
        "main.tf",
        "resource \"aws_s3_bucket\" \"assets\" { bucket = \"before\" }\n",
        "resource \"aws_s3_bucket\" \"assets\" { bucket = \"after\" }\n")]
    [InlineData(
        "main.tf",
        "locals { policy = <<EOF\nbefore\nEOF\n}\n",
        "locals { policy = <<EOF\nafter\nEOF\n}\n")]
    public async Task LiteralAndHeredocChangesRemainMeaningful(string path, string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(State((path, before)), State((path, after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task UnterminatedHeredocFailsClosed()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("main.tf", "locals { value = <<EOF\none\n")),
            State(("main.tf", "locals { value = <<EOF\n  one\n")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Theory]
    [InlineData("main.tf", "resource \"aws_s3_bucket\" \"assets\" {}\n", EffortCategory.CiCdAndInfrastructureAsCode)]
    [InlineData("main.tftest.hcl", "run \"plan\" {\n  assert {\n    condition = true\n    error_message = \"x\"\n  }\n}\n", EffortCategory.IntegrationContractAndComponentTesting)]
    [InlineData(".terraformrc", "plugin_cache_dir = \"./cache\"\n", EffortCategory.BuildConfigurationAndDeveloperTooling)]
    public async Task AddedHclArtifactsReachTheirIntendedCategory(
        string path,
        string content,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(State(), State((path, content)));

        Assert.Contains(report.Categories, candidate =>
            candidate.Category == category && candidate.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.17.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task TerraformSemanticCategoriesRemainSeparateInChangeMode()
    {
        const string content = """
            provider "aws" { region = "ca-central-1" }
            resource "aws_iam_policy" "access" { policy = "{}" }
            variable "token" { sensitive = true }
            """;

        ChangeEstimateReport report = await EstimateAsync(State(), State(("main.tf", content)));

        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.CiCdAndInfrastructureAsCode);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.SecurityAndAccessibility);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-terraform-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
