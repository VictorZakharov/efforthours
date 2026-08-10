using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class SeedRuleCatalogTests
{
    [Fact]
    public void BundledCatalogIsSchemaValidAndVersioned()
    {
        string json = SeedRuleCatalog.ReadJson();
        SchemaValidationResult validation = ContractSchemaValidator.Validate(
            SchemaNames.SeedRuleModel,
            json);
        SeedRuleCatalogInfo info = SeedRuleCatalog.Info;

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal("seed-rules", info.Id);
        Assert.Equal("0.3.0", info.Version);
        Assert.Equal(SeedEstimator.Version, info.EstimatorVersion);
        Assert.Equal("experimental-uncalibrated", info.Status);
        Assert.Equal(2026, info.TechnologyBaselineYear);
        Assert.StartsWith("sha256:", info.Sha256, StringComparison.Ordinal);
        Assert.Equal(71, info.Sha256.Length);

        using JsonDocument document = JsonDocument.Parse(json);
        IEnumerable<JsonElement> rules = document.RootElement
            .GetProperty("rules")
            .EnumerateArray();
        JsonElement uiRule = Assert.Single(
            rules,
            rule => rule.GetProperty("id").GetString() == "ui-surface");
        string?[] drivers = [.. uiRule.GetProperty("drivers")
            .EnumerateArray()
            .Select(driver => driver.GetProperty("name").GetString())];
        Assert.DoesNotContain("asset-lines", drivers);
        Assert.Contains("template-structure-units", drivers);
        Assert.Contains("template-binding-units", drivers);
        Assert.Contains("style-structure-units", drivers);
        Assert.Contains("responsive-surfaces", drivers);
        Assert.Contains("design-token-units", drivers);
        Assert.Contains("animation-theme-surfaces", drivers);
    }
}
