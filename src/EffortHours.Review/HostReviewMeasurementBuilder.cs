using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

public static class HostReviewMeasurementBuilder
{
    private const string Unavailable = "Telemetry was not supplied by the surrounding caller.";

    public static HostReviewMeasurement Build(
        HostReviewPacket packet,
        string packetPayloadText,
        HostReviewAdjustmentLedger ledger,
        string adjustmentPayloadText,
        HostReviewMeasurementOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateInputs(packet, ledger, options);
        HostReviewPayloadMeasurement packetPayload =
            HostReviewPayloadMeter.MeasureDocument(packet, packetPayloadText);
        HostReviewPayloadMeasurement adjustmentPayload =
            HostReviewPayloadMeter.MeasureDocument(ledger, adjustmentPayloadText);
        HostReviewQueryMeasurement[] queries = BuildQueries(packet, options, cancellationToken);
        HostReviewPayloadTotals additionalSize = BuildAdditionalSize(options);
        HostReviewPayloadTotals observedInput = HostReviewPayloadMeter.Add(
            [
                packetPayload.Size,
                .. queries.Select(query => query.Payload.Size),
                additionalSize,
            ]);

        Dictionary<string, HostReviewCandidate> candidates = packet.Candidates
            .ToDictionary(candidate => candidate.Capability.Id, StringComparer.Ordinal);
        HostReviewDecisionMeasurement[] decisions =
        [
            .. ledger.Adjustments
                .OrderBy(adjustment => adjustment.TargetId, StringComparer.Ordinal)
                .Select(adjustment => BuildDecision(candidates[adjustment.TargetId], adjustment)),
        ];
        BuildEffortProjection(
            packet,
            decisions,
            out HostReviewCategoryEffort[] baselineCategories,
            out HostReviewCategoryEffort[] reviewedCategories,
            out EffortRange reviewedTotal);

        HostReviewMeasurement measurement = new()
        {
            SubjectId = options.SubjectId,
            Context = options.Context,
            ProtocolVersion = packet.ProtocolVersion,
            InputDigest = packet.InputDigest,
            EstimatorVersion = packet.EstimatorVersion,
            Profile = packet.Profile,
            ReviewerModel = ledger.ReviewerModel,
            PacketPayload = packetPayload,
            AdjustmentPayload = adjustmentPayload,
            Queries = queries,
            AdditionalInput = new HostReviewAdditionalInputMeasurement
            {
                SizeReported = options.AdditionalInputSizeReported,
                Size = additionalSize,
                Basis = options.AdditionalInputBasis ?? DefaultAdditionalInputBasis(options),
            },
            ObservedInput = observedInput,
            ObservedInputComplete = options.ObservedInputComplete,
            ProviderTokens = BuildProviderTokens(options),
            Elapsed = BuildElapsed(options),
            Cost = BuildCost(options),
            Conditions = new HostReviewSessionConditions
            {
                SessionId = options.SessionId,
                BroaderSourceAvailableBeforeDecision =
                    options.Context == HostReviewContextMode.BroaderSource ||
                    options.BroaderSourceAvailableBeforeDecision,
                ReferenceReviewAvailableBeforeDecision =
                    options.ReferenceReviewAvailableBeforeDecision,
                IndependentOfPairedReview = options.IndependentOfPairedReview,
                Notes = options.ConditionNotes,
            },
            Decisions = decisions,
            BaselineCategories = baselineCategories,
            ReviewedCategories = reviewedCategories,
            BaselineTotal = packet.TotalEffort,
            ReviewedTotal = reviewedTotal,
            Privacy = new HostReviewMeasurementPrivacy
            {
                RepositoryIdentityCopied = false,
                PromptTextCopied = false,
                SourceTextCopied = false,
                QuerySelectorsCopied = false,
                CallerSuppliedTextRetained = true,
                DisclosureNotice =
                    "EffortHours does not copy repository identity, prompts, query selectors " +
                    "or reasons, adjustment rationale or evidence IDs, paths, or source text. " +
                    "Caller-supplied subject/session IDs, telemetry bases, and condition notes " +
                    "are retained verbatim and must be non-sensitive.",
            },
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(measurement);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The host-review measurement is invalid: " + string.Join(" ", errors));
        }

        return measurement;
    }

    private static void ValidateInputs(
        HostReviewPacket packet,
        HostReviewAdjustmentLedger ledger,
        HostReviewMeasurementOptions options)
    {
        IReadOnlyList<string> packetErrors = ContractValidation.Validate(packet);
        if (packetErrors.Count > 0)
        {
            throw new ArgumentException(
                "The host-review packet is invalid: " + string.Join(" ", packetErrors),
                nameof(packet));
        }

        HostReviewValidationReport validation = HostReviewAdjustmentValidator.Validate(packet, ledger);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                "The host-review adjustment is invalid: " + string.Join(" ", validation.Errors),
                nameof(ledger));
        }

        string[] candidateIds =
        [.. packet.Candidates.Select(candidate => candidate.Capability.Id).Order(StringComparer.Ordinal)];
        string[] adjustmentIds =
        [.. ledger.Adjustments.Select(adjustment => adjustment.TargetId).Order(StringComparer.Ordinal)];
        if (!candidateIds.SequenceEqual(adjustmentIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "A measurement requires exactly one affirm or replace decision for every packet candidate.",
                nameof(ledger));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.SubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SessionId);
        if (options.Context == HostReviewContextMode.Compact &&
            options.Queries.Any(query => query.Result.ContainsSourceExcerpt))
        {
            throw new ArgumentException(
                "A compact measurement cannot include a selected-source excerpt query.",
                nameof(options));
        }

        ValidateOptionGroups(options);
    }

    private static void ValidateOptionGroups(HostReviewMeasurementOptions options)
    {
        if (options.AdditionalInputBytes < 0 || options.AdditionalInputCharacters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Additional input sizes cannot be negative.");
        }

        if (!options.AdditionalInputSizeReported &&
            (options.AdditionalInputBytes != 0 || options.AdditionalInputCharacters != 0 ||
             !string.IsNullOrWhiteSpace(options.AdditionalInputBasis)))
        {
            throw new ArgumentException(
                "Additional input values require complete caller-reported size telemetry.",
                nameof(options));
        }

        bool priorContextAvailable = options.Context == HostReviewContextMode.BroaderSource ||
            options.BroaderSourceAvailableBeforeDecision ||
            options.ReferenceReviewAvailableBeforeDecision;
        if (options.ObservedInputComplete && priorContextAvailable &&
            !options.AdditionalInputSizeReported)
        {
            throw new ArgumentException(
                "Complete input accounting requires reported additional-context sizes when source or reference context was available.",
                nameof(options));
        }

        bool hasAnyTokens = options.ProviderInputTokens.HasValue ||
            options.ProviderOutputTokens.HasValue || options.ProviderCachedInputTokens.HasValue;
        if (hasAnyTokens &&
            (options.ProviderInputTokens is null || options.ProviderOutputTokens is null ||
             string.IsNullOrWhiteSpace(options.TokenBasis)))
        {
            throw new ArgumentException(
                "Reported provider telemetry requires input tokens, output tokens, and a token basis.",
                nameof(options));
        }

        if (options.ElapsedMilliseconds.HasValue && string.IsNullOrWhiteSpace(options.ElapsedBasis))
        {
            throw new ArgumentException(
                "Reported elapsed time requires an elapsed basis.",
                nameof(options));
        }

        if (options.CostAmount.HasValue &&
            (string.IsNullOrWhiteSpace(options.CostCurrency) ||
             string.IsNullOrWhiteSpace(options.CostBasis)))
        {
            throw new ArgumentException(
                "Reported monetary cost requires currency and a cost basis.",
                nameof(options));
        }
    }

    private static HostReviewQueryMeasurement[] BuildQueries(
        HostReviewPacket packet,
        HostReviewMeasurementOptions options,
        CancellationToken cancellationToken)
    {
        List<HostReviewQueryMeasurement> measurements = [];
        foreach (HostReviewQueryPayloadInput query in options.Queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> errors = ContractValidation.Validate(query.Result);
            if (errors.Count > 0 || query.Result.InputDigest != packet.InputDigest ||
                query.Result.Query.InputDigest != packet.InputDigest)
            {
                throw new ArgumentException(
                    "A measured query result is invalid or belongs to another review input.",
                    nameof(options));
            }

            measurements.Add(new HostReviewQueryMeasurement
            {
                Kind = query.Result.Query.Kind,
                ContainsSourceExcerpt = query.Result.ContainsSourceExcerpt,
                Payload = HostReviewPayloadMeter.MeasureDocument(query.Result, query.PayloadText),
            });
        }

        return
        [
            .. measurements
                .OrderBy(measurement => measurement.Kind)
                .ThenBy(measurement => measurement.Payload.Digest, StringComparer.Ordinal),
        ];
    }

    private static HostReviewDecisionMeasurement BuildDecision(
        HostReviewCandidate candidate,
        HostReviewAdjustment adjustment)
    {
        EffortCategory reviewedCategory = adjustment.Replacement?.Category ??
            candidate.Capability.Category;
        EffortRange reviewedHours = adjustment.Replacement?.Hours ?? candidate.Capability.Hours;
        return new HostReviewDecisionMeasurement
        {
            TargetId = adjustment.TargetId,
            Decision = adjustment.Decision,
            BaselineCategory = candidate.Capability.Category,
            BaselineHours = candidate.Capability.Hours,
            ReviewedCategory = reviewedCategory,
            ReviewedHours = reviewedHours,
        };
    }

    private static void BuildEffortProjection(
        HostReviewPacket packet,
        IReadOnlyList<HostReviewDecisionMeasurement> decisions,
        out HostReviewCategoryEffort[] baselineCategories,
        out HostReviewCategoryEffort[] reviewedCategories,
        out EffortRange reviewedTotal)
    {
        Dictionary<EffortCategory, EffortRange> baseline = packet.Categories
            .ToDictionary(category => category.Category, category => category.Hours);
        Dictionary<EffortCategory, EffortRange> reviewed = new(baseline);
        foreach (HostReviewDecisionMeasurement decision in decisions)
        {
            reviewed[decision.BaselineCategory] = Subtract(
                reviewed[decision.BaselineCategory],
                decision.BaselineHours);
            reviewed.TryAdd(decision.ReviewedCategory, Zero());
            reviewed[decision.ReviewedCategory] = Add(
                reviewed[decision.ReviewedCategory],
                decision.ReviewedHours);
            baseline.TryAdd(decision.ReviewedCategory, Zero());
        }

        EffortCategory[] categories =
        [.. baseline.Keys.Union(reviewed.Keys).Order()];
        baselineCategories =
        [.. categories.Select(category => new HostReviewCategoryEffort
        {
            Category = category,
            Hours = baseline[category],
        })];
        reviewedCategories =
        [.. categories.Select(category => new HostReviewCategoryEffort
        {
            Category = category,
            Hours = reviewed[category],
        })];
        reviewedTotal = Add(packet.TotalEffort, SumDeltas(decisions));
    }

    private static EffortRange SumDeltas(IEnumerable<HostReviewDecisionMeasurement> decisions) =>
        decisions.Aggregate(Zero(), (sum, decision) => Add(
            sum,
            Subtract(decision.ReviewedHours, decision.BaselineHours)));

    private static HostReviewPayloadTotals BuildAdditionalSize(HostReviewMeasurementOptions options) =>
        new()
        {
            Utf8Bytes = options.AdditionalInputBytes,
            CharacterCount = options.AdditionalInputCharacters,
            ApproximateTokens = HostReviewPayloadMeter.ApproximateTokens(
                options.AdditionalInputCharacters),
        };

    private static HostReviewProviderTokenUsage BuildProviderTokens(
        HostReviewMeasurementOptions options) => options.ProviderInputTokens.HasValue
        ? new HostReviewProviderTokenUsage
        {
            InputTokens = options.ProviderInputTokens,
            OutputTokens = options.ProviderOutputTokens,
            CachedInputTokens = options.ProviderCachedInputTokens,
            Basis = options.TokenBasis,
        }
        : new HostReviewProviderTokenUsage { UnavailableReason = Unavailable };

    private static HostReviewElapsedTelemetry BuildElapsed(HostReviewMeasurementOptions options) =>
        options.ElapsedMilliseconds.HasValue
            ? new HostReviewElapsedTelemetry
            {
                Milliseconds = options.ElapsedMilliseconds,
                Basis = options.ElapsedBasis,
            }
            : new HostReviewElapsedTelemetry { UnavailableReason = Unavailable };

    private static HostReviewCostTelemetry BuildCost(HostReviewMeasurementOptions options) =>
        options.CostAmount.HasValue
            ? new HostReviewCostTelemetry
            {
                Amount = options.CostAmount,
                Currency = options.CostCurrency?.ToUpperInvariant(),
                Basis = options.CostBasis,
            }
            : new HostReviewCostTelemetry { UnavailableReason = Unavailable };

    private static string DefaultAdditionalInputBasis(HostReviewMeasurementOptions options) =>
        options.AdditionalInputSizeReported
            ? "Caller-reported additional context size."
            : "No additional input size was supplied by the caller.";

    private static EffortRange Zero() => new() { Low = 0m, Expected = 0m, High = 0m };

    private static EffortRange Add(EffortRange left, EffortRange right) => new()
    {
        Low = left.Low + right.Low,
        Expected = left.Expected + right.Expected,
        High = left.High + right.High,
    };

    private static EffortRange Subtract(EffortRange left, EffortRange right) => new()
    {
        Low = left.Low - right.Low,
        Expected = left.Expected - right.Expected,
        High = left.High - right.High,
    };
}
