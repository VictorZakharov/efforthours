using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateTelemetryComparison(
        HostReviewTelemetryComparison comparison,
        string path,
        List<string> errors)
    {
        ValidateContextTelemetry(comparison.Compact, $"{path}.compact", errors);
        ValidateContextTelemetry(comparison.BroaderSource, $"{path}.broaderSource", errors);
        ValidateOptionalRate(comparison.ObservedInputByteRatio, $"{path}.observedInputByteRatio", false, errors);
        ValidateOptionalRate(comparison.ApproximateInputTokenRatio, $"{path}.approximateInputTokenRatio", false, errors);
        ValidateOptionalRate(comparison.ProviderInputTokenRatio, $"{path}.providerInputTokenRatio", false, errors);
        ValidateOptionalRate(comparison.ElapsedTimeRatio, $"{path}.elapsedTimeRatio", false, errors);
        ValidateOptionalRate(comparison.MonetaryCostRatio, $"{path}.monetaryCostRatio", false, errors);
        ValidateTextSet(comparison.Diagnostics, $"{path}.diagnostics", errors);
        bool complete = comparison.Compact.ObservedInputComplete &&
            comparison.BroaderSource.ObservedInputComplete;
        if (!complete &&
            (comparison.ObservedInputByteRatio.HasValue ||
             comparison.ApproximateInputTokenRatio.HasValue))
        {
            errors.Add(
                $"{path} cannot report observed-input ratios when either context is incomplete.");
        }

        if (complete &&
            (comparison.ObservedInputByteRatio is null ||
             comparison.ApproximateInputTokenRatio is null))
        {
            errors.Add(
                $"{path} must report observed-input ratios when both contexts are complete.");
        }
    }

    private static void ValidateContextTelemetry(
        HostReviewContextTelemetry telemetry,
        string path,
        List<string> errors)
    {
        ValidatePayloadTotals(telemetry.ObservedInput, $"{path}.observedInput", errors);
        if (telemetry.QueryCount < 0 || telemetry.SelectedSourceQueryCount < 0 ||
            telemetry.SelectedSourceQueryCount > telemetry.QueryCount)
        {
            errors.Add($"{path} contains inconsistent query counts.");
        }

        ValidateProviderTokens(telemetry.ProviderTokens, $"{path}.providerTokens", errors);
        ValidateElapsed(telemetry.Elapsed, $"{path}.elapsed", errors);
        ValidateCost(telemetry.Cost, $"{path}.cost", errors);
    }

    private static void RequireMeasurementVersion(string value, List<string> errors)
    {
        if (value != HostReviewMeasurementVersions.V1)
        {
            errors.Add(
                $"Host-review measurementVersion must be '{HostReviewMeasurementVersions.V1}'.");
        }
    }

    private static void ValidatePayload(
        HostReviewPayloadMeasurement payload,
        string path,
        List<string> errors)
    {
        ValidateDigest(payload.Digest, $"{path}.digest", errors);
        ValidatePayloadTotals(payload.Size, $"{path}.size", errors);
    }

    private static void ValidatePayloadTotals(
        HostReviewPayloadTotals payload,
        string path,
        List<string> errors)
    {
        if (payload.Utf8Bytes < 0 || payload.CharacterCount < 0 || payload.ApproximateTokens < 0)
        {
            errors.Add($"{path} sizes cannot be negative.");
        }

        if (payload.Utf8Bytes < payload.CharacterCount)
        {
            errors.Add($"{path}.utf8Bytes cannot be smaller than its Unicode character count.");
        }

        long expectedTokens = (long)decimal.Ceiling(payload.CharacterCount / 4m);
        if (payload.ApproximateTokens != expectedTokens)
        {
            errors.Add($"{path}.approximateTokens must equal ceiling(characterCount / 4).");
        }
    }

    private static void ValidateProviderTokens(
        HostReviewProviderTokenUsage usage,
        string path,
        List<string> errors)
    {
        bool reported = usage.InputTokens.HasValue || usage.OutputTokens.HasValue ||
            usage.CachedInputTokens.HasValue;
        if (!reported)
        {
            RequireText(usage.UnavailableReason, $"{path}.unavailableReason", errors);
            if (usage.Basis is not null)
            {
                errors.Add($"{path}.basis must be absent when provider tokens are unavailable.");
            }

            return;
        }

        if (usage.InputTokens is null or < 0 || usage.OutputTokens is null or < 0 ||
            usage.CachedInputTokens is < 0)
        {
            errors.Add($"{path} requires non-negative input and output tokens when reported.");
        }

        if (usage.CachedInputTokens > usage.InputTokens)
        {
            errors.Add($"{path}.cachedInputTokens cannot exceed inputTokens.");
        }

        RequireText(usage.Basis, $"{path}.basis", errors);
        if (usage.UnavailableReason is not null)
        {
            errors.Add($"{path}.unavailableReason must be absent when tokens are reported.");
        }
    }

    private static void ValidateElapsed(
        HostReviewElapsedTelemetry elapsed,
        string path,
        List<string> errors)
    {
        ValidateOptionalTelemetry(
            elapsed.Milliseconds,
            elapsed.Basis,
            elapsed.UnavailableReason,
            path,
            errors);
    }

    private static void ValidateCost(
        HostReviewCostTelemetry cost,
        string path,
        List<string> errors)
    {
        ValidateOptionalTelemetry(cost.Amount, cost.Basis, cost.UnavailableReason, path, errors);
        if (cost.Amount.HasValue)
        {
            if (cost.Currency is null || cost.Currency.Length != 3 ||
                cost.Currency.Any(character => character is < 'A' or > 'Z'))
            {
                errors.Add($"{path}.currency must be a three-letter uppercase code when cost is reported.");
            }
        }
        else if (cost.Currency is not null)
        {
            errors.Add($"{path}.currency must be absent when cost is unavailable.");
        }
    }

    private static void ValidateOptionalTelemetry<T>(
        T? value,
        string? basis,
        string? unavailableReason,
        string path,
        List<string> errors)
        where T : struct, IComparable<T>
    {
        if (value.HasValue)
        {
            if (value.Value.CompareTo(default) < 0)
            {
                errors.Add($"{path} value cannot be negative.");
            }

            RequireText(basis, $"{path}.basis", errors);
            if (unavailableReason is not null)
            {
                errors.Add($"{path}.unavailableReason must be absent when telemetry is reported.");
            }
        }
        else
        {
            RequireText(unavailableReason, $"{path}.unavailableReason", errors);
            if (basis is not null)
            {
                errors.Add($"{path}.basis must be absent when telemetry is unavailable.");
            }
        }
    }

    private static void ValidateOptionalRate(
        decimal? rate,
        string path,
        bool allowNegative,
        List<string> errors)
    {
        if (rate.HasValue && !allowNegative && rate.Value < 0m)
        {
            errors.Add($"{path} cannot be negative.");
        }
    }

    private static void ValidateOptionalUnitRate(decimal? rate, string path, List<string> errors)
    {
        if (rate is < 0m or > 1m)
        {
            errors.Add($"{path} must be between zero and one.");
        }
    }
}
