using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ProfileUncertaintySupportAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintySupportHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string populationPath = arguments[0];
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
                    await standardOutput.WriteLineAsync(UncertaintySupportHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    if (option.StartsWith("--", StringComparison.Ordinal))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            $"Unknown calibration uncertainty-support option '{option}'.")
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
                "Calibration uncertainty support profiling requires at least one feature report path.")
                .ConfigureAwait(false);
        }

        CalibrationUncertaintySupportPopulation? population =
            await LoadUncertaintySupportPopulationAsync(
                populationPath,
                standardError,
                cancellationToken).ConfigureAwait(false);
        if (population is null)
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

        CalibrationUncertaintySupportProfile profile;
        try
        {
            profile = CalibrationUncertaintySupportProfiler.Profile(
                population,
                reports,
                cancellationToken);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(profile)
            : ContractJson.Serialize(profile);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintySupportProfile,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The uncertainty support profiler produced an invalid schema document.")
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
            "calibration uncertainty support profile",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CalibrationUncertaintySupportPopulation?>
        LoadUncertaintySupportPopulationAsync(
            string path,
            TextWriter standardError,
            CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            await standardError.WriteLineAsync(
                $"Uncertainty support population path was not found: {path}")
                .ConfigureAwait(false);
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
                $"Could not read uncertainty support population: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintySupportPopulation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Input does not satisfy the uncertainty support population schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationUncertaintySupportPopulation population;
        try
        {
            population = ContractJson.Deserialize<CalibrationUncertaintySupportPopulation>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize uncertainty support population: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(population);
        if (errors.Count == 0)
        {
            return population;
        }

        await standardError.WriteLineAsync(
            "Uncertainty support population is semantically invalid:").ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private const string UncertaintySupportHelpText = """
        Usage:
          eh calibration uncertainty-support <population.json> <features.json>... [options]

        Profiles repository-held-out sample support and label-independent OOD distance
        from immutable uncertainty feature reports. It accepts no reviewed corpus, fits
        no production model, and changes no estimate hours or intervals.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the profile to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
