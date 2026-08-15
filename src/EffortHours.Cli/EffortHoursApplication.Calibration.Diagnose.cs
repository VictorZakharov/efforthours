using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> DiagnoseCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationDiagnoseHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string corpusPath = arguments[0];
        List<string> candidatePaths = [];
        CalibrationPartition? partition = null;
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

            if (option == "--partition")
            {
                if (index + 1 >= arguments.Length)
                {
                    return await UsageErrorAsync(
                        standardError,
                        "Option '--partition' requires a value.").ConfigureAwait(false);
                }

                string value = arguments[++index];
                if (!TryParseCalibrationPartition(value, out CalibrationPartition parsed))
                {
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration partition '{value}'.").ConfigureAwait(false);
                }

                if (partition is not null)
                {
                    return await UsageErrorAsync(
                        standardError,
                        "Option '--partition' can be supplied only once.").ConfigureAwait(false);
                }

                partition = parsed;
                continue;
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

            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(CalibrationDiagnoseHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option.StartsWith("--", StringComparison.Ordinal))
            {
                return await UsageErrorAsync(
                    standardError,
                    $"Unknown calibration diagnose option '{option}'.").ConfigureAwait(false);
            }

            candidatePaths.Add(option);
        }

        if (partition is null)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration diagnostics require '--partition <development|validation|test>'.")
                .ConfigureAwait(false);
        }

        if (candidatePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration diagnostics require at least one estimate path.").ConfigureAwait(false);
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        List<EstimateReport> candidates = [];
        foreach (string candidatePath in candidatePaths)
        {
            EstimateReport? candidate = await LoadEstimateAsync(
                candidatePath,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (candidate is null)
            {
                return CliExitCodes.InvalidInput;
            }

            candidates.Add(candidate);
        }

        CalibrationDiagnosticReport report;
        try
        {
            report = CalibrationDiagnostics.Diagnose(corpus, candidates, partition.Value);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationDiagnostic,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The calibration diagnostic produced an invalid report.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration diagnostic report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string CalibrationDiagnoseHelpText = """
        Usage:
          eh calibration diagnose <corpus.json> <estimate.json>... [options]

        Ranks every category and evidence-linked component residual, reports interval
        misses and symmetry, and proves exact reconciliation to repository totals.

        Options:
          --partition <development|validation|test>  Required repository-held-out partition
          --compact                                  Emit compact JSON
          --output <path>                            Write report to a path instead of stdout
          -h, --help                                 Show this help
        """;
}
