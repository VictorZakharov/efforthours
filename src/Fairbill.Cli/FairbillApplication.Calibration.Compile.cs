using System.Globalization;
using System.Reflection;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Calibration;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;
using Fairbill.Estimation;
using Fairbill.Pricing;
using Fairbill.Reporting;

namespace Fairbill.Cli;

public sealed partial class FairbillApplication
{
    private static async Task<int> EvaluateMutationsAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationMutationsHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string suitePath = arguments[0];
        List<string> candidatePaths = [];
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
                await standardOutput.WriteLineAsync(CalibrationMutationsHelpText).ConfigureAwait(false);
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
                    $"Unknown calibration mutations option '{option}'.").ConfigureAwait(false);
            }

            candidatePaths.Add(option);
        }

        if (candidatePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration mutation evaluation requires at least one estimate path.")
                .ConfigureAwait(false);
        }

        CalibrationMutationSuite? suite = await LoadCalibrationMutationSuiteAsync(
            suitePath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (suite is null)
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

        CalibrationMutationReport report;
        try
        {
            report = CalibrationMutationEvaluator.Evaluate(suite, candidates);
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
            SchemaNames.CalibrationMutationReport,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The calibration mutation evaluator produced an invalid report.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        int outputCode = await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration mutation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (outputCode != CliExitCodes.Success)
        {
            return outputCode;
        }

        return report.AllPassed
            ? CliExitCodes.Success
            : CliExitCodes.CalibrationRegression;
    }

    private static async Task<int> CompileCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationCompileHelpText).ConfigureAwait(false);
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
                await standardOutput.WriteLineAsync(CalibrationCompileHelpText).ConfigureAwait(false);
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
                    $"Unknown calibration compile option '{option}'.").ConfigureAwait(false);
            }

            estimatePaths.Add(option);
        }

        if (estimatePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration compilation requires at least one source estimate path.")
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

        List<EstimateReport> estimates = [];
        foreach (string estimatePath in estimatePaths)
        {
            EstimateReport? estimate = await LoadEstimateAsync(
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
            corpus = CalibrationReviewCompiler.Compile(plan, estimates);
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
                "The calibration review compiler produced an invalid corpus.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration corpus",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string CalibrationMutationsHelpText = """
        Usage:
          fairbill calibration mutations <suite.json> <estimate.json>... [options]

        Evaluates deterministic relational guardrails over synthetic mutation cases.
        A complete report is emitted; failed assertions return exit code 5.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the mutation report to an explicit path instead of stdout
          -h, --help       Show this help
        """;

    private const string CalibrationCompileHelpText = """
        Usage:
          fairbill calibration compile <review-plan.json> <estimate.json>... [options]

        Compiles completed capability-level review decisions into a calibration corpus.
        Every represented source capability must be reviewed; source estimates and their
        digests must exactly match the plan. The command is deterministic and offline.

        Options:
          --compact        Emit compact JSON
          --output <path>  Write the corpus to an explicit path instead of stdout
          -h, --help       Show this help
        """;

}
