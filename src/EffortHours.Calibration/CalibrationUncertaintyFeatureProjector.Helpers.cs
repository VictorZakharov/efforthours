using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyFeatureProjector
{
    private static CalibrationUncertaintyFeatureSummary BuildSummary(
        CalibrationUncertaintyWorkItemFeatures[] workItems,
        CalibrationUncertaintyFeatureContract contract,
        HashSet<string> widthDriverIds) => new()
        {
            WorkItemCount = workItems.Length,
            FeatureCount = contract.Features.Count,
            WidthDriverFeatureCount = widthDriverIds.Count,
            DeferredCandidateCount = contract.DeferredCandidates.Count,
            ResolvedEvidenceReferenceCount = workItems.Sum(item => item.ResolvedEvidenceIds.Count),
            UnresolvedEvidenceReferenceCount = workItems.Sum(item => item.UnresolvedEvidenceIds.Count),
            WidthDriverUnavailableWorkItemCount = workItems.Count(item => item.Features.Any(feature =>
                widthDriverIds.Contains(feature.FeatureId) &&
                feature.Availability == CalibrationUncertaintyFeatureAvailability.Unavailable)),
            MaterialAccessGapWorkItemCount = CountPositive(
                workItems,
                CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount),
            NonMaterialOfflineLimitationWorkItemCount = CountPositive(
                workItems,
                CalibrationUncertaintyFeatureIds.NonMaterialOfflineLimitationCount),
            SourcePolicyCompliantWorkItemCount =
                workItems.Count(item => item.SourceRangePolicyCompliant),
        };

    private static int CountPositive(
        IEnumerable<CalibrationUncertaintyWorkItemFeatures> workItems,
        string featureId) => workItems.Count(item => item.Features.Any(feature =>
            feature.FeatureId == featureId && feature.Value > 0m));

    private static decimal? ParserRisk(EvidenceFact fact)
    {
        if (fact.Tags.Any(tag => tag is "parser-confidence:low" or "syntax:token-fallback"))
        {
            return 3m;
        }

        if (fact.Tags.Contains("syntax:token-backed", StringComparer.Ordinal))
        {
            return 2m;
        }

        if (fact.Tags.Any(tag =>
            tag == "parser-confidence:medium" ||
            tag.StartsWith("parser:bounded-", StringComparison.Ordinal)))
        {
            return 1m;
        }

        return fact.Tags.Any(tag => tag is "parser-confidence:high" or "syntax:parser-backed")
            ? 0m
            : null;
    }

    private static bool IsNonMaterialOfflineLimitation(string tag) =>
        NonMaterialOfflineSuffixes.Any(suffix => tag.EndsWith(suffix, StringComparison.Ordinal));

    private static bool HasDynamicBoundary(EvidenceFact fact) =>
        fact.Tags.Any(tag => tag.Contains("dynamic", StringComparison.Ordinal)) ||
        fact.Measurements.Any(measurement =>
            measurement.Value > 0m &&
            measurement.Name.Contains("dynamic", StringComparison.Ordinal));

    private static bool HasUnsupportedBoundary(EvidenceFact fact) => fact.Tags.Any(tag =>
        tag.Contains("unsupported", StringComparison.Ordinal) ||
        tag.EndsWith(":unresolved", StringComparison.Ordinal) ||
        tag.EndsWith(":excluded", StringComparison.Ordinal));

    private static decimal SumMeasurements(
        IEnumerable<EvidenceFact> facts,
        params string[] names) => facts
            .SelectMany(fact => fact.Measurements)
            .Where(measurement => names.Contains(measurement.Name, StringComparer.Ordinal))
            .Sum(measurement => measurement.Value);

    private static CalibrationUncertaintyFeatureValue Available(
        string id,
        decimal value,
        IEnumerable<string>? evidenceIds = null) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.Available,
            Value = value,
            EvidenceIds = OrderIds(evidenceIds),
        };

    private static CalibrationUncertaintyFeatureValue NotApplicable(
        string id,
        string reason) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.NotApplicable,
            ReasonCode = reason,
        };

    private static CalibrationUncertaintyFeatureValue Unavailable(
        string id,
        string reason,
        IEnumerable<string>? evidenceIds = null) => new()
        {
            FeatureId = id,
            Availability = CalibrationUncertaintyFeatureAvailability.Unavailable,
            ReasonCode = reason,
            EvidenceIds = OrderIds(evidenceIds),
        };

    private static string[] OrderIds(IEnumerable<string>? values) => values is null
        ? []
        : [.. values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static decimal Round6(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
