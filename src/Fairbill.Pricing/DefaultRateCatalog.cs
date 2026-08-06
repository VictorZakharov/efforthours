using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Pricing;

public sealed record DefaultRateCatalogInfo
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Status { get; init; }

    public required DateOnly EffectiveDate { get; init; }

    public required string Currency { get; init; }

    public required decimal HourlyRate { get; init; }

    public required RateRange MarketRange { get; init; }

    public required string Sha256 { get; init; }
}

public static class DefaultRateCatalog
{
    private const string ResourceName =
        "Fairbill.Pricing.Rates.UsSeniorContractor.2026.1.json";

    private static readonly Lazy<LoadedRateCatalog> Loaded = new(
        Load,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static DefaultRateCatalogInfo Info => Loaded.Value.Info;

    public static RateCard RateCard => Loaded.Value.RateCard;

    public static string ReadJson() => Loaded.Value.Json;

    private static LoadedRateCatalog Load()
    {
        Assembly assembly = typeof(DefaultRateCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded rate-card model '{ResourceName}' was not found.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        string json = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(bytes);

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RateCardModel,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The embedded rate-card model does not satisfy its schema: " +
                string.Join(" ", schema.Errors));
        }

        RateCardModel model = ContractJson.Deserialize<RateCardModel>(json);
        string[] semanticErrors = [.. Validate(model)];
        if (semanticErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "The embedded rate-card model is semantically invalid: " +
                string.Join(" ", semanticErrors));
        }

        RateCard rateCard = new()
        {
            Id = $"{model.Id}/{model.Version}",
            Name = model.Name,
            Currency = model.Currency,
            HourlyRate = model.HourlyRate,
            MarketRange = model.MarketRange,
            EffectiveDate = model.EffectiveDate,
            Methodology =
                $"BLS May 2025 OEWS SOC 15-1252 75th-percentile wage " +
                $"(${ExpectedPoint(model).WagePerHour:0.00}), multiplied by the March 2026 " +
                $"ECEC professional compensation/wage ratio " +
                $"({model.Formula.TotalCompensationPerHour:0.00}/" +
                $"{model.Formula.WagesAndSalariesPerHour:0.00}), divided by Fairbill's " +
                $"{model.Formula.BillableUtilization:P0} billable-utilization assumption, " +
                $"and rounded to ${model.Formula.RoundingIncrement:0.##}/hour.",
            SourceUri = model.Sources[0].Uri,
        };

        IReadOnlyList<string> rateErrors = ContractValidation.Validate(rateCard);
        if (rateErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The embedded rate-card model produced an invalid rate card: " +
                string.Join(" ", rateErrors));
        }

        SchemaValidationResult rateSchema = ContractSchemaValidator.Validate(
            SchemaNames.RateCard,
            ContractJson.Serialize(rateCard));
        if (!rateSchema.IsValid)
        {
            throw new InvalidOperationException(
                "The embedded rate-card model produced a schema-invalid rate card: " +
                string.Join(" ", rateSchema.Errors));
        }

        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        DefaultRateCatalogInfo info = new()
        {
            Id = model.Id,
            Version = model.Version,
            Status = model.Status,
            EffectiveDate = model.EffectiveDate,
            Currency = model.Currency,
            HourlyRate = model.HourlyRate,
            MarketRange = model.MarketRange,
            Sha256 = $"sha256:{sha256}",
        };

        return new LoadedRateCatalog(json, rateCard, info);
    }

    private static IEnumerable<string> Validate(RateCardModel model)
    {
        if (!string.Equals(model.SchemaVersion, ContractVersions.V1, StringComparison.Ordinal))
        {
            yield return $"Unsupported schema version '{model.SchemaVersion}'.";
        }

        if (!string.Equals(model.Currency, "USD", StringComparison.Ordinal))
        {
            yield return "The bundled nationwide US rate must use USD.";
        }

        decimal calculatedLoad =
            model.Formula.TotalCompensationPerHour /
            model.Formula.WagesAndSalariesPerHour;
        if (decimal.Abs(calculatedLoad - model.Formula.CompensationLoad) > 0.00000001m)
        {
            yield return "The stored compensation load does not reproduce its inputs.";
        }

        IGrouping<string, RateWagePoint>? repeatedLabel = model.WagePoints
            .GroupBy(point => point.Label, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (repeatedLabel is not null)
        {
            yield return $"Wage point label '{repeatedLabel.Key}' is repeated.";
            yield break;
        }

        Dictionary<string, RateWagePoint> points = model.WagePoints
            .ToDictionary(point => point.Label, StringComparer.Ordinal);
        if (!points.TryGetValue("low", out RateWagePoint? lowPoint) ||
            !points.TryGetValue("expected", out RateWagePoint? expectedPoint) ||
            !points.TryGetValue("high", out RateWagePoint? highPoint))
        {
            yield return "Wage points must contain unique low, expected, and high labels.";
            yield break;
        }

        foreach (RateWagePoint point in model.WagePoints)
        {
            decimal raw = point.WagePerHour * calculatedLoad /
                model.Formula.BillableUtilization;
            decimal rounded = RoundToIncrement(raw, model.Formula.RoundingIncrement);
            if (decimal.Abs(raw - point.RawBillRate) > 0.0001m)
            {
                yield return $"Wage point '{point.Label}' does not reproduce its raw bill rate.";
            }

            if (rounded != point.RoundedBillRate)
            {
                yield return $"Wage point '{point.Label}' does not reproduce its rounded bill rate.";
            }
        }

        if (lowPoint.RoundedBillRate != model.MarketRange.Low ||
            expectedPoint.RoundedBillRate != model.MarketRange.Expected ||
            highPoint.RoundedBillRate != model.MarketRange.High ||
            expectedPoint.RoundedBillRate != model.HourlyRate)
        {
            yield return "Published rates do not match the derived wage points.";
        }

        if (model.Sources.Any(source => source.Uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return "Bundled rate sources must use HTTPS URIs.";
        }
    }

    private static RateWagePoint ExpectedPoint(RateCardModel model) =>
        model.WagePoints.Single(point => point.Label == "expected");

    private static decimal RoundToIncrement(decimal value, decimal increment) =>
        decimal.Round(
            value / increment,
            0,
            MidpointRounding.AwayFromZero) * increment;

    private sealed record LoadedRateCatalog(
        string Json,
        RateCard RateCard,
        DefaultRateCatalogInfo Info);
}

internal sealed record RateCardModel
{
    public required string SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Status { get; init; }

    public required string Name { get; init; }

    public required string Currency { get; init; }

    public required DateOnly EffectiveDate { get; init; }

    public required string Geography { get; init; }

    public required string Role { get; init; }

    public required decimal HourlyRate { get; init; }

    public required RateRange MarketRange { get; init; }

    public required RateFormula Formula { get; init; }

    public IReadOnlyList<RateWagePoint> WagePoints { get; init; } = [];

    public IReadOnlyList<RateSource> Sources { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];
}

internal sealed record RateFormula
{
    public required string Expression { get; init; }

    public required decimal TotalCompensationPerHour { get; init; }

    public required decimal WagesAndSalariesPerHour { get; init; }

    public required decimal CompensationLoad { get; init; }

    public required decimal BillableUtilization { get; init; }

    public required decimal RoundingIncrement { get; init; }
}

internal sealed record RateWagePoint
{
    public required string Label { get; init; }

    public required int Percentile { get; init; }

    public required string SeriesId { get; init; }

    public required decimal WagePerHour { get; init; }

    public required decimal RawBillRate { get; init; }

    public required decimal RoundedBillRate { get; init; }
}

internal sealed record RateSource
{
    public required string Id { get; init; }

    public required string Publisher { get; init; }

    public required string Title { get; init; }

    public required DateOnly ReleaseDate { get; init; }

    public required Uri Uri { get; init; }

    public required string License { get; init; }
}
