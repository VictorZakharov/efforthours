using System.Text.Json;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Tests;

public sealed class ContractSchemaTests
{
    [Fact]
    public void CatalogContainsAllV1SchemasWithUniqueIds()
    {
        Assert.Equal(13, ContractSchemaCatalog.Names.Count);
        Assert.Contains(SchemaNames.CalibrationCorpus, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.CalibrationEvaluation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.CalibrationValidation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.EstimateExplanation, ContractSchemaCatalog.Names);
        Assert.Contains(SchemaNames.EstimateView, ContractSchemaCatalog.Names);
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
