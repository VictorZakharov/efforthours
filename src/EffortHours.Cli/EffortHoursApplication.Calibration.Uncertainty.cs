using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ProjectUncertaintyFeaturesAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintyFeaturesHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments.Length < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration uncertainty feature projection requires an estimate and evidence path.")
                .ConfigureAwait(false);
        }

        string estimatePath = arguments[0];
        string evidencePath = arguments[1];
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
                    await standardOutput.WriteLineAsync(UncertaintyFeaturesHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration uncertainty-features option '{option}'.")
                        .ConfigureAwait(false);
            }
        }

        EstimateReport? estimate = await LoadEstimateAsync(
            estimatePath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        RepositoryEvidence? evidence = await LoadSavedEvidenceAsync(
            evidencePath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (estimate is null || evidence is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationUncertaintyFeatureReport report;
        try
        {
            report = CalibrationUncertaintyFeatureProjector.Project(estimate, evidence);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(report);
        if (semanticErrors.Count > 0)
        {
            await standardError.WriteLineAsync(
                "The uncertainty feature projector produced a semantically invalid report.")
                .ConfigureAwait(false);
            foreach (string error in semanticErrors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyFeatures,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The uncertainty feature projector produced an invalid schema document.")
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
            "calibration uncertainty feature report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<RepositoryEvidence?> LoadSavedEvidenceAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Evidence path was not found: {inputPath}")
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
            await standardError.WriteLineAsync($"Could not read evidence: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Evidence does not satisfy the repository-evidence schema:").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        RepositoryEvidence evidence;
        try
        {
            evidence = ContractJson.Deserialize<RepositoryEvidence>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync($"Could not deserialize evidence: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(evidence);
        if (errors.Count == 0)
        {
            return evidence;
        }

        await standardError.WriteLineAsync("Evidence is semantically invalid:").ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private const string UncertaintyFeaturesHelpText = """
        Usage:
          eh calibration uncertainty-features <estimate.json> <evidence.json> [options]

        Projects the frozen, label-independent uncertainty feature contract from a
        saved estimate and its digest-matched saved repository evidence. It does not
        scan a repository, read calibration labels, fit a model, or change hours.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the report to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
