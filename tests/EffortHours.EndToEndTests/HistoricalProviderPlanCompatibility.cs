using System.Text.Json.Nodes;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    private static void AssertHistoricalPlanCompatibility(ChangePortfolioComparisonReport report, string json)
    {
        JsonNode root = JsonNode.Parse(json)!;
        JsonObject plan = root["discovery"]!["providerDiagnostics"]!["historicalPullRequests"]!.AsObject();
        foreach (string name in new[] { "inventoryStrategy", "inventoryComplete", "metadataComplete", "inventoryQueryCount", "headerQueryCount",
            "metadataQueryCount", "headerBatchCount", "headerFallbackCount", "cacheWriteCount", "resumeState" }) plan.Remove(name);
        string older = root.ToJsonString();
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, older).IsValid);
        Assert.Empty(ContractValidation.Validate(ContractJson.Deserialize<ChangePortfolioComparisonReport>(older)));
        var discovery = report.Discovery!;
        var diagnostics = discovery.ProviderDiagnostics!;
        var original = diagnostics.HistoricalPullRequests!;
        foreach (var invalid in new[]
        {
            original with { InventoryComplete = false }, original with { MetadataComplete = false },
            original with { InventoryComplete = null }, original with { HeaderQueryCount = -1 },
            original with { CacheWriteCount = original.CompletedCount + 1 }
        })
            Assert.NotEmpty(ContractValidation.Validate(report with
            { Discovery = discovery with { ProviderDiagnostics = diagnostics with { HistoricalPullRequests = invalid } } }));
    }
}
