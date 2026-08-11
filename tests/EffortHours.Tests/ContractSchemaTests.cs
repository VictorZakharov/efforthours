using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ContractSchemaTests
{
    [Fact]
    public void CatalogContainsAllV1SchemasWithUniqueIds()
    {
        Assert.Equal(30, ContractSchemaCatalog.Names.Count);
        Assert.Contains(SchemaNames.CalibrationAuthoringPacket, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.CalibrationCorpus, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.CalibrationEvaluation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.CalibrationReviewPlan, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.CalibrationValidation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.ChangeEvidence, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.ChangeEstimateExplanation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.ChangeEstimateReport, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.ChangePortfolioManifest, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.ChangePortfolioReport, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.EstimateExplanation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.EstimateView, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.HostReviewAdjustment, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.HostReviewBenchmark, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.HostReviewMeasurement, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.HostReviewPacket, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.HostReviewQueryResult, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.HostReviewValidation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.RateCardModel, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.SeedRuleModel, ContractSchemaCatalog.Names);
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (string name in ContractSchemaCatalog.Names)
        {
            using JsonDocument schema = JsonDocument.Parse(ContractSchemaCatalog.Read(name));
            string id = schema.RootElement.GetProperty("$id").GetString()!;
            string draft = schema.RootElement.GetProperty("$schema").GetString()!;

            Assert.True(ids.Add(id), $"Duplicate schema ID: {id}");
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", draft);
        }
    }
}
