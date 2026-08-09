using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

public static class HostReviewBenchmarkEngine
{
    public static HostReviewBenchmarkReport Build(
        IReadOnlyList<HostReviewMeasurement> measurements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        if (measurements.Count == 0)
        {
            throw new ArgumentException(
                "At least one compact/broader-source measurement pair is required.",
                nameof(measurements));
        }

        foreach (HostReviewMeasurement measurement in measurements)
        {
            IReadOnlyList<string> errors = ContractValidation.Validate(measurement);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"Measurement '{measurement.SubjectId}' is invalid: {string.Join(" ", errors)}",
                    nameof(measurements));
            }
        }

        List<HostReviewSubjectBenchmark> subjects = [];
        List<HostReviewComparisonObservation> itemObservations = [];
        List<HostReviewComparisonObservation> categoryObservations = [];
        List<HostReviewComparisonObservation> totalObservations = [];
        foreach (IGrouping<string, HostReviewMeasurement> group in measurements
            .GroupBy(measurement => measurement.SubjectId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            HostReviewMeasurement compact = RequireSingleContext(
                group,
                HostReviewContextMode.Compact);
            HostReviewMeasurement reference = RequireSingleContext(
                group,
                HostReviewContextMode.BroaderSource);
            ValidatePair(compact, reference);

            List<HostReviewComparisonObservation> subjectItems = BuildItemObservations(
                compact,
                reference);
            List<HostReviewComparisonObservation> subjectCategories = BuildCategoryObservations(
                compact,
                reference);
            HostReviewComparisonObservation subjectTotal = new(
                compact.BaselineTotal,
                compact.ReviewedTotal,
                reference.ReviewedTotal);
            itemObservations.AddRange(subjectItems);
            categoryObservations.AddRange(subjectCategories);
            totalObservations.Add(subjectTotal);

            subjects.Add(new HostReviewSubjectBenchmark
            {
                SubjectId = compact.SubjectId,
                InputDigest = compact.InputDigest,
                EstimatorVersion = compact.EstimatorVersion,
                Profile = compact.Profile,
                CompactMeasurementDigest = HostReviewPayloadMeter.Digest(compact),
                BroaderSourceMeasurementDigest = HostReviewPayloadMeter.Digest(reference),
                CapabilityItems = HostReviewComparisonMetrics.Build(
                    HostReviewComparisonLevel.CapabilityItem,
                    subjectItems),
                Categories = HostReviewComparisonMetrics.Build(
                    HostReviewComparisonLevel.Category,
                    subjectCategories),
                RepositoryTotal = HostReviewComparisonMetrics.Build(
                    HostReviewComparisonLevel.RepositoryTotal,
                    [subjectTotal]),
                Telemetry = BuildTelemetry(compact, reference),
                Limitations = BuildSubjectLimitations(compact, reference),
            });
        }

        HostReviewBenchmarkReport report = new()
        {
            SubjectCount = subjects.Count,
            MeasurementCount = measurements.Count,
            Subjects = subjects,
            CapabilityItems = HostReviewComparisonMetrics.Build(
                HostReviewComparisonLevel.CapabilityItem,
                itemObservations),
            Categories = HostReviewComparisonMetrics.Build(
                HostReviewComparisonLevel.Category,
                categoryObservations),
            RepositoryTotals = HostReviewComparisonMetrics.Build(
                HostReviewComparisonLevel.RepositoryTotal,
                totalObservations),
            BudgetDecision = new HostReviewBudgetDecision
            {
                DefaultBudgetSelected = false,
                Reason =
                    "No default host-review budget is selected by measurement version 1. " +
                    "Representative multi-model and independent-review evidence is required first.",
            },
            Limitations =
            [
                "Broader-source reviews are experiment references, not ground truth or historical labor records.",
                "Approximate tokens use ceiling(characters / 4); only caller-reported provider telemetry is exact for a provider session.",
                "Measurements omit prompts and source, so their authorization and review quality remain caller responsibilities.",
            ],
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The host-review benchmark is invalid: " + string.Join(" ", reportErrors));
        }

        return report;
    }

    private static HostReviewMeasurement RequireSingleContext(
        IEnumerable<HostReviewMeasurement> measurements,
        HostReviewContextMode context)
    {
        HostReviewMeasurement[] matches =
        [.. measurements.Where(measurement => measurement.Context == context)];
        if (matches.Length != 1)
        {
            throw new ArgumentException(
                $"Each benchmark subject requires exactly one '{context}' measurement.",
                nameof(measurements));
        }

        return matches[0];
    }

    private static void ValidatePair(
        HostReviewMeasurement compact,
        HostReviewMeasurement reference)
    {
        if (compact.InputDigest != reference.InputDigest ||
            compact.ProtocolVersion != reference.ProtocolVersion ||
            compact.EstimatorVersion != reference.EstimatorVersion ||
            compact.Profile != reference.Profile ||
            compact.PacketPayload.Digest != reference.PacketPayload.Digest ||
            compact.BaselineTotal != reference.BaselineTotal)
        {
            throw new ArgumentException(
                $"Measurement pair '{compact.SubjectId}' does not describe one identical baseline packet.");
        }

        Dictionary<string, HostReviewDecisionMeasurement> compactDecisions =
            compact.Decisions.ToDictionary(decision => decision.TargetId, StringComparer.Ordinal);
        Dictionary<string, HostReviewDecisionMeasurement> referenceDecisions =
            reference.Decisions.ToDictionary(decision => decision.TargetId, StringComparer.Ordinal);
        if (!compactDecisions.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(referenceDecisions.Keys))
        {
            throw new ArgumentException(
                $"Measurement pair '{compact.SubjectId}' does not cover the same candidates.");
        }

        foreach ((string targetId, HostReviewDecisionMeasurement decision) in compactDecisions)
        {
            HostReviewDecisionMeasurement paired = referenceDecisions[targetId];
            if (decision.BaselineCategory != paired.BaselineCategory ||
                decision.BaselineHours != paired.BaselineHours)
            {
                throw new ArgumentException(
                    $"Measurement pair '{compact.SubjectId}' has inconsistent baseline target '{targetId}'.");
            }
        }

        if (!CategoryMap(compact.BaselineCategories)
            .OrderBy(pair => pair.Key)
            .SequenceEqual(CategoryMap(reference.BaselineCategories).OrderBy(pair => pair.Key)))
        {
            throw new ArgumentException(
                $"Measurement pair '{compact.SubjectId}' has inconsistent baseline categories.");
        }
    }

    private static List<HostReviewComparisonObservation> BuildItemObservations(
        HostReviewMeasurement compact,
        HostReviewMeasurement reference)
    {
        Dictionary<string, HostReviewDecisionMeasurement> references =
            reference.Decisions.ToDictionary(decision => decision.TargetId, StringComparer.Ordinal);
        return
        [
            .. compact.Decisions
                .OrderBy(decision => decision.TargetId, StringComparer.Ordinal)
                .Select(decision => new HostReviewComparisonObservation(
                    decision.BaselineHours,
                    decision.ReviewedHours,
                    references[decision.TargetId].ReviewedHours)),
        ];
    }

    private static List<HostReviewComparisonObservation> BuildCategoryObservations(
        HostReviewMeasurement compact,
        HostReviewMeasurement reference)
    {
        Dictionary<EffortCategory, EffortRange> baseline = CategoryMap(compact.BaselineCategories);
        Dictionary<EffortCategory, EffortRange> compactReviewed = CategoryMap(compact.ReviewedCategories);
        Dictionary<EffortCategory, EffortRange> referenceReviewed = CategoryMap(reference.ReviewedCategories);
        return
        [
            .. baseline.Keys
                .Union(compactReviewed.Keys)
                .Union(referenceReviewed.Keys)
                .Order()
                .Select(category => new HostReviewComparisonObservation(
                    GetRange(baseline, category),
                    GetRange(compactReviewed, category),
                    GetRange(referenceReviewed, category))),
        ];
    }

    private static HostReviewTelemetryComparison BuildTelemetry(
        HostReviewMeasurement compact,
        HostReviewMeasurement reference)
    {
        List<string> diagnostics = [];
        decimal? observedByteRatio = null;
        decimal? approximateTokenRatio = null;
        if (compact.ObservedInputComplete && reference.ObservedInputComplete)
        {
            observedByteRatio = Ratio(
                compact.ObservedInput.Utf8Bytes,
                reference.ObservedInput.Utf8Bytes);
            approximateTokenRatio = Ratio(
                compact.ObservedInput.ApproximateTokens,
                reference.ObservedInput.ApproximateTokens);
        }
        else
        {
            diagnostics.Add(
                "Observed-input ratios are unavailable because one or both sessions lack complete observed-input accounting.");
        }

        decimal? providerRatio = Ratio(
            compact.ProviderTokens.InputTokens,
            reference.ProviderTokens.InputTokens);
        if (providerRatio is null)
        {
            diagnostics.Add("Provider input-token ratio is unavailable because one or both sessions lack compatible telemetry.");
        }

        decimal? elapsedRatio = Ratio(
            compact.Elapsed.Milliseconds,
            reference.Elapsed.Milliseconds);
        if (elapsedRatio is null)
        {
            diagnostics.Add("Elapsed-time ratio is unavailable because one or both sessions lack measured duration.");
        }

        decimal? costRatio = null;
        if (compact.Cost.Amount.HasValue && reference.Cost.Amount is > 0m &&
            compact.Cost.Currency == reference.Cost.Currency)
        {
            costRatio = Round4(compact.Cost.Amount.Value / reference.Cost.Amount.Value);
        }
        else
        {
            diagnostics.Add("Monetary-cost ratio is unavailable because cost is missing, zero, or uses incompatible currencies.");
        }

        return new HostReviewTelemetryComparison
        {
            Compact = ContextTelemetry(compact),
            BroaderSource = ContextTelemetry(reference),
            ObservedInputByteRatio = observedByteRatio,
            ApproximateInputTokenRatio = approximateTokenRatio,
            ProviderInputTokenRatio = providerRatio,
            ElapsedTimeRatio = elapsedRatio,
            MonetaryCostRatio = costRatio,
            Diagnostics = diagnostics,
        };
    }

    private static HostReviewContextTelemetry ContextTelemetry(HostReviewMeasurement measurement) =>
        new()
        {
            ObservedInput = measurement.ObservedInput,
            ObservedInputComplete = measurement.ObservedInputComplete,
            QueryCount = measurement.Queries.Count,
            SelectedSourceQueryCount = measurement.Queries.Count(query =>
                query.Kind == HostReviewQueryKind.SelectedSource),
            ProviderTokens = measurement.ProviderTokens,
            Elapsed = measurement.Elapsed,
            Cost = measurement.Cost,
        };

    private static string[] BuildSubjectLimitations(
        HostReviewMeasurement compact,
        HostReviewMeasurement reference)
    {
        List<string> limitations = [];
        if (compact.Conditions.BroaderSourceAvailableBeforeDecision)
        {
            limitations.Add("The compact reviewer had broader source available before deciding, so context isolation was not blind.");
        }

        if (compact.Conditions.ReferenceReviewAvailableBeforeDecision)
        {
            limitations.Add("The compact reviewer had the reference review available before deciding, creating anchoring risk.");
        }

        if (!compact.Conditions.IndependentOfPairedReview ||
            !reference.Conditions.IndependentOfPairedReview)
        {
            limitations.Add("The paired reviews are not recorded as independently performed.");
        }

        if (compact.ReviewerModel == reference.ReviewerModel)
        {
            limitations.Add("Both contexts record the same available reviewer-model identity.");
        }

        if (limitations.Count == 0)
        {
            limitations.Add("Independence and ordering are caller-reported and were not externally verified by EffortHours.");
        }

        return [.. limitations];
    }

    private static Dictionary<EffortCategory, EffortRange> CategoryMap(
        IEnumerable<HostReviewCategoryEffort> categories) =>
        categories.ToDictionary(category => category.Category, category => category.Hours);

    private static EffortRange GetRange(
        Dictionary<EffortCategory, EffortRange> categories,
        EffortCategory category) => categories.TryGetValue(category, out EffortRange? range)
            ? range
            : new EffortRange { Low = 0m, Expected = 0m, High = 0m };

    private static decimal? Ratio(long? numerator, long? denominator) =>
        numerator.HasValue && denominator is > 0
            ? Round4((decimal)numerator.Value / denominator.Value)
            : null;

    private static decimal Round4(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
