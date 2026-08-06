using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Estimation;

namespace Fairbill.Tests;

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
        Assert.Equal("0.2.1", info.Version);
        Assert.Equal(SeedEstimator.Version, info.EstimatorVersion);
        Assert.Equal("experimental-uncalibrated", info.Status);
        Assert.Equal(2026, info.TechnologyBaselineYear);
        Assert.StartsWith("sha256:", info.Sha256, StringComparison.Ordinal);
        Assert.Equal(71, info.Sha256.Length);
    }
}
