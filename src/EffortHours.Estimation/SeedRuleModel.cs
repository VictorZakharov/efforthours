using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

public sealed record SeedRuleCatalogInfo
{
    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string EstimatorVersion { get; init; }

    public required string Status { get; init; }

    public required int TechnologyBaselineYear { get; init; }

    public required string Sha256 { get; init; }
}

public static class SeedRuleCatalog
{
    private const string ResourceName = "EffortHours.Estimation.Models.SeedRules.0.2.1.json";

    private static readonly Lazy<LoadedSeedRuleCatalog> Loaded = new(
        Load,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static SeedRuleCatalogInfo Info => Loaded.Value.Info;

    public static string ReadJson() => Loaded.Value.Json;

    internal static SeedRuleModel Model => Loaded.Value.Model;

    private static LoadedSeedRuleCatalog Load()
    {
        Assembly assembly = typeof(SeedRuleCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded seed-rule model '{ResourceName}' was not found.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        string json = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(bytes);

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.SeedRuleModel,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The embedded seed-rule model does not satisfy its schema: " +
                string.Join(" ", schema.Errors));
        }

        SeedRuleModel model = ContractJson.Deserialize<SeedRuleModel>(json);
        List<string> semanticErrors = Validate(model);
        if (semanticErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The embedded seed-rule model is semantically invalid: " +
                string.Join(" ", semanticErrors));
        }

        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        SeedRuleCatalogInfo info = new()
        {
            Id = model.Id,
            Version = model.Version,
            EstimatorVersion = $"{model.Id}/{model.Version}",
            Status = model.Status,
            TechnologyBaselineYear = model.TechnologyBaselineYear,
            Sha256 = $"sha256:{sha256}",
        };
        return new LoadedSeedRuleCatalog(model, json, info);
    }

    private static List<string> Validate(SeedRuleModel model)
    {
        List<string> errors = [];
        if (!string.Equals(model.SchemaVersion, ContractVersions.V1, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported model schema version '{model.SchemaVersion}'.");
        }

        if (model.ItemSizing.MinimumExpectedHours > model.ItemSizing.MaximumExpectedHours)
        {
            errors.Add("Minimum expected item hours cannot exceed the maximum.");
        }

        if (model.ItemSizing.TargetExpectedHours < model.ItemSizing.MinimumExpectedHours ||
            model.ItemSizing.TargetExpectedHours > model.ItemSizing.MaximumExpectedHours)
        {
            errors.Add("Target expected item hours must be between the minimum and maximum.");
        }

        if (model.ItemSizing.RoundingIncrementHours > model.ItemSizing.MinimumExpectedHours)
        {
            errors.Add("The rounding increment cannot exceed the minimum expected item size.");
        }

        if (model.ComplexityFactors.Routine > model.ComplexityFactors.Moderate ||
            model.ComplexityFactors.Moderate > model.ComplexityFactors.High ||
            model.ComplexityFactors.High > model.ComplexityFactors.Exceptional)
        {
            errors.Add("Complexity factors must be nondecreasing.");
        }

        HashSet<string> ruleIds = new(StringComparer.Ordinal);
        foreach (SeedRuleDefinition rule in model.Rules)
        {
            if (!ruleIds.Add(rule.Id))
            {
                errors.Add($"Seed rule ID '{rule.Id}' is duplicated.");
            }

            if (rule.LowFactor > 1m || rule.HighFactor < 1m)
            {
                errors.Add($"Seed rule '{rule.Id}' must bracket expected effort.");
            }

            HashSet<string> driverNames = new(StringComparer.Ordinal);
            foreach (SeedRuleDriver driver in rule.Drivers)
            {
                if (!driverNames.Add(driver.Name))
                {
                    errors.Add($"Seed rule '{rule.Id}' repeats driver '{driver.Name}'.");
                }

                decimal previousLimit = 0m;
                for (int index = 0; index < driver.Tiers.Count; index++)
                {
                    SeedMarginalTier tier = driver.Tiers[index];
                    bool isLast = index == driver.Tiers.Count - 1;
                    if (tier.UpTo is null && !isLast)
                    {
                        errors.Add(
                            $"Seed rule '{rule.Id}' driver '{driver.Name}' has an unbounded non-final tier.");
                    }

                    if (tier.UpTo is not null)
                    {
                        if (tier.UpTo.Value <= previousLimit)
                        {
                            errors.Add(
                                $"Seed rule '{rule.Id}' driver '{driver.Name}' tier limits must increase.");
                        }

                        previousLimit = tier.UpTo.Value;
                    }
                }

                if (driver.Tiers[^1].UpTo is not null)
                {
                    errors.Add(
                        $"Seed rule '{rule.Id}' driver '{driver.Name}' must end with an unbounded tier.");
                }
            }
        }

        return errors;
    }

    private sealed record LoadedSeedRuleCatalog(
        SeedRuleModel Model,
        string Json,
        SeedRuleCatalogInfo Info);
}

internal sealed record SeedRuleModel
{
    public required string SchemaVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Status { get; init; }

    public required string Description { get; init; }

    public required int TechnologyBaselineYear { get; init; }

    public required SeedItemSizing ItemSizing { get; init; }

    public required SeedComplexityFactors ComplexityFactors { get; init; }

    public required SeedProvenanceConfidence ProvenanceConfidence { get; init; }

    public IReadOnlyList<SeedRuleDefinition> Rules { get; init; } = [];
}

internal sealed record SeedItemSizing
{
    public required decimal MinimumExpectedHours { get; init; }

    public required decimal TargetExpectedHours { get; init; }

    public required decimal MaximumExpectedHours { get; init; }

    public required decimal RoundingIncrementHours { get; init; }
}

internal sealed record SeedComplexityFactors
{
    public required decimal Routine { get; init; }

    public required decimal Moderate { get; init; }

    public required decimal High { get; init; }

    public required decimal Exceptional { get; init; }

    public decimal For(ComplexityLevel complexity) => complexity switch
    {
        ComplexityLevel.Routine => Routine,
        ComplexityLevel.Moderate => Moderate,
        ComplexityLevel.High => High,
        ComplexityLevel.Exceptional => Exceptional,
        _ => throw new ArgumentOutOfRangeException(nameof(complexity)),
    };
}

internal sealed record SeedProvenanceConfidence
{
    public required decimal Observed { get; init; }

    public required decimal Measured { get; init; }

    public required decimal DeclaredAssumed { get; init; }

    public required decimal Inferred { get; init; }

    public decimal For(EvidenceSourceKind sourceKind) => sourceKind switch
    {
        EvidenceSourceKind.Observed => Observed,
        EvidenceSourceKind.Measured => Measured,
        EvidenceSourceKind.DeclaredAssumed => DeclaredAssumed,
        EvidenceSourceKind.Inferred => Inferred,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceKind)),
    };
}

internal sealed record SeedRuleDefinition
{
    public required string Id { get; init; }

    public required EffortCategory Category { get; init; }

    public required string QuantityUnit { get; init; }

    public required decimal SetupHours { get; init; }

    public required decimal LowFactor { get; init; }

    public required decimal HighFactor { get; init; }

    public required decimal DefaultConfidence { get; init; }

    public IReadOnlyList<SeedRuleDriver> Drivers { get; init; } = [];
}

internal sealed record SeedRuleDriver
{
    public required string Name { get; init; }

    public IReadOnlyList<SeedMarginalTier> Tiers { get; init; } = [];
}

internal sealed record SeedMarginalTier
{
    public decimal? UpTo { get; init; }

    public required decimal HoursPerUnit { get; init; }
}
