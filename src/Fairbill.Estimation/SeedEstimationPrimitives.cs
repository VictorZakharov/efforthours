using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fairbill.Contracts.V1;

namespace Fairbill.Estimation;

internal sealed record CapabilityUnit
{
    public required string Id { get; init; }

    public required string RuleId { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public required decimal Quantity { get; init; }

    public IReadOnlyDictionary<string, decimal> Drivers { get; init; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    public required ComplexityLevel Complexity { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<EvidenceFact> Evidence { get; init; } = [];

    public IReadOnlyList<EstimationProfile> Profiles { get; init; } = [];

    public string? CorrelationGroup { get; init; }

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Exclusions { get; init; } = [];

    public IReadOnlyList<string> UncertaintyReasons { get; init; } = [];

    public decimal ConfidencePenalty { get; init; }
}

internal sealed class SeedWorkItemFactory(SeedRuleModel model)
{
    private readonly SeedRuleModel _model = model;
    private readonly Dictionary<string, SeedRuleDefinition> _rules = model.Rules
        .ToDictionary(rule => rule.Id, StringComparer.Ordinal);

    public IReadOnlyList<WorkItem> Create(CapabilityUnit capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (capability.Quantity <= 0m)
        {
            throw new InvalidOperationException(
                $"Capability '{capability.Id}' has a non-positive quantity.");
        }

        if (capability.Evidence.Count == 0)
        {
            throw new InvalidOperationException(
                $"Capability '{capability.Id}' has no evidence lineage.");
        }

        SeedRuleDefinition rule = GetRule(capability.RuleId);
        decimal expected = EvaluateExpected(capability, rule);
        decimal confidence = CalculateConfidence(capability, rule);
        (decimal lowFactor, decimal highFactor) = CalculateRangeFactors(rule, confidence);
        int partCount = decimal.ToInt32(decimal.Ceiling(
            expected / _model.ItemSizing.TargetExpectedHours));
        decimal[] expectedParts = DistributeExpected(expected, partCount);
        decimal[] quantityParts = DistributeQuantity(capability.Quantity, partCount);
        string evidenceDescription = DescribeDrivers(capability, rule);
        string[] uncertaintyReasons = Normalize(
        [
            .. capability.UncertaintyReasons,
            .. AutomaticUncertaintyReasons(capability),
        ]);
        string[] evidenceIds = [.. capability.Evidence
            .Select(fact => fact.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        EstimatorReference estimator = new()
        {
            Id = $"seed-rule:{rule.Id}",
            Version = _model.Version,
            Kind = EstimatorKind.Rule,
        };

        List<WorkItem> items = new(partCount);
        string identity = StableToken($"{capability.RuleId}\n{capability.Scope}\n{capability.Id}");
        for (int index = 0; index < partCount; index++)
        {
            decimal partExpected = expectedParts[index];
            EffortRange hours = new()
            {
                Low = RoundToIncrement(partExpected * lowFactor),
                Expected = partExpected,
                High = RoundToIncrement(partExpected * highFactor),
            };
            if (hours.Low > hours.Expected)
            {
                hours = hours with { Low = hours.Expected };
            }

            if (hours.High < hours.Expected)
            {
                hours = hours with { High = hours.Expected };
            }

            string title = partCount == 1
                ? capability.Title
                : $"{capability.Title} (part {index + 1} of {partCount})";
            items.Add(new WorkItem
            {
                Id = $"work:{rule.Id}:{identity}:part-{index + 1:D4}",
                Category = rule.Category,
                Title = title,
                Scope = capability.Scope,
                EvidenceIds = evidenceIds,
                Quantity = quantityParts[index],
                Complexity = capability.Complexity,
                Hours = hours,
                Confidence = confidence,
                Reason = $"{capability.Reason} {evidenceDescription}",
                Estimator = estimator,
                Profiles = capability.Profiles,
                CorrelationGroup = capability.CorrelationGroup,
                Assumptions = Normalize(capability.Assumptions),
                Exclusions = Normalize(capability.Exclusions),
                UncertaintyReasons = uncertaintyReasons,
            });
        }

        return items;
    }

    public decimal EvaluateExpected(CapabilityUnit capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return EvaluateExpected(capability, GetRule(capability.RuleId));
    }

    private decimal EvaluateExpected(
        CapabilityUnit capability,
        SeedRuleDefinition rule)
    {
        HashSet<string> knownDrivers = rule.Drivers
            .Select(driver => driver.Name)
            .ToHashSet(StringComparer.Ordinal);
        string? unknownDriver = capability.Drivers.Keys
            .FirstOrDefault(driver => !knownDrivers.Contains(driver));
        if (unknownDriver is not null)
        {
            throw new InvalidOperationException(
                $"Capability '{capability.Id}' supplies unknown driver '{unknownDriver}' " +
                $"to rule '{rule.Id}'.");
        }

        decimal expected = rule.SetupHours;
        foreach (SeedRuleDriver driver in rule.Drivers)
        {
            decimal quantity = capability.Drivers.GetValueOrDefault(driver.Name);
            if (quantity < 0m)
            {
                throw new InvalidOperationException(
                    $"Capability '{capability.Id}' has a negative '{driver.Name}' driver.");
            }

            expected += EvaluateDriver(quantity, driver);
        }

        expected *= _model.ComplexityFactors.For(capability.Complexity);
        expected = RoundToIncrement(expected);
        return decimal.Max(expected, _model.ItemSizing.MinimumExpectedHours);
    }

    private static decimal EvaluateDriver(decimal quantity, SeedRuleDriver driver)
    {
        decimal effort = 0m;
        decimal previousLimit = 0m;
        decimal remaining = quantity;
        foreach (SeedMarginalTier tier in driver.Tiers)
        {
            if (remaining <= 0m)
            {
                break;
            }

            decimal tierCapacity = tier.UpTo is null
                ? remaining
                : tier.UpTo.Value - previousLimit;
            decimal tierQuantity = decimal.Min(remaining, tierCapacity);
            effort += tierQuantity * tier.HoursPerUnit;
            remaining -= tierQuantity;
            if (tier.UpTo is not null)
            {
                previousLimit = tier.UpTo.Value;
            }
        }

        return effort;
    }

    private decimal CalculateConfidence(
        CapabilityUnit capability,
        SeedRuleDefinition rule)
    {
        decimal provenance = capability.Evidence
            .Select(fact => _model.ProvenanceConfidence.For(fact.Provenance.SourceKind))
            .Average();
        decimal penalty = capability.ConfidencePenalty;
        if (capability.Evidence.Any(fact => fact.Tags.Any(tag => tag is
            "syntax:token-backed" or "syntax:token-fallback")))
        {
            penalty += 0.08m;
        }

        if (capability.Evidence.Any(fact => fact.Tags.Contains(
            "locations:truncated",
            StringComparer.Ordinal)))
        {
            penalty += 0.04m;
        }

        decimal confidence = ((rule.DefaultConfidence + provenance) / 2m) - penalty;
        return decimal.Round(decimal.Clamp(confidence, 0.2m, 0.95m), 2);
    }

    private static (decimal Low, decimal High) CalculateRangeFactors(
        SeedRuleDefinition rule,
        decimal confidence)
    {
        decimal widening = (1m - confidence) * 0.25m;
        decimal low = decimal.Max(0.35m, rule.LowFactor - widening);
        decimal high = rule.HighFactor + widening;
        return (low, high);
    }

    private static IEnumerable<string> AutomaticUncertaintyReasons(CapabilityUnit capability)
    {
        if (capability.Evidence.Any(fact => fact.Provenance.SourceKind == EvidenceSourceKind.Inferred))
        {
            yield return "Some supporting evidence is inferred rather than directly observed.";
        }

        if (capability.Evidence.Any(fact => fact.Tags.Any(tag => tag is
            "syntax:token-backed" or "syntax:token-fallback")))
        {
            yield return "Some TypeScript or fallback structure is token-backed rather than parser-backed.";
        }

        if (capability.Evidence.Any(fact => fact.Tags.Contains(
            "locations:truncated",
            StringComparer.Ordinal)))
        {
            yield return "The analyzer truncated locations for at least one aggregate fact.";
        }
    }

    private string DescribeDrivers(
        CapabilityUnit capability,
        SeedRuleDefinition rule)
    {
        string drivers = string.Join(
            ", ",
            rule.Drivers
                .Where(driver => capability.Drivers.GetValueOrDefault(driver.Name) > 0m)
                .Select(driver =>
                    $"{driver.Name}={capability.Drivers[driver.Name].ToString("0.####", CultureInfo.InvariantCulture)}"));
        string driverText = drivers.Length == 0 ? "setup-only" : drivers;
        decimal factor = _model.ComplexityFactors.For(capability.Complexity);
        return $"Rule '{rule.Id}' uses {driverText}; complexity " +
            $"'{ToKebabCase(capability.Complexity)}' applies factor " +
            $"{factor.ToString("0.##", CultureInfo.InvariantCulture)}.";
    }

    private decimal[] DistributeExpected(decimal total, int partCount)
    {
        decimal increment = _model.ItemSizing.RoundingIncrementHours;
        int totalUnits = decimal.ToInt32(total / increment);
        int baseUnits = totalUnits / partCount;
        int remainder = totalUnits % partCount;
        decimal[] parts = new decimal[partCount];
        for (int index = 0; index < partCount; index++)
        {
            parts[index] = (baseUnits + (index < remainder ? 1 : 0)) * increment;
            if (parts[index] < _model.ItemSizing.MinimumExpectedHours ||
                parts[index] > _model.ItemSizing.MaximumExpectedHours)
            {
                throw new InvalidOperationException(
                    "Seed work-item partitioning produced an out-of-range expected value.");
            }
        }

        return parts;
    }

    private static decimal[] DistributeQuantity(decimal total, int partCount)
    {
        decimal[] parts = new decimal[partCount];
        decimal apportioned = 0m;
        for (int index = 0; index < partCount - 1; index++)
        {
            parts[index] = decimal.Round(
                total / partCount,
                4,
                MidpointRounding.AwayFromZero);
            apportioned += parts[index];
        }

        parts[^1] = total - apportioned;
        if (parts.Any(part => part <= 0m))
        {
            // Quantity is explanatory rather than an effort bound. Preserve a
            // positive deterministic share even for very small decimal inputs.
            decimal even = decimal.Round(
                total / partCount,
                8,
                MidpointRounding.AwayFromZero);
            Array.Fill(parts, even);
            parts[^1] = total - even * (partCount - 1);
        }

        return parts;
    }

    private decimal RoundToIncrement(decimal value)
    {
        decimal increment = _model.ItemSizing.RoundingIncrementHours;
        return decimal.Round(
            value / increment,
            0,
            MidpointRounding.AwayFromZero) * increment;
    }

    private SeedRuleDefinition GetRule(string id) =>
        _rules.TryGetValue(id, out SeedRuleDefinition? rule)
            ? rule
            : throw new InvalidOperationException($"Seed-rule model does not define '{id}'.");

    private static string[] Normalize(IEnumerable<string> values) =>
    [
        .. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private static string StableToken(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 10)).ToLowerInvariant();
    }

    private static string ToKebabCase<T>(T value)
        where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}
