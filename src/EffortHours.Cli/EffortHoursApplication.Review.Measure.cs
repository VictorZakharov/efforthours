using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ReviewMeasureAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || arguments.Any(IsHelp))
        {
            await standardOutput.WriteLineAsync(ReviewMeasureHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments.Length < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Review measurement requires packet and adjustment paths.")
                .ConfigureAwait(false);
        }

        if (!TryParseReviewMeasureOptions(arguments, out ReviewMeasureCliOptions options, out string error))
        {
            return await UsageErrorAsync(standardError, error).ConfigureAwait(false);
        }

        ReviewDocument<HostReviewPacket>? packet = await ReadReviewDocumentAsync<HostReviewPacket>(
            arguments[0],
            "packet",
            SchemaNames.HostReviewPacket,
            ContractValidation.Validate,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (packet is null)
        {
            return CliExitCodes.InvalidInput;
        }

        ReviewDocument<HostReviewAdjustmentLedger>? adjustment = await ReadReviewDocumentAsync<HostReviewAdjustmentLedger>(
            arguments[1],
            "adjustment ledger",
            SchemaNames.HostReviewAdjustment,
            ContractValidation.Validate,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (adjustment is null)
        {
            return CliExitCodes.InvalidInput;
        }

        List<HostReviewQueryPayloadInput> queries = [];
        foreach (string queryPath in options.QueryResultPaths)
        {
            ReviewDocument<HostReviewQueryResult>? query = await ReadReviewDocumentAsync<HostReviewQueryResult>(
                queryPath,
                "query result",
                SchemaNames.HostReviewQueryResult,
                ContractValidation.Validate,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (query is null)
            {
                return CliExitCodes.InvalidInput;
            }

            queries.Add(new HostReviewQueryPayloadInput
            {
                Result = query.Value,
                PayloadText = query.Json,
            });
        }

        HostReviewMeasurement measurement;
        try
        {
            measurement = HostReviewMeasurementBuilder.Build(
                packet.Value,
                packet.Json,
                adjustment.Value,
                adjustment.Json,
                ToMeasurementOptions(options, queries),
                cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await standardError.WriteLineAsync(
                $"eh: could not create host-review measurement: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string output = new HostReviewJsonRenderer(options.Compact).Render(measurement);
        return await WriteCliOutputAsync(
            output,
            options.OutputPath,
            "host-review measurement",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static HostReviewMeasurementOptions ToMeasurementOptions(
        ReviewMeasureCliOptions options,
        IReadOnlyList<HostReviewQueryPayloadInput> queries) => new()
        {
            SubjectId = options.SubjectId,
            SessionId = options.SessionId,
            Context = options.Context,
            Queries = queries,
            ElapsedMilliseconds = options.ElapsedMilliseconds,
            ElapsedBasis = options.ElapsedBasis,
            ProviderInputTokens = options.ProviderInputTokens,
            ProviderOutputTokens = options.ProviderOutputTokens,
            ProviderCachedInputTokens = options.ProviderCachedInputTokens,
            TokenBasis = options.TokenBasis,
            CostAmount = options.Cost,
            CostCurrency = options.Currency,
            CostBasis = options.CostBasis,
            AdditionalInputBytes = options.AdditionalInputBytes,
            AdditionalInputCharacters = options.AdditionalInputCharacters,
            AdditionalInputBasis = options.AdditionalInputBasis,
            AdditionalInputSizeReported = options.AdditionalInputSizeReported,
            ObservedInputComplete = options.InputComplete,
            BroaderSourceAvailableBeforeDecision = options.SourceSeenBefore,
            ReferenceReviewAvailableBeforeDecision = options.ReferenceSeenBefore,
            IndependentOfPairedReview = options.IndependentReviewer,
            ConditionNotes = options.Notes,
        };

    private const string ReviewMeasureHelpText = """
        Usage:
          eh review measure <packet.json> <adjustment.json> --subject <id> --session <id> --context <compact|broader-source> [options]

        Options:
          --query-result <path>                 Record one bounded query-result payload; repeatable
          --elapsed-ms <integer>                Caller-measured review duration
          --elapsed-basis <text>                Duration measurement method
          --provider-input-tokens <integer>     Caller-reported provider input tokens
          --provider-output-tokens <integer>    Caller-reported provider output tokens
          --provider-cached-input-tokens <int>  Caller-reported cached input tokens
          --token-basis <text>                  Provider report or tokenizer identity
          --cost <amount>                       Caller-reported monetary cost
          --currency <code>                     Three-letter cost currency
          --cost-basis <text>                   Cost report or calculation basis
          --additional-input-bytes <integer>    Authorized context beyond packet and queries
          --additional-input-characters <int>   Character count for that additional context
          --additional-input-basis <text>       How additional context was measured
          --input-complete                      Attest that all review input size is accounted for
          --source-seen-before                  Compact decision was not source-isolated
          --reference-seen-before               Reviewer saw the paired reference first
          --independent-reviewer                Reviewer is independent of the paired session
          --note <text>                         Record a non-sensitive condition note; repeatable
          --compact                             Emit compact JSON
          --output <path>                       Write to an explicit path instead of stdout
          -h, --help                            Show this help

        The adjustment must explicitly affirm or replace every packet candidate.
        Output does not copy repository identity, prompts, selectors, reasons, paths, or source.
        Caller-supplied IDs, telemetry bases, and notes are retained verbatim.
        Input ratios require --input-complete on both paired measurements.
        Source/reference-aware completeness also requires both additional-input size options.
        Missing provider tokens, elapsed time, and cost remain explicitly unavailable.
        """;
}
