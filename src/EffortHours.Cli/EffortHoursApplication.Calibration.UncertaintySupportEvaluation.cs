using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> EvaluateUncertaintySupportAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintySupportEvaluateHelpText)
                .ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments.Length < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration uncertainty support evaluation requires corpus and support " +
                "profile paths.").ConfigureAwait(false);
        }

        string corpusPath = arguments[0];
        string supportPath = arguments[1];
        List<string> featurePaths = [];
        bool compact = false;
        string? outputPath = null;
        for (int index = 2; index < arguments.Length; index++)
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
                    await standardOutput.WriteLineAsync(UncertaintySupportEvaluateHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    if (option.StartsWith("--", StringComparison.Ordinal))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            $"Unknown calibration uncertainty-support-evaluate option '{option}'.")
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
                "Calibration uncertainty support evaluation requires at least one feature report.")
                .ConfigureAwait(false);
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        CalibrationUncertaintySupportProfile? support = await LoadUncertaintySupportProfileAsync(
            supportPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null || support is null)
        {
            return CliExitCodes.InvalidInput;
        }

        List<CalibrationUncertaintyFeatureReport> reports = [];
        foreach (string path in featurePaths)
        {
            CalibrationUncertaintyFeatureReport? report = await LoadUncertaintyFeatureReportAsync(
                path,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (report is null)
            {
                return CliExitCodes.InvalidInput;
            }

            reports.Add(report);
        }

        CalibrationUncertaintySupportEvaluationReport evaluation;
        try
        {
            evaluation = CalibrationUncertaintyEvaluator.EvaluateSupportDevelopment(
                corpus,
                reports,
                support);
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
            SchemaNames.CalibrationUncertaintySupportEvaluation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The uncertainty support evaluation produced an invalid schema document.")
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
            "calibration uncertainty support evaluation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CalibrationUncertaintySupportProfile?>
        LoadUncertaintySupportProfileAsync(
            string path,
            TextWriter standardError,
            CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            await standardError.WriteLineAsync(
                $"Uncertainty support profile path was not found: {path}").ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not read uncertainty support profile: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintySupportProfile,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Input does not satisfy the uncertainty support profile schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationUncertaintySupportProfile profile;
        try
        {
            profile = ContractJson.Deserialize<CalibrationUncertaintySupportProfile>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize uncertainty support profile: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(profile);
        if (errors.Count == 0)
        {
            return profile;
        }

        await standardError.WriteLineAsync(
            "Uncertainty support profile is semantically invalid:").ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private const string UncertaintySupportEvaluateHelpText = """
        Usage:
          eh calibration uncertainty-support-evaluate <corpus.json> <support-profile.json> <features.json>... [options]

        Measures four frozen label-independent support and out-of-distribution signals
        against reviewed residuals with leave-one-repository-out development folds. It
        fits no production model and changes no estimate hours or intervals.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the report to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
