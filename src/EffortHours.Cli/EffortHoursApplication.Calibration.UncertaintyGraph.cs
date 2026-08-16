using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ProjectUncertaintyGraphAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintyGraphHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments.Length < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration uncertainty graph projection requires an estimate and evidence path.")
                .ConfigureAwait(false);
        }

        string estimatePath = arguments[0];
        string evidencePath = arguments[1];
        bool compact = false;
        string? outputPath = null;
        for (int index = 2; index < arguments.Length; index++)
        {
            switch (arguments[index])
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
                    await standardOutput.WriteLineAsync(UncertaintyGraphHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration uncertainty-graph option '{arguments[index]}'.")
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

        CalibrationUncertaintyGraphFeatureReport report;
        try
        {
            report = CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);
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
                "The uncertainty graph projector produced a semantically invalid report.")
                .ConfigureAwait(false);
            foreach (string error in semanticErrors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        string json = compact ? ContractJson.SerializeCompact(report) : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyGraphFeatures,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The uncertainty graph projector produced an invalid schema document.")
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
            "calibration uncertainty graph feature report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string UncertaintyGraphHelpText = """
        Usage:
          eh calibration uncertainty-graph <estimate.json> <evidence.json> [options]

        Projects the frozen, label-independent .NET/JavaScript local dependency
        graph and public-interface distributions from a saved estimate and
        digest-matched evidence. It reads no labels, fits no model, and changes no
        estimate or interval.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the report to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
