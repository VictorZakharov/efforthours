using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ProjectUncertaintyStructureAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(UncertaintyStructureHelpText)
                .ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments.Length < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration uncertainty structure projection requires an estimate and evidence path.")
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
                    await standardOutput.WriteLineAsync(UncertaintyStructureHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration uncertainty-structure option '{arguments[index]}'.")
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

        CalibrationUncertaintyStructuralFeatureReport report;
        try
        {
            report = CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence);
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
                "The uncertainty structural feature projector produced a semantically invalid report.")
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
            SchemaNames.CalibrationUncertaintyStructuralFeatures,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The uncertainty structural feature projector produced an invalid schema document.")
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
            "calibration uncertainty structural feature report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string UncertaintyStructureHelpText = """
        Usage:
          eh calibration uncertainty-structure <estimate.json> <evidence.json> [options]

        Projects the frozen, label-independent callable size, bounded decision
        complexity, nesting, parser coverage, and analyzer ambiguity features from a
        saved estimate and digest-matched evidence. It does not read labels, fit a
        model, or change estimate hours.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the report to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
