using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> EvaluateChangeCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ChangeCalibrationEvaluateHelpText)
                .ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string corpusPath = arguments[0];
        List<string> estimatePaths = [];
        CalibrationPartition? partition = null;
        bool compact = false;
        string? outputPath = null;
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (option == "--compact")
            {
                compact = true;
                continue;
            }

            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ChangeCalibrationEvaluateHelpText)
                    .ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option is "--partition" or "--output")
            {
                if (index + 1 >= arguments.Length)
                {
                    return await UsageErrorAsync(
                        standardError,
                        $"Option '{option}' requires a value.").ConfigureAwait(false);
                }

                string value = arguments[++index];
                if (option == "--output")
                {
                    outputPath = value;
                    continue;
                }

                if (!TryParseCalibrationPartition(value, out CalibrationPartition parsed))
                {
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration partition '{value}'.").ConfigureAwait(false);
                }

                partition = parsed;
                continue;
            }

            if (option.StartsWith("--", StringComparison.Ordinal))
            {
                return await UsageErrorAsync(
                    standardError,
                    $"Unknown calibration change-evaluate option '{option}'.")
                    .ConfigureAwait(false);
            }

            estimatePaths.Add(option);
        }

        if (partition is null)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration change-evaluate requires --partition <development|validation|test>.")
                .ConfigureAwait(false);
        }

        if (estimatePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration change-evaluate requires at least one candidate change estimate path.")
                .ConfigureAwait(false);
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        List<ChangeEstimateReport> candidates = [];
        foreach (string estimatePath in estimatePaths)
        {
            ChangeEstimateReport? candidate = await LoadChangeEstimateAsync(
                estimatePath,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (candidate is null)
            {
                return CliExitCodes.InvalidInput;
            }

            candidates.Add(candidate);
        }

        CalibrationEvaluationReport report;
        try
        {
            report = ChangeCalibrationEvaluator.Evaluate(corpus, candidates, partition.Value);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationEvaluation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The Change calibration evaluator produced an invalid report.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "Change calibration evaluation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string ChangeCalibrationEvaluateHelpText = """
        Usage:
          eh calibration change-evaluate <corpus.json> <change-estimate.json>... --partition <name> [options]

        Evaluates immutable final-delta candidates against reviewed Change EHE labels.
        The partition is explicit; selection activity never enters the metrics.

        Options:
          --partition <development|validation|test>  Required corpus partition
          --compact                                  Emit compact JSON
          --output <path>                            Write to an explicit path
          -h, --help                                 Show this help
        """;
}
