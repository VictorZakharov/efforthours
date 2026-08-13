using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class LogicalCandidateTransformer
{
    public static EstimateReport Transform(
        EstimateReport source,
        RepositoryEvidence evidence,
        LogicalCandidateModel model)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(model);
        ValidateModel(model);
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
        foreach (LogicalCapabilityGroup capability in LogicalCandidateScorer.Build(source, evidence))
        {
            PointKey pointKey = new(capability.Kind, capability.LogicalSizeBand);
            if (!points.TryGetValue(pointKey, out LogicalCandidatePointFactor? point))
            {
                fallbackCount++;
                AddFallback(transformed, capability, "point group is outside the frozen table");
                continue;
            }

            decimal expected = capability.LogicalExpected * point.Factor;
            RangeKey rangeKey = new(capability.Kind, LogicalCandidateScorer.SizeBand(expected));
            if (!ranges.TryGetValue(rangeKey, out LogicalCandidateRangeFactor? range))
            {
                fallbackCount++;
                AddFallback(transformed, capability, "range group is outside the frozen table");
                continue;
            }

            decimal[] lows = Allocate(expected * range.LowFactor, capability.Items);
            decimal[] expecteds = Allocate(expected, capability.Items);
            decimal[] highs = Allocate(expected * range.HighFactor, capability.Items);
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
                    Reason =
                        $"Candidate '{model.CandidateId}' derived {capability.LogicalExpected} logical hours " +
                        $"for capability '{capability.Id}' using scorer '{model.Point.ScorerVersion}', " +
                        $"scope-role factor {capability.ScopeRoleFactor}, frozen point group " +
                        $"'{capability.Kind}/{capability.LogicalSizeBand}' factor {point.Factor}, and frozen " +
                        $"range group '{capability.Kind}/{range.ExpectedSizeBand}' factors " +
                        $"{range.LowFactor}/1/{range.HighFactor}. The stable seed part '{item.Id}' receives " +
                        "its proportional share.",
                    Estimator = new EstimatorReference
                    {
                        Id = "candidate-logical-capability",
                        Version = "0.1.0",
                        Kind = EstimatorKind.Rule,
                    },
                });
            }
        }

        WorkItem[] workItems = [.. source.WorkItems.Select(item => transformed[item.Id])];
        EffortRange total = ContractValidation.Sum(workItems.Select(item => item.Hours));
        return source with
        {
            EstimatorVersion = model.EstimatorVersion,
            TotalEffort = total,
            TotalCost = ProjectCost(source.RateCard, total),
            Categories = BuildCategories(workItems, total),
            WorkItems = workItems,
            Assumptions =
            [
                .. source.Assumptions,
                "Development-only fitted logical-capability candidate; this estimate is not admitted for product use.",
                $"{fallbackCount} capability group(s) used the complete seed fallback.",
            ],
        };
    }

    internal static void ValidateModel(LogicalCandidateModel model)
    {
        if (model.ModelVersion != LogicalCandidateModelFitter.ModelVersion ||
            model.Id != LogicalCandidateModelFitter.ModelId ||
            model.CandidateId != LogicalCandidateModelFitter.CandidateId ||
            model.EstimatorVersion != LogicalCandidateModelFitter.EstimatorVersion ||
            model.BaselineEstimatorVersion != LogicalCandidateModelFitter.BaselineEstimatorVersion ||
            model.FeatureContractVersion != LogicalCandidateModelFitter.FeatureContractVersion ||
            model.Point.ScorerVersion != LogicalCandidateScorer.Version ||
            model.Point.MinimumFactor != LogicalCandidateModelFitter.MinimumFactor ||
            model.Point.MaximumFactor != LogicalCandidateModelFitter.MaximumFactor ||
            model.Range.LowerQuantile != LogicalCandidateModelFitter.LowerQuantile ||
            model.Range.UpperQuantile != LogicalCandidateModelFitter.UpperQuantile ||
            model.Range.MinimumExactGroupSamples != LogicalCandidateModelFitter.MinimumRangeSamples ||
            model.LicenseExpression != "MIT")
        {
            throw new InvalidDataException("Logical-candidate model identity or frozen configuration differs.");
        }

        if (model.Point.Factors.Count == 0 ||
            model.Point.Factors.Any(factor =>
                factor.Factor < model.Point.MinimumFactor ||
                factor.Factor > model.Point.MaximumFactor) ||
            model.Point.Factors.Select(factor => new PointKey(
                    factor.WorkItemKind,
                    factor.LogicalSizeBand))
                .Distinct().Count() != model.Point.Factors.Count ||
            model.Range.Factors.Count == 0 ||
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

    private static void AddFallback(
        Dictionary<string, WorkItem> output,
        LogicalCapabilityGroup capability,
        string reason)
    {
        foreach (WorkItem item in capability.Items)
        {
            output.Add(item.Id, item with
            {
                Reason =
                    $"Candidate '{LogicalCandidateModelFitter.CandidateId}' used the complete " +
                    $"'{LogicalCandidateModelFitter.BaselineEstimatorVersion}' fallback for " +
                    $"capability '{capability.Id}' because its {reason}; {item.Reason}",
            });
        }
    }

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

    private static CategoryEstimate[] BuildCategories(
        IReadOnlyList<WorkItem> workItems,
        EffortRange total)
    {
        CategoryEstimate[] categories =
        [
            .. workItems.GroupBy(item => item.Category)
                .OrderBy(group => group.Key)
                .Select(group => new CategoryEstimate
                {
                    Category = group.Key,
                    Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
                }),
        ];
        if (categories.Length == 0)
        {
            return categories;
        }

        EffortRange beforeLast = ContractValidation.Sum(
            categories[..^1].Select(category => category.Hours));
        categories[^1] = categories[^1] with
        {
            Hours = new EffortRange
            {
                Low = total.Low - beforeLast.Low,
                Expected = total.Expected - beforeLast.Expected,
                High = total.High - beforeLast.High,
            },
        };
        return categories;
    }

    private static CostRange? ProjectCost(RateCard? rateCard, EffortRange hours) =>
        rateCard is null
            ? null
            : new CostRange
            {
                Low = RoundMoney(hours.Low * rateCard.HourlyRate),
                Expected = RoundMoney(hours.Expected * rateCard.HourlyRate),
                High = RoundMoney(hours.High * rateCard.HourlyRate),
                Currency = rateCard.Currency,
            };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private readonly record struct PointKey(string Kind, string SizeBand);

    private readonly record struct RangeKey(string Kind, string SizeBand);
}
