using System.Text.Json;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Pricing;

namespace Fairbill.Tests;

public sealed class DefaultRateCatalogTests
{
    [Fact]
    public void BundledArtifactIsSchemaValidAndIdentifiesItsDigest()
    {
        string json = DefaultRateCatalog.ReadJson();
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RateCardModel,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Equal("us-senior-software-contractor", DefaultRateCatalog.Info.Id);
        Assert.Equal("2026.1", DefaultRateCatalog.Info.Version);
        Assert.Equal("current-methodology", DefaultRateCatalog.Info.Status);
        Assert.StartsWith("sha256:", DefaultRateCatalog.Info.Sha256, StringComparison.Ordinal);
        Assert.Equal(71, DefaultRateCatalog.Info.Sha256.Length);
    }

    [Fact]
    public void BundledRateReproducesPublishedFormulaAndMarketRange()
    {
        using JsonDocument document = JsonDocument.Parse(DefaultRateCatalog.ReadJson());
        JsonElement root = document.RootElement;
        JsonElement formula = root.GetProperty("formula");
        decimal load = formula.GetProperty("totalCompensationPerHour").GetDecimal() /
            formula.GetProperty("wagesAndSalariesPerHour").GetDecimal();
        decimal utilization = formula.GetProperty("billableUtilization").GetDecimal();
        decimal increment = formula.GetProperty("roundingIncrement").GetDecimal();

        foreach (JsonElement point in root.GetProperty("wagePoints").EnumerateArray())
        {
            decimal raw = point.GetProperty("wagePerHour").GetDecimal() * load / utilization;
            decimal rounded = decimal.Round(
                raw / increment,
                0,
                MidpointRounding.AwayFromZero) * increment;

            Assert.InRange(
                decimal.Abs(raw - point.GetProperty("rawBillRate").GetDecimal()),
                0m,
                0.0001m);
            Assert.Equal(point.GetProperty("roundedBillRate").GetDecimal(), rounded);
        }

        Assert.Equal(160m, root.GetProperty("hourlyRate").GetDecimal());
        Assert.Equal(125m, root.GetProperty("marketRange").GetProperty("low").GetDecimal());
        Assert.Equal(160m, root.GetProperty("marketRange").GetProperty("expected").GetDecimal());
        Assert.Equal(200m, root.GetProperty("marketRange").GetProperty("high").GetDecimal());
    }

    [Fact]
    public void DefaultRateCardIsSemanticallyAndSchemaValid()
    {
        RateCard rateCard = DefaultRateCatalog.RateCard;
        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(rateCard);
        string json = ContractJson.Serialize(rateCard);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RateCard,
            json);

        Assert.Empty(semanticErrors);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Equal("us-senior-software-contractor/2026.1", rateCard.Id);
        Assert.Equal("USD", rateCard.Currency);
        Assert.Equal(160m, rateCard.HourlyRate);
        Assert.Equal(new DateOnly(2026, 6, 12), rateCard.EffectiveDate);
        Assert.Equal(125m, rateCard.MarketRange!.Low);
        Assert.Equal(200m, rateCard.MarketRange.High);
    }
}
