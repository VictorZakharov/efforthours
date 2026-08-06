using Fairbill.Calibration;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Cli;

public sealed partial class FairbillApplication
{
    private static async Task<int> CompileChangeCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ChangeCalibrationCompileHelpText)
                .ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string planPath = arguments[0];
        List<string> estimatePaths = [];
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
                await standardOutput.WriteLineAsync(ChangeCalibrationCompileHelpText)
                    .ConfigureAwait(false);
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
                    $"Unknown calibration change-compile option '{option}'.")
                    .ConfigureAwait(false);
            }

            estimatePaths.Add(option);
        }

        if (estimatePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration change-compile requires at least one source change estimate path.")
                .ConfigureAwait(false);
        }

        CalibrationReviewPlan? plan = await LoadCalibrationReviewPlanAsync(
            planPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return CliExitCodes.InvalidInput;
        }

        List<ChangeEstimateReport> estimates = [];
        foreach (string estimatePath in estimatePaths)
        {
            ChangeEstimateReport? estimate = await LoadChangeEstimateAsync(
                estimatePath,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (estimate is null)
            {
                return CliExitCodes.InvalidInput;
            }

            estimates.Add(estimate);
        }

        CalibrationCorpus corpus;
        try
        {
            corpus = ChangeCalibrationReviewCompiler.Compile(plan, estimates);
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
                "The Change calibration compiler produced an invalid corpus.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "Change calibration corpus",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string ChangeCalibrationCompileHelpText = """
        Usage:
          fairbill calibration change-compile <review-plan.json> <change-estimate.json>... [options]

        Compiles completed Change capability decisions only after exact final-delta,
        immutable object, evidence, estimator, and source-estimate identities match.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the corpus to an explicit path instead of stdout
          -h, --help       Show this help
        """;
}
