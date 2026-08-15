using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> EvaluateUncertaintyFeaturesAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintyEvaluateHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string corpusPath = arguments[0];
        List<string> featurePaths = [];
        bool compact = false;
        string? outputPath = null;
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            switch (option)
            {
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
                    await standardOutput.WriteLineAsync(UncertaintyEvaluateHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    if (option.StartsWith("--", StringComparison.Ordinal))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            $"Unknown calibration uncertainty-evaluate option '{option}'.")
                            .ConfigureAwait(false);
                    }

                    featurePaths.Add(option);
                    break;
            }
        }

        if (featurePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration uncertainty evaluation requires at least one feature report path.")
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

        List<CalibrationUncertaintyFeatureReport> reports = [];
        foreach (string path in featurePaths)
        {
            CalibrationUncertaintyFeatureReport? report =
                await LoadUncertaintyFeatureReportAsync(
                    path,
                    standardError,
                    cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return CliExitCodes.InvalidInput;
            }

            reports.Add(report);
        }

        CalibrationUncertaintyEvaluationReport evaluation;
        try
        {
            evaluation = CalibrationUncertaintyEvaluator.EvaluateDevelopment(corpus, reports);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(evaluation)
            : ContractJson.Serialize(evaluation);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyEvaluation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The uncertainty feature evaluation produced an invalid schema document.")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration uncertainty evaluation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CalibrationUncertaintyFeatureReport?>
        LoadUncertaintyFeatureReportAsync(
            string inputPath,
            TextWriter standardError,
            CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync(
                $"Uncertainty feature report path was not found: {inputPath}")
                .ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not read uncertainty feature report: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyFeatures,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Input does not satisfy the calibration uncertainty feature schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationUncertaintyFeatureReport report;
        try
        {
            report = ContractJson.Deserialize<CalibrationUncertaintyFeatureReport>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize uncertainty feature report: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count == 0)
        {
            return report;
        }

        await standardError.WriteLineAsync(
            "Uncertainty feature report is semantically invalid:").ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private const string UncertaintyEvaluateHelpText = """
        Usage:
          eh calibration uncertainty-evaluate <corpus.json> <features.json>... [options]

        Measures frozen offline uncertainty features against reviewed expected-point
        residuals with leave-one-repository-out development folds. It rejects any
        validation or test record, fits no production model, and changes no estimate hours.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the report to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
