using System.Globalization;
using System.Reflection;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Calibration;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;
using Fairbill.Estimation;
using Fairbill.Pricing;
using Fairbill.Reporting;

namespace Fairbill.Cli;

public sealed partial class FairbillApplication
{
    private static async Task<int> ScaffoldCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationScaffoldHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string estimatePath = arguments[0];
        bool blind = false;
        bool compact = false;
        string? outputPath = null;
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            switch (option)
            {
                case "--blind":
                    blind = true;
                    break;
                case "--compact":
                    compact = true;
                    break;
                case "--output":
                    if (index + 1 >= arguments.Length)
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Option '--output' requires a value.").ConfigureAwait(false);
                    }

                    outputPath = arguments[++index];
                    break;
                case "-h":
                case "--help":
                    await standardOutput.WriteLineAsync(CalibrationScaffoldHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration scaffold option '{option}'.").ConfigureAwait(false);
            }
        }

        EstimateReport? estimate = await LoadEstimateAsync(
            estimatePath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (estimate is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationAuthoringPacket packet;
        try
        {
            packet = CalibrationAuthoring.Scaffold(estimate, blind);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(packet)
            : ContractJson.Serialize(packet);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration authoring produced an invalid packet.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration authoring packet",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ValidateCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationValidateHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string corpusPath = arguments[0];
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
                await standardOutput.WriteLineAsync(CalibrationValidateHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option == "--output")
            {
                if (index + 1 >= arguments.Length)
                {
                    return await UsageErrorAsync(
                        standardError,
                        "Option '--output' requires a value.").ConfigureAwait(false);
                }

                outputPath = arguments[++index];
                continue;
            }

            return await UsageErrorAsync(
                standardError,
                $"Unknown calibration validate option '{option}'.").ConfigureAwait(false);
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationValidationSummary summary;
        try
        {
            summary = CalibrationEvaluator.Summarize(corpus);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(summary)
            : ContractJson.Serialize(summary);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationValidation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The calibration validator produced an invalid summary.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration validation summary",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string CalibrationScaffoldHelpText = """
        Usage:
          fairbill calibration scaffold <estimate.json> [options]

        Produces a schema-versioned, explicitly unreviewed authoring packet. Candidate
        values are reference material and cannot be consumed as calibration labels.

        Options:
          --blind          Hide candidate hours, category totals, and confidence while reviewing
          --compact        Emit compact JSON
          --output <path>  Write the packet to an explicit path instead of stdout
          -h, --help       Show this help
        """;

    private const string CalibrationValidateHelpText = """
        Usage:
          fairbill calibration validate <corpus.json> [--compact] [--output <path>]
        """;

}
