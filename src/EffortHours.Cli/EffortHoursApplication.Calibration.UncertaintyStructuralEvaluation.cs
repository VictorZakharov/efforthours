using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> EvaluateUncertaintyStructureAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintyStructureEvaluateHelpText)
                .ConfigureAwait(false);
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
                    await standardOutput.WriteLineAsync(UncertaintyStructureEvaluateHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    if (option.StartsWith("--", StringComparison.Ordinal))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            $"Unknown calibration uncertainty-structure-evaluate option " +
                            $"'{option}'.").ConfigureAwait(false);
                    }

                    featurePaths.Add(option);
                    break;
            }
        }

        if (featurePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration structural uncertainty evaluation requires at least one feature " +
                "report path.").ConfigureAwait(false);
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        List<CalibrationUncertaintyStructuralFeatureReport> reports = [];
        foreach (string path in featurePaths)
        {
            CalibrationUncertaintyStructuralFeatureReport? report =
                await LoadUncertaintyStructuralFeatureReportAsync(
                    path,
                    standardError,
                    cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return CliExitCodes.InvalidInput;
            }

            reports.Add(report);
        }

        CalibrationUncertaintyStructuralEvaluationReport evaluation;
        try
        {
            evaluation = CalibrationUncertaintyEvaluator.EvaluateStructuralDevelopment(
                corpus,
                reports);
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
            SchemaNames.CalibrationUncertaintyStructuralEvaluation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The structural uncertainty evaluation produced an invalid schema document.")
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
            "calibration structural uncertainty evaluation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CalibrationUncertaintyStructuralFeatureReport?>
        LoadUncertaintyStructuralFeatureReportAsync(
            string inputPath,
            TextWriter standardError,
            CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync(
                $"Structural uncertainty feature report path was not found: {inputPath}")
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
                $"Could not read structural uncertainty feature report: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyStructuralFeatures,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Input does not satisfy the structural uncertainty feature schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationUncertaintyStructuralFeatureReport report;
        try
        {
            report = ContractJson.Deserialize<CalibrationUncertaintyStructuralFeatureReport>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize structural uncertainty feature report: " +
                exception.Message).ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count == 0)
        {
            return report;
        }

        await standardError.WriteLineAsync(
            "Structural uncertainty feature report is semantically invalid:")
            .ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private const string UncertaintyStructureEvaluateHelpText = """
        Usage:
          eh calibration uncertainty-structure-evaluate <corpus.json> <structural-features.json>... [options]

        Measures the frozen label-independent structural diagnostics against reviewed
        expected-point residuals with leave-one-repository-out development folds. It
        rejects validation or test labels, fits no production model, and changes no
        estimate hours or intervals.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the report to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
