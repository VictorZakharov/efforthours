using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static bool TryParseReviewMeasureOptions(
        string[] arguments,
        out ReviewMeasureCliOptions options,
        out string error)
    {
        ReviewMeasureCliOptionsBuilder builder = new();
        for (int index = 2; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (!TryReadReviewMeasureOption(arguments, ref index, option, builder, out error))
            {
                options = new ReviewMeasureCliOptions();
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(builder.SubjectId) ||
            string.IsNullOrWhiteSpace(builder.SessionId) || builder.Context is null)
        {
            options = new ReviewMeasureCliOptions();
            error = "Review measurement requires --subject, --session, and --context.";
            return false;
        }

        if (!ValidateReviewMeasureOptionGroups(builder, out error))
        {
            options = new ReviewMeasureCliOptions();
            return false;
        }

        options = builder.Build();
        error = string.Empty;
        return true;
    }

    private static bool TryReadReviewMeasureOption(
        string[] arguments,
        ref int index,
        string option,
        ReviewMeasureCliOptionsBuilder builder,
        out string error)
    {
        error = string.Empty;
        switch (option)
        {
            case "--compact":
                builder.Compact = true;
                return true;
            case "--source-seen-before":
                builder.SourceSeenBefore = true;
                return true;
            case "--reference-seen-before":
                builder.ReferenceSeenBefore = true;
                return true;
            case "--independent-reviewer":
                builder.IndependentReviewer = true;
                return true;
            case "--input-complete":
                builder.InputComplete = true;
                return true;
        }

        if (!TryReadOptionValue(arguments, ref index, option, out string value, out error))
        {
            return false;
        }

        switch (option)
        {
            case "--subject": builder.SubjectId = value; return true;
            case "--session": builder.SessionId = value; return true;
            case "--query-result": builder.QueryResultPaths.Add(value); return true;
            case "--token-basis": builder.TokenBasis = value; return true;
            case "--elapsed-basis": builder.ElapsedBasis = value; return true;
            case "--currency": builder.Currency = value; return true;
            case "--cost-basis": builder.CostBasis = value; return true;
            case "--additional-input-basis": builder.AdditionalInputBasis = value; return true;
            case "--note": builder.Notes.Add(value); return true;
            case "--output": builder.OutputPath = value; return true;
            case "--context":
                if (value.Equals("compact", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Context = HostReviewContextMode.Compact;
                    return true;
                }

                if (value.Equals("broader-source", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Context = HostReviewContextMode.BroaderSource;
                    return true;
                }

                error = "Option '--context' must be 'compact' or 'broader-source'.";
                return false;
            case "--elapsed-ms":
                return TryAssignLong(value, option, parsed => builder.ElapsedMilliseconds = parsed, out error);
            case "--provider-input-tokens":
                return TryAssignLong(value, option, parsed => builder.ProviderInputTokens = parsed, out error);
            case "--provider-output-tokens":
                return TryAssignLong(value, option, parsed => builder.ProviderOutputTokens = parsed, out error);
            case "--provider-cached-input-tokens":
                return TryAssignLong(value, option, parsed => builder.ProviderCachedInputTokens = parsed, out error);
            case "--additional-input-bytes":
                return TryAssignLong(value, option, parsed => builder.AdditionalInputBytes = parsed, out error);
            case "--additional-input-characters":
                return TryAssignLong(value, option, parsed => builder.AdditionalInputCharacters = parsed, out error);
            case "--cost":
                if (!decimal.TryParse(
                    value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal cost) || cost < 0m)
                {
                    error = "Option '--cost' requires a non-negative decimal amount.";
                    return false;
                }

                builder.Cost = cost;
                return true;
            default:
                error = $"Unknown review measure option '{option}'.";
                return false;
        }
    }

    private static bool TryReadOptionValue(
        string[] arguments,
        ref int index,
        string option,
        out string value,
        out string error)
    {
        if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith('-'))
        {
            value = string.Empty;
            error = $"Option '{option}' requires a value.";
            return false;
        }

        value = arguments[++index];
        error = string.Empty;
        return true;
    }

    private static bool TryAssignLong(
        string value,
        string option,
        Action<long> assign,
        out string error)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
            parsed < 0)
        {
            error = $"Option '{option}' requires a non-negative integer.";
            return false;
        }

        assign(parsed);
        error = string.Empty;
        return true;
    }

    private static bool ValidateReviewMeasureOptionGroups(
        ReviewMeasureCliOptionsBuilder builder,
        out string error)
    {
        bool anyTokens = builder.ProviderInputTokens.HasValue ||
            builder.ProviderOutputTokens.HasValue || builder.ProviderCachedInputTokens.HasValue;
        if (anyTokens &&
            (builder.ProviderInputTokens is null || builder.ProviderOutputTokens is null ||
             string.IsNullOrWhiteSpace(builder.TokenBasis)))
        {
            error = "Provider telemetry requires input tokens, output tokens, and --token-basis.";
            return false;
        }

        if (builder.ElapsedMilliseconds.HasValue && string.IsNullOrWhiteSpace(builder.ElapsedBasis))
        {
            builder.ElapsedBasis = "caller-reported wall-clock duration";
        }

        if (builder.Cost.HasValue &&
            (string.IsNullOrWhiteSpace(builder.Currency) || string.IsNullOrWhiteSpace(builder.CostBasis)))
        {
            error = "Cost telemetry requires --currency and --cost-basis.";
            return false;
        }

        bool anyAdditional = builder.AdditionalInputBytes.HasValue ||
            builder.AdditionalInputCharacters.HasValue ||
            !string.IsNullOrWhiteSpace(builder.AdditionalInputBasis);
        if (anyAdditional &&
            (builder.AdditionalInputBytes is null || builder.AdditionalInputCharacters is null))
        {
            error = "Additional input telemetry requires both bytes and characters.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private sealed record ReviewMeasureCliOptions
    {
        public string SubjectId { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public HostReviewContextMode Context { get; init; }
        public IReadOnlyList<string> QueryResultPaths { get; init; } = [];
        public long? ElapsedMilliseconds { get; init; }
        public string? ElapsedBasis { get; init; }
        public long? ProviderInputTokens { get; init; }
        public long? ProviderOutputTokens { get; init; }
        public long? ProviderCachedInputTokens { get; init; }
        public string? TokenBasis { get; init; }
        public decimal? Cost { get; init; }
        public string? Currency { get; init; }
        public string? CostBasis { get; init; }
        public long AdditionalInputBytes { get; init; }
        public long AdditionalInputCharacters { get; init; }
        public string? AdditionalInputBasis { get; init; }
        public bool AdditionalInputSizeReported { get; init; }
        public bool InputComplete { get; init; }
        public bool SourceSeenBefore { get; init; }
        public bool ReferenceSeenBefore { get; init; }
        public bool IndependentReviewer { get; init; }
        public IReadOnlyList<string> Notes { get; init; } = [];
        public bool Compact { get; init; }
        public string? OutputPath { get; init; }
    }

    private sealed class ReviewMeasureCliOptionsBuilder
    {
        public string? SubjectId { get; set; }
        public string? SessionId { get; set; }
        public HostReviewContextMode? Context { get; set; }
        public List<string> QueryResultPaths { get; } = [];
        public long? ElapsedMilliseconds { get; set; }
        public string? ElapsedBasis { get; set; }
        public long? ProviderInputTokens { get; set; }
        public long? ProviderOutputTokens { get; set; }
        public long? ProviderCachedInputTokens { get; set; }
        public string? TokenBasis { get; set; }
        public decimal? Cost { get; set; }
        public string? Currency { get; set; }
        public string? CostBasis { get; set; }
        public long? AdditionalInputBytes { get; set; }
        public long? AdditionalInputCharacters { get; set; }
        public string? AdditionalInputBasis { get; set; }
        public bool InputComplete { get; set; }
        public bool SourceSeenBefore { get; set; }
        public bool ReferenceSeenBefore { get; set; }
        public bool IndependentReviewer { get; set; }
        public List<string> Notes { get; } = [];
        public bool Compact { get; set; }
        public string? OutputPath { get; set; }

        public ReviewMeasureCliOptions Build() => new()
        {
            SubjectId = SubjectId!,
            SessionId = SessionId!,
            Context = Context!.Value,
            QueryResultPaths = QueryResultPaths,
            ElapsedMilliseconds = ElapsedMilliseconds,
            ElapsedBasis = ElapsedBasis,
            ProviderInputTokens = ProviderInputTokens,
            ProviderOutputTokens = ProviderOutputTokens,
            ProviderCachedInputTokens = ProviderCachedInputTokens,
            TokenBasis = TokenBasis,
            Cost = Cost,
            Currency = Currency,
            CostBasis = CostBasis,
            AdditionalInputBytes = AdditionalInputBytes ?? 0,
            AdditionalInputCharacters = AdditionalInputCharacters ?? 0,
            AdditionalInputBasis = AdditionalInputBasis,
            AdditionalInputSizeReported = AdditionalInputBytes.HasValue,
            InputComplete = InputComplete,
            SourceSeenBefore = SourceSeenBefore,
            ReferenceSeenBefore = ReferenceSeenBefore,
            IndependentReviewer = IndependentReviewer,
            Notes = Notes,
            Compact = Compact,
            OutputPath = OutputPath,
        };
    }
}
