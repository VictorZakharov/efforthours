using System.Globalization;
using System.Reflection;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Pricing;
using EffortHours.Reporting;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> CalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        return arguments[0] switch
        {
            "scaffold" => await ScaffoldCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "change-scaffold" => await ScaffoldChangeCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "compile" => await CompileCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "change-compile" => await CompileChangeCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "review-scaffold" => await ScaffoldCorpusReviewAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "review-compile" => await CompileCorpusReviewAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "mutations" => await EvaluateMutationsAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "validate" => await ValidateCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "evaluate" => await EvaluateCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "diagnose" => await DiagnoseCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "uncertainty-features" => await ProjectUncertaintyFeaturesAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "uncertainty-structure" => await ProjectUncertaintyStructureAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "uncertainty-evaluate" => await EvaluateUncertaintyFeaturesAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "uncertainty-structure-evaluate" => await EvaluateUncertaintyStructureAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "uncertainty-support" => await ProfileUncertaintySupportAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "uncertainty-support-evaluate" => await EvaluateUncertaintySupportAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "change-evaluate" => await EvaluateChangeCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            _ => await UsageErrorAsync(
                standardError,
                "Expected 'calibration scaffold', 'calibration change-scaffold', " +
                "'calibration compile', 'calibration change-compile', " +
                "'calibration review-scaffold', 'calibration review-compile', " +
                "'calibration mutations', 'calibration validate', 'calibration evaluate', " +
                "'calibration diagnose', 'calibration uncertainty-features', " +
                "'calibration uncertainty-structure', " +
                "'calibration uncertainty-evaluate', " +
                "'calibration uncertainty-structure-evaluate', " +
                "'calibration uncertainty-support', " +
                "'calibration uncertainty-support-evaluate', or " +
                "'calibration change-evaluate'.")
                .ConfigureAwait(false),
        };
    }

    private static async Task<int> ScaffoldCorpusReviewAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationReviewScaffoldHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string corpusPath = arguments[0];
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
                    await standardOutput.WriteLineAsync(CalibrationReviewScaffoldHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration review-scaffold option '{option}'.").ConfigureAwait(false);
            }
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationCorpusReviewPacket packet;
        try
        {
            packet = CalibrationCorpusReviewAuthoring.Scaffold(corpus, blind);
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
            SchemaNames.CalibrationCorpusReviewPacket,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration corpus review authoring produced an invalid packet.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration corpus review packet",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> CompileCorpusReviewAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationReviewCompileHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string planPath = arguments[0];
        string? corpusPath = null;
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
                await standardOutput.WriteLineAsync(CalibrationReviewCompileHelpText).ConfigureAwait(false);
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

            if (option.StartsWith("--", StringComparison.Ordinal))
            {
                return await UsageErrorAsync(
                    standardError,
                    $"Unknown calibration review-compile option '{option}'.").ConfigureAwait(false);
            }

            if (corpusPath is not null)
            {
                return await UsageErrorAsync(
                    standardError,
                    "Calibration review compilation accepts exactly one source corpus path.")
                    .ConfigureAwait(false);
            }

            corpusPath = option;
        }

        if (corpusPath is null)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration review compilation requires a source corpus path.").ConfigureAwait(false);
        }

        CalibrationCorpusReviewPlan? plan = await LoadCalibrationCorpusReviewPlanAsync(
            planPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationCorpus? sourceCorpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (sourceCorpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationCorpus corpus;
        try
        {
            corpus = CalibrationCorpusReviewCompiler.Compile(plan, sourceCorpus);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(corpus)
            : ContractJson.Serialize(corpus);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The calibration corpus review compiler produced an invalid corpus.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "reviewed calibration corpus",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string CalibrationReviewScaffoldHelpText = """
        Usage:
          eh calibration review-scaffold <corpus.json> [options]

        Produces an explicitly unreviewed second-pass packet from an existing corpus.
        Blind mode hides prior ranges, rationale, uncertainty, and record totals.

        Options:
          --blind          Hide prior target judgments and record totals
          --compact        Emit compact JSON
          --output <path>  Write the packet to an explicit path instead of stdout
          -h, --help       Show this help
        """;

    private const string CalibrationReviewCompileHelpText = """
        Usage:
          eh calibration review-compile <plan.json> <corpus.json> [options]

        Advances every source record through explicit accept/replace decisions.
        The exact source-corpus digest and distinct subsequent reviewer identities
        are required. Structural target and evidence lineage remains unchanged.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the reviewed corpus to an explicit path instead of stdout
          -h, --help       Show this help
        """;

}
