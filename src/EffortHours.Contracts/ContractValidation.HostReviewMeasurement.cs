using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(HostReviewMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        List<string> errors = [];
        RequireVersion(measurement.SchemaVersion, "host-review measurement", errors);
        RequireMeasurementVersion(measurement.MeasurementVersion, errors);
        RequireText(measurement.SubjectId, "subjectId", errors);
        if (measurement.SubjectId.Length > 256)
        {
            errors.Add("subjectId cannot exceed 256 characters.");
        }
        RequireHostReviewProtocol(measurement.ProtocolVersion, errors);
        ValidateDigest(measurement.InputDigest, "inputDigest", errors);
        RequireText(measurement.EstimatorVersion, "estimatorVersion", errors);
        ValidateModelIdentity(measurement.ReviewerModel, errors);
        ValidatePayload(measurement.PacketPayload, "packetPayload", errors);
        ValidatePayload(measurement.AdjustmentPayload, "adjustmentPayload", errors);

        foreach ((HostReviewQueryMeasurement query, int index) in
            measurement.Queries.Select((query, index) => (query, index)))
        {
            ValidatePayload(query.Payload, $"queries[{index}].payload", errors);
            bool selectedSource = query.Kind == HostReviewQueryKind.SelectedSource;
            if (query.ContainsSourceExcerpt != selectedSource)
            {
                errors.Add(
                    $"queries[{index}].containsSourceExcerpt must be true only for a selected-source query.");
            }

            if (measurement.Context == HostReviewContextMode.Compact &&
                query.ContainsSourceExcerpt)
            {
                errors.Add("A compact measurement cannot contain a selected-source excerpt query.");
            }
        }

        ValidatePayloadTotals(measurement.AdditionalInput.Size, "additionalInput.size", errors);
        RequireText(measurement.AdditionalInput.Basis, "additionalInput.basis", errors);
        if (!measurement.AdditionalInput.SizeReported &&
            measurement.AdditionalInput.Size != ZeroPayload())
        {
            errors.Add("additionalInput.size must be zero when its size was not reported.");
        }

        ValidatePayloadTotals(measurement.ObservedInput, "observedInput", errors);
        ValidateObservedInput(measurement, errors);
        ValidateProviderTokens(measurement.ProviderTokens, "providerTokens", errors);
        ValidateElapsed(measurement.Elapsed, "elapsed", errors);
        ValidateCost(measurement.Cost, "cost", errors);
        ValidateSessionConditions(measurement, errors);
        ValidateMeasurementDecisions(measurement.Decisions, errors);
        ValidateMeasurementCategories(
            measurement.BaselineCategories,
            "baselineCategories",
            measurement.BaselineTotal,
            errors);
        ValidateMeasurementCategories(
            measurement.ReviewedCategories,
            "reviewedCategories",
            measurement.ReviewedTotal,
            errors);
        ValidatePrivacy(measurement.Privacy, errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(HostReviewBenchmarkReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> errors = [];
        RequireVersion(report.SchemaVersion, "host-review benchmark report", errors);
        RequireMeasurementVersion(report.MeasurementVersion, errors);
        if (report.MetricVersion != HostReviewMeasurementVersions.MetricsV1)
        {
            errors.Add(
                $"Host-review metricVersion must be '{HostReviewMeasurementVersions.MetricsV1}'.");
        }

        if (report.SubjectCount <= 0 || report.SubjectCount != report.Subjects.Count)
        {
            errors.Add("Host-review benchmark subjectCount must match a non-empty subjects list.");
        }

        if (report.MeasurementCount != report.SubjectCount * 2)
        {
            errors.Add("Host-review benchmark measurementCount must equal two measurements per subject.");
        }

        HashSet<string> subjectIds = new(StringComparer.Ordinal);
        foreach (HostReviewSubjectBenchmark subject in report.Subjects)
        {
            ValidateSubjectBenchmark(subject, errors);
            if (!subjectIds.Add(subject.SubjectId))
            {
                errors.Add($"Host-review benchmark subject '{subject.SubjectId}' is duplicated.");
            }
        }

        ValidateLevelComparison(
            report.CapabilityItems,
            HostReviewComparisonLevel.CapabilityItem,
            "capabilityItems",
            errors);
        ValidateLevelComparison(
            report.Categories,
            HostReviewComparisonLevel.Category,
            "categories",
            errors);
        ValidateLevelComparison(
            report.RepositoryTotals,
            HostReviewComparisonLevel.RepositoryTotal,
            "repositoryTotals",
            errors);
        if (report.BudgetDecision.DefaultBudgetSelected)
        {
            errors.Add("Measurement version 1 cannot select a default host-review budget.");
        }

        RequireText(report.BudgetDecision.Reason, "budgetDecision.reason", errors);
        ValidateTextSet(report.Limitations, "limitations", errors);
        if (report.Limitations.Count == 0)
        {
            errors.Add("A host-review benchmark must disclose its limitations.");
        }

        return errors;
    }

    private static void ValidateMeasurementDecisions(
        IReadOnlyList<HostReviewDecisionMeasurement> decisions,
        List<string> errors)
    {
        HashSet<string> targetIds = new(StringComparer.Ordinal);
        foreach (HostReviewDecisionMeasurement decision in decisions)
        {
            RequireText(decision.TargetId, "decision.targetId", errors);
            if (!targetIds.Add(decision.TargetId))
            {
                errors.Add($"Host-review measurement target '{decision.TargetId}' is duplicated.");
            }

            ValidateRange(decision.BaselineHours, $"decision[{decision.TargetId}].baselineHours", errors);
            ValidateRange(decision.ReviewedHours, $"decision[{decision.TargetId}].reviewedHours", errors);
            if (decision.Decision == HostReviewDecision.Affirm &&
                (decision.BaselineCategory != decision.ReviewedCategory ||
                 decision.BaselineHours != decision.ReviewedHours))
            {
                errors.Add(
                    $"Affirmed measurement target '{decision.TargetId}' must retain its baseline category and range.");
            }
        }
    }

    private static void ValidateMeasurementCategories(
        IReadOnlyList<HostReviewCategoryEffort> categories,
        string path,
        EffortRange total,
        List<string> errors)
    {
        HashSet<EffortCategory> seen = [];
        foreach (HostReviewCategoryEffort category in categories)
        {
            ValidateRange(category.Hours, $"{path}[{category.Category}]", errors);
            if (!seen.Add(category.Category))
            {
                errors.Add($"{path} contains duplicate category '{category.Category}'.");
            }
        }

        ValidateRange(total, path == "baselineCategories" ? "baselineTotal" : "reviewedTotal", errors);
        EffortRange sum = SumRanges(categories.Select(category => category.Hours));
        if (sum != total)
        {
            errors.Add($"{path} does not reconcile to its repository total.");
        }
    }

    private static void ValidateObservedInput(
        HostReviewMeasurement measurement,
        List<string> errors)
    {
        HostReviewPayloadTotals expected = SumPayloads(
            measurement.PacketPayload.Size,
            measurement.Queries.Select(query => query.Payload.Size),
            measurement.AdditionalInput.Size);
        if (measurement.ObservedInput != expected)
        {
            errors.Add("observedInput does not equal packet, query, and additional input payloads.");
        }

        bool priorContextAvailable =
            measurement.Context == HostReviewContextMode.BroaderSource ||
            measurement.Conditions.BroaderSourceAvailableBeforeDecision ||
            measurement.Conditions.ReferenceReviewAvailableBeforeDecision;
        if (measurement.ObservedInputComplete && priorContextAvailable &&
            !measurement.AdditionalInput.SizeReported)
        {
            errors.Add(
                "Complete observed-input accounting requires reported additional-context sizes when source or reference context was available.");
        }
    }

    private static void ValidatePrivacy(
        HostReviewMeasurementPrivacy privacy,
        List<string> errors)
    {
        RequireText(privacy.DisclosureNotice, "privacy.disclosureNotice", errors);
        if (privacy.RepositoryIdentityCopied || privacy.PromptTextCopied ||
            privacy.SourceTextCopied || privacy.QuerySelectorsCopied)
        {
            errors.Add("A host-review measurement must not copy repository identity, prompts, source, or query selectors.");
        }

        if (!privacy.CallerSuppliedTextRetained)
        {
            errors.Add("A host-review measurement must disclose that caller-supplied text is retained.");
        }
    }

    private static void ValidateSubjectBenchmark(
        HostReviewSubjectBenchmark subject,
        List<string> errors)
    {
        RequireText(subject.SubjectId, "subject.subjectId", errors);
        ValidateDigest(subject.InputDigest, $"subject[{subject.SubjectId}].inputDigest", errors);
        RequireText(subject.EstimatorVersion, $"subject[{subject.SubjectId}].estimatorVersion", errors);
        ValidateDigest(
            subject.CompactMeasurementDigest,
            $"subject[{subject.SubjectId}].compactMeasurementDigest",
            errors);
        ValidateDigest(
            subject.BroaderSourceMeasurementDigest,
            $"subject[{subject.SubjectId}].broaderSourceMeasurementDigest",
            errors);
        ValidateLevelComparison(
            subject.CapabilityItems,
            HostReviewComparisonLevel.CapabilityItem,
            $"subject[{subject.SubjectId}].capabilityItems",
            errors);
        ValidateLevelComparison(
            subject.Categories,
            HostReviewComparisonLevel.Category,
            $"subject[{subject.SubjectId}].categories",
            errors);
        ValidateLevelComparison(
            subject.RepositoryTotal,
            HostReviewComparisonLevel.RepositoryTotal,
            $"subject[{subject.SubjectId}].repositoryTotal",
            errors);
        ValidateTelemetryComparison(subject.Telemetry, $"subject[{subject.SubjectId}].telemetry", errors);
        ValidateTextSet(subject.Limitations, $"subject[{subject.SubjectId}].limitations", errors);
        if (subject.Limitations.Count == 0)
        {
            errors.Add($"Host-review benchmark subject '{subject.SubjectId}' must disclose limitations.");
        }
    }

    private static void ValidateSessionConditions(
        HostReviewMeasurement measurement,
        List<string> errors)
    {
        RequireText(measurement.Conditions.SessionId, "conditions.sessionId", errors);
        if (measurement.Conditions.SessionId.Length > 256)
        {
            errors.Add("conditions.sessionId cannot exceed 256 characters.");
        }
        ValidateTextSet(measurement.Conditions.Notes, "conditions.notes", errors);
        if (measurement.Context == HostReviewContextMode.BroaderSource &&
            !measurement.Conditions.BroaderSourceAvailableBeforeDecision)
        {
            errors.Add("A broader-source measurement must record that broader source was available before its decision.");
        }
    }

    private static void ValidateLevelComparison(
        HostReviewLevelComparison comparison,
        HostReviewComparisonLevel expectedLevel,
        string path,
        List<string> errors)
    {
        if (comparison.Level != expectedLevel)
        {
            errors.Add($"{path}.level must be '{expectedLevel}'.");
        }

        ValidateAgreement(comparison.BaselineAgreement, $"{path}.baselineAgreement", errors);
        ValidateAgreement(comparison.CompactAgreement, $"{path}.compactAgreement", errors);
        if (comparison.BaselineToCompactAbsoluteExpectedCorrectionHours < 0m ||
            comparison.BaselineToReferenceAbsoluteExpectedCorrectionHours < 0m)
        {
            errors.Add($"{path} correction magnitudes cannot be negative.");
        }

        ValidateOptionalRate(
            comparison.ExpectedAbsoluteErrorReductionRate,
            $"{path}.expectedAbsoluteErrorReductionRate",
            allowNegative: true,
            errors);
    }

    private static void ValidateAgreement(
        HostReviewRangeAgreementMetrics metrics,
        string path,
        List<string> errors)
    {
        ValidatePointAgreement(metrics.Low, $"{path}.low", errors);
        ValidatePointAgreement(metrics.Expected, $"{path}.expected", errors);
        ValidatePointAgreement(metrics.High, $"{path}.high", errors);
        ValidateIntervalAgreement(metrics.Interval, $"{path}.interval", errors);
    }

    private static void ValidatePointAgreement(
        HostReviewPointAgreementMetrics metrics,
        string path,
        List<string> errors)
    {
        if (metrics.SampleCount < 0 || metrics.ReferenceHours < 0m ||
            metrics.CandidateHours < 0m || metrics.AbsoluteErrorHours < 0m ||
            metrics.MeanAbsoluteErrorHours < 0m)
        {
            errors.Add($"{path} contains a negative count, hour total, or absolute error.");
        }

        ValidateOptionalRate(metrics.WeightedAbsolutePercentageError, $"{path}.weightedAbsolutePercentageError", false, errors);
        ValidateOptionalRate(metrics.AggregateBiasRate, $"{path}.aggregateBiasRate", true, errors);
    }

    private static void ValidateIntervalAgreement(
        HostReviewIntervalAgreementMetrics metrics,
        string path,
        List<string> errors)
    {
        if (metrics.SampleCount < 0 || metrics.ReferenceExpectedCoveredCount < 0 ||
            metrics.ReferenceRangeFullyCoveredCount < 0 || metrics.RangeOverlapCount < 0 ||
            metrics.ReferenceExpectedCoveredCount > metrics.SampleCount ||
            metrics.ReferenceRangeFullyCoveredCount > metrics.SampleCount ||
            metrics.RangeOverlapCount > metrics.SampleCount)
        {
            errors.Add($"{path} contains inconsistent interval counts.");
        }

        ValidateOptionalUnitRate(metrics.ReferenceExpectedCoverage, $"{path}.referenceExpectedCoverage", errors);
        ValidateOptionalUnitRate(metrics.ReferenceRangeFullyCoveredRate, $"{path}.referenceRangeFullyCoveredRate", errors);
        ValidateOptionalUnitRate(metrics.RangeOverlapRate, $"{path}.rangeOverlapRate", errors);
    }

    private static EffortRange SumRanges(IEnumerable<EffortRange> ranges) => new()
    {
        Low = ranges.Sum(range => range.Low),
        Expected = ranges.Sum(range => range.Expected),
        High = ranges.Sum(range => range.High),
    };

    private static HostReviewPayloadTotals SumPayloads(
        HostReviewPayloadTotals packet,
        IEnumerable<HostReviewPayloadTotals> queries,
        HostReviewPayloadTotals additional)
    {
        HostReviewPayloadTotals[] payloads = [packet, .. queries, additional];
        return new HostReviewPayloadTotals
        {
            Utf8Bytes = payloads.Sum(payload => payload.Utf8Bytes),
            CharacterCount = payloads.Sum(payload => payload.CharacterCount),
            ApproximateTokens = (long)decimal.Ceiling(
                payloads.Sum(payload => payload.CharacterCount) / 4m),
        };
    }

    private static HostReviewPayloadTotals ZeroPayload() => new()
    {
        Utf8Bytes = 0,
        CharacterCount = 0,
        ApproximateTokens = 0,
    };
}
