using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateTransformer
{
    private const string LegacyModelVersion = "repository-logical-capability-model/0.1.0";
    private const string LegacyCandidateId = "logical-capability/0.1.0";
    private const string LegacyEstimatorVersion =
        "candidate-logical-capability/0.1.0+seed-rules/0.4.0";
    private const string LegacyFeatureContractVersion = "logical-capability-features/1.0.0";
    private const string RetiredModelVersion = "repository-logical-capability-model/0.2.0";
    private const string RetiredCandidateId = "logical-capability/0.2.0";
    private const string RetiredEstimatorVersion =
        "candidate-logical-capability/0.2.0+seed-rules/0.4.0";
    private const string RetiredFeatureContractVersion = "logical-capability-features/1.1.0";

    public static EstimateReport Transform(
        EstimateReport source,
        RepositoryEvidence evidence,
        LogicalCandidateModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(model);
        ValidateModel(model);
        bool current = HasCurrentIdentity(model);
        if (source.EstimatorVersion != model.BaselineEstimatorVersion ||
            source.Profile != EstimationProfile.Implementation)
        {
            throw new InvalidDataException(
                $"Candidate '{model.CandidateId}' supports implementation-profile " +
                $"'{model.BaselineEstimatorVersion}' reports only.");
        }

        Dictionary<PointKey, LogicalCandidatePointFactor> points = model.Point.Factors.ToDictionary(
            factor => new PointKey(factor.WorkItemKind, factor.LogicalSizeBand));
        Dictionary<RangeKey, LogicalCandidateRangeFactor> ranges = model.Range.Factors.ToDictionary(
            factor => new RangeKey(factor.WorkItemKind, factor.ExpectedSizeBand));
        Dictionary<string, WorkItem> transformed = new(StringComparer.Ordinal);
        int fallbackCount = 0;
        foreach (LogicalCapabilityGroup capability in LogicalCandidateScorer.Build(
                     source,
                     evidence,
                     model.Point.ScorerVersion,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            PointKey pointKey = new(capability.Kind, capability.LogicalSizeBand);
            if (!points.TryGetValue(pointKey, out LogicalCandidatePointFactor? point))
            {
                fallbackCount++;
                AddFallback(
                    transformed,
                    capability,
                    model,
                    "point group is outside the frozen table");
                continue;
            }

            decimal seedAnchor = capability.LogicalExpected <=
                    model.Point.SeedAnchorMaximumLogicalHours &&
                model.Point.SeedAnchorWorkItemKinds.Contains(
                    capability.Kind,
                    StringComparer.Ordinal)
                ? capability.SeedExpected * model.Point.SeedAnchorFactor
                : 0m;
            decimal residualExpected = capability.LogicalExpected * point.Factor;
            decimal expected = seedAnchor + residualExpected;
            RangeKey rangeKey = new(capability.Kind, LogicalCandidateScorer.SizeBand(expected));
            if (!ranges.TryGetValue(rangeKey, out LogicalCandidateRangeFactor? range))
            {
                fallbackCount++;
                AddFallback(
                    transformed,
                    capability,
                    model,
                    "range group is outside the frozen table");
                continue;
            }

            decimal fittedLow = seedAnchor + residualExpected * range.LowFactor;
            decimal configuredLow = model.Range.MinimumLowHours
                .Where(item => item.WorkItemKind == capability.Kind)
                .Select(item => item.Hours * capability.ScopeRoleFactor)
                .SingleOrDefault();
            decimal low = decimal.Min(expected, decimal.Max(fittedLow, configuredLow));
            decimal[] lows = Allocate(low, capability.Items);
            decimal[] expecteds = Allocate(expected, capability.Items);
            decimal[] highs = Allocate(
                seedAnchor + residualExpected * range.HighFactor,
                capability.Items);
            for (int index = 0; index < capability.Items.Count; index++)
            {
                WorkItem item = capability.Items[index];
                transformed.Add(item.Id, item with
                {
                    Hours = new EffortRange
                    {
                        Low = lows[index],
                        Expected = expecteds[index],
                        High = highs[index],
                    },
                    Reason = current
                        ? $"Candidate '{model.CandidateId}' derived {capability.LogicalExpected} logical hours " +
                          $"for capability '{capability.Id}' using scorer '{model.Point.ScorerVersion}', " +
                          $"scope-role factor {capability.ScopeRoleFactor}, seed anchor {seedAnchor}, frozen " +
                          $"point group '{capability.Kind}/{capability.LogicalSizeBand}' factor {point.Factor} " +
                          $"from '{point.SampleSource}', low floor {configuredLow}, and frozen range group " +
                          $"'{capability.Kind}/{range.ExpectedSizeBand}' factors " +
                          $"{range.LowFactor}/1/{range.HighFactor}. The stable seed part '{item.Id}' receives " +
                          "its proportional share."
                        : $"Candidate '{model.CandidateId}' derived {capability.LogicalExpected} logical hours " +
                          $"for capability '{capability.Id}' using scorer '{model.Point.ScorerVersion}', " +
                          $"scope-role factor {capability.ScopeRoleFactor}, frozen point group " +
                          $"'{capability.Kind}/{capability.LogicalSizeBand}' factor {point.Factor}, and frozen " +
                          $"range group '{capability.Kind}/{range.ExpectedSizeBand}' factors " +
                          $"{range.LowFactor}/1/{range.HighFactor}. The stable seed part '{item.Id}' receives " +
                          "its proportional share.",
                    Estimator = new EstimatorReference
                    {
                        Id = "candidate-logical-capability",
                        Version = CandidateVersion(model.CandidateId),
                        Kind = EstimatorKind.Rule,
                    },
                });
            }
        }

        WorkItem[] workItems = [.. source.WorkItems.Select(item => transformed[item.Id])];
        return CandidateEstimateProjection.Create(
            source,
            model.EstimatorVersion,
            workItems,
            [
                "Development-only fitted logical-capability candidate; this estimate is not admitted for product use.",
                $"{fallbackCount} capability group(s) used the complete seed fallback.",
            ]);
    }

    internal static void ValidateModel(LogicalCandidateModel model)
    {
        bool legacy = HasLegacyIdentity(model);
        bool retired = HasRetiredIdentity(model);
        bool current = HasCurrentIdentity(model);
        string expectedScorer = current
            ? LogicalCandidateScorer.Version
            : LogicalCandidateScorer.LegacyVersion;
        if ((!legacy && !retired && !current) ||
            model.Point.ScorerVersion != expectedScorer ||
            model.Point.MinimumFactor != LogicalCandidateModelFitter.MinimumFactor ||
            model.Point.MaximumFactor != LogicalCandidateModelFitter.MaximumFactor ||
            model.Range.LowerQuantile != LogicalCandidateModelFitter.LowerQuantile ||
            model.Range.UpperQuantile != (current
                ? LogicalCandidateModelFitter.UpperQuantile
                : LogicalCandidateModelFitter.RetiredUpperQuantile) ||
            model.Range.MinimumHighFactor != (current
                ? LogicalCandidateModelFitter.MinimumHighFactor
                : 0m) ||
            model.Range.MinimumExactGroupSamples != LogicalCandidateModelFitter.MinimumRangeSamples ||
            model.LicenseExpression != "MIT")
        {
            throw new InvalidDataException("Logical-candidate model identity or frozen configuration differs.");
        }

        bool validOverrides = legacy
            ? model.Point.MaximumFactorOverrides.Count == 0
            : model.Point.MaximumFactorOverrides.Count == 1 &&
              model.Point.MaximumFactorOverrides[0].WorkItemKind ==
                  "specification-comprehension" &&
              model.Point.MaximumFactorOverrides[0].MaximumFactor ==
                  LogicalCandidateModelFitter.SpecificationComprehensionMaximumFactor;
        if (!validOverrides)
        {
            throw new InvalidDataException(
                "Logical-candidate point-factor ceilings differ from the frozen configuration.");
        }

        bool validAnchor = current
            ? model.Point.SeedAnchorFactor == LogicalCandidateModelFitter.SeedAnchorFactor &&
              model.Point.SeedAnchorMaximumLogicalHours ==
                  LogicalCandidateModelFitter.SeedAnchorMaximumLogicalHours &&
              model.Point.SeedAnchorWorkItemKinds.SequenceEqual(
                  LogicalCandidateModelFitter.SeedAnchorWorkItemKinds,
                  StringComparer.Ordinal)
            : model.Point.SeedAnchorFactor == 0m &&
              model.Point.SeedAnchorMaximumLogicalHours == 0m &&
              model.Point.SeedAnchorWorkItemKinds.Count == 0;
        if (!validAnchor)
        {
            throw new InvalidDataException(
                "Logical-candidate seed-anchor configuration differs from the frozen version.");
        }

        bool validLowMinimums = current
            ? model.Range.MinimumLowHours.Count == 2 &&
              model.Range.MinimumLowHours.Any(item =>
                  item.WorkItemKind == "data-persistence" &&
                  item.Hours == LogicalCandidateModelFitter.DataPersistenceMinimumLowHours) &&
              model.Range.MinimumLowHours.Any(item =>
                  item.WorkItemKind == "external-integration" &&
                  item.Hours == LogicalCandidateModelFitter.ExternalIntegrationMinimumLowHours)
            : model.Range.MinimumLowHours.Count == 0;
        if (!validLowMinimums)
        {
            throw new InvalidDataException(
                "Logical-candidate range minimums differ from the frozen version.");
        }

        Dictionary<string, decimal> maximumFactors = model.Point.MaximumFactorOverrides
            .ToDictionary(item => item.WorkItemKind, item => item.MaximumFactor, StringComparer.Ordinal);
        if (model.Point.Factors.Count == 0 ||
            model.Point.Factors.Any(factor =>
                factor.Factor < model.Point.MinimumFactor ||
                factor.Factor > maximumFactors.GetValueOrDefault(
                    factor.WorkItemKind,
                    model.Point.MaximumFactor)) ||
            model.Point.Factors.Select(factor => new PointKey(
                    factor.WorkItemKind,
                    factor.LogicalSizeBand))
                .Distinct().Count() != model.Point.Factors.Count ||
            (current && model.Point.Factors.GroupBy(
                    factor => factor.WorkItemKind,
                    StringComparer.Ordinal)
                .Any(group => group.Count() != LogicalCandidateScorer.SizeBands.Length)) ||
            model.Range.Factors.Count == 0 ||
            model.Range.MinimumLowHours.Any(item => item.Hours < 0m) ||
            model.Range.MinimumLowHours.Select(item => item.WorkItemKind)
                .Distinct(StringComparer.Ordinal).Count() != model.Range.MinimumLowHours.Count ||
            model.Range.Factors.Any(factor =>
                factor.LowFactor < 0m ||
                factor.LowFactor > 1m ||
                factor.HighFactor < 1m) ||
            model.Range.Factors.Select(factor => new RangeKey(
                    factor.WorkItemKind,
                    factor.ExpectedSizeBand))
                .Distinct().Count() != model.Range.Factors.Count)
        {
            throw new InvalidDataException("Logical-candidate model tables are empty, duplicated, or out of bounds.");
        }
    }

    private static bool HasLegacyIdentity(LogicalCandidateModel model) =>
        model.ModelVersion == LegacyModelVersion &&
        model.Id == LogicalCandidateModelFitter.ModelId &&
        model.CandidateId == LegacyCandidateId &&
        model.EstimatorVersion == LegacyEstimatorVersion &&
        model.BaselineEstimatorVersion == LogicalCandidateModelFitter.BaselineEstimatorVersion &&
        model.FeatureContractVersion == LegacyFeatureContractVersion;

    private static bool HasCurrentIdentity(LogicalCandidateModel model) =>
        model.ModelVersion == LogicalCandidateModelFitter.ModelVersion &&
        model.Id == LogicalCandidateModelFitter.ModelId &&
        model.CandidateId == LogicalCandidateModelFitter.CandidateId &&
        model.EstimatorVersion == LogicalCandidateModelFitter.EstimatorVersion &&
        model.BaselineEstimatorVersion == LogicalCandidateModelFitter.BaselineEstimatorVersion &&
        model.FeatureContractVersion == LogicalCandidateModelFitter.FeatureContractVersion;

    private static bool HasRetiredIdentity(LogicalCandidateModel model) =>
        model.ModelVersion == RetiredModelVersion &&
        model.Id == LogicalCandidateModelFitter.ModelId &&
        model.CandidateId == RetiredCandidateId &&
        model.EstimatorVersion == RetiredEstimatorVersion &&
        model.BaselineEstimatorVersion == LogicalCandidateModelFitter.BaselineEstimatorVersion &&
        model.FeatureContractVersion == RetiredFeatureContractVersion;

    private static void AddFallback(
        Dictionary<string, WorkItem> output,
        LogicalCapabilityGroup capability,
        LogicalCandidateModel model,
        string reason)
    {
        foreach (WorkItem item in capability.Items)
        {
            output.Add(item.Id, item with
            {
                Reason =
                    $"Candidate '{model.CandidateId}' used the complete " +
                    $"'{model.BaselineEstimatorVersion}' fallback for " +
                    $"capability '{capability.Id}' because its {reason}; {item.Reason}",
            });
        }
    }

    private static string CandidateVersion(string candidateId) =>
        candidateId[(candidateId.IndexOf('/', StringComparison.Ordinal) + 1)..];

    private static decimal[] Allocate(decimal total, IReadOnlyList<WorkItem> items)
    {
        decimal[] result = new decimal[items.Count];
        if (total == 0m || items.Count == 0)
        {
            return result;
        }

        decimal weight = items.Sum(item => item.Hours.Expected);
        decimal assigned = 0m;
        for (int index = 0; index < items.Count - 1; index++)
        {
            result[index] = weight == 0m
                ? total / items.Count
                : total * items[index].Hours.Expected / weight;
            assigned += result[index];
        }

        result[^1] = total - assigned;
        return result;
    }

    private readonly record struct PointKey(string Kind, string SizeBand);

    private readonly record struct RangeKey(string Kind, string SizeBand);
}
