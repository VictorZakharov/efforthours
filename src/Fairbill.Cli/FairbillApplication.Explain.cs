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
    private async Task<int> ExplainAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ExplainHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string inputPath = arguments[0];
        EstimationProfile profile = EstimationProfile.Implementation;
        string format = "json";
        string? itemId = null;
        bool compact = false;
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ExplainHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option == "--compact")
            {
                compact = true;
                continue;
            }

            if (index + 1 >= arguments.Length)
            {
                return await UsageErrorAsync(
                    standardError,
                    $"Option '{option}' requires a value.").ConfigureAwait(false);
            }

            string value = arguments[++index];
            switch (option)
            {
                case "--item":
                    itemId = value;
                    break;

                case "--profile":
                    if (!TryParseProfile(value, out profile))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Profile must be 'implementation' or 'recreation'.").ConfigureAwait(false);
                    }

                    break;

                case "--format":
                    format = value.ToLowerInvariant();
                    if (format is not ("json" or "markdown"))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Format must be 'json' or 'markdown'.").ConfigureAwait(false);
                    }

                    break;

                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown explain option '{option}'.").ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--item' is required.").ConfigureAwait(false);
        }

        if (compact && format != "json")
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--compact' can only be used with JSON output.").ConfigureAwait(false);
        }

        RepositoryEvidence? evidence = await LoadEvidenceAsync(
            inputPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (evidence is null)
        {
            return CliExitCodes.InvalidInput;
        }

        EstimateReport report = _estimator.Estimate(evidence, profile);
        EstimateExplanation explanation;
        try
        {
            explanation = EstimateProjector.Explain(report, evidence, itemId);
        }
        catch (KeyNotFoundException exception)
        {
            await standardError.WriteLineAsync($"fairbill: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        catch (ArgumentException exception)
        {
            await standardError.WriteLineAsync($"fairbill: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string output = format == "markdown"
            ? EstimateExplanationMarkdownRenderer.Render(explanation)
            : new EstimateExplanationJsonRenderer(compact).Render(explanation);
        await standardOutput.WriteLineAsync(output.TrimEnd()).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private const string ExplainHelpText = """
        Usage:
          fairbill explain <repository-or-evidence.json> --item <id> [options]

        Options:
          --item <id>                             Work-item or capability ID (required)
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --format <json|markdown>                Output format (default: json)
          --compact                               Emit compact JSON
          -h, --help                              Show this help
        """;

}
