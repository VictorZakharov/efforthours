using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Estimation;
using Fairbill.Reporting;

namespace Fairbill.Tests;

public sealed class ReportRendererTests
{
    private readonly EstimateReport _report = new SeedEstimator().Estimate(
        TestRepositoryEvidence.Create(),
        EstimationProfile.Implementation);

    [Fact]
    public void JsonRendererProducesSchemaValidOutput()
    {
        string json = new JsonReportRenderer().Render(_report);

        SchemaValidationResult reportResult = ContractSchemaValidator.Validate(
            SchemaNames.EstimateReport,
            json);
        SchemaValidationResult itemResult = ContractSchemaValidator.Validate(
            SchemaNames.WorkItem,
            ContractJson.Serialize(_report.WorkItems[0]));
        SchemaValidationResult diagnosticResult = ContractSchemaValidator.Validate(
            SchemaNames.Diagnostic,
            ContractJson.Serialize(_report.Diagnostics[0]));

        Assert.True(reportResult.IsValid, string.Join(Environment.NewLine, reportResult.Errors));
        Assert.True(itemResult.IsValid, string.Join(Environment.NewLine, itemResult.Errors));
        Assert.True(diagnosticResult.IsValid, string.Join(Environment.NewLine, diagnosticResult.Errors));
    }

    [Fact]
    public void JsonRendererProducesSchemaValidRateCard()
    {
        RateCard rate = new()
        {
            Id = "test-rate",
            Name = "Test rate",
            Currency = "USD",
            HourlyRate = 125m,
            EffectiveDate = new DateOnly(2026, 1, 1),
            Methodology = "Test methodology",
            SourceUri = new Uri("https://example.test/rate"),
        };

        SchemaValidationResult result = ContractSchemaValidator.Validate(
            SchemaNames.RateCard,
            ContractJson.Serialize(rate));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void MarkdownRendererIncludesDisclaimerEvidenceAndDiagnostics()
    {
        string markdown = new MarkdownReportRenderer().Render(_report);

        Assert.Contains("counterfactual replacement effort", markdown, StringComparison.Ordinal);
        Assert.Contains("component:billing", markdown, StringComparison.Ordinal);
        Assert.Contains("FB1000", markdown, StringComparison.Ordinal);
        Assert.Contains("| Human-hours | 8 | 14 | 24.5 |", markdown, StringComparison.Ordinal);
    }
}
