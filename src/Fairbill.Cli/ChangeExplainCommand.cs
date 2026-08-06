using System.Globalization;
using System.Text;
using Fairbill.Change;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Pricing;
using Fairbill.Reporting;

namespace Fairbill.Cli;

internal static class ChangeExplainCommand
{
    public static async Task<int> ExecuteAsync(
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
        string? itemId = null;
        string format = "json";
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
                        $"Unknown change explain option '{option}'.").ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return await UsageErrorAsync(standardError, "Option --item is required.")
                .ConfigureAwait(false);
        }

        if (compact && format != "json")
        {
            return await UsageErrorAsync(
                standardError,
                "Option --compact can only be used with JSON output.").ConfigureAwait(false);
        }

        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Change estimate path was not found: {inputPath}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"Could not read change estimate: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeEstimateReport,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Change estimate does not satisfy the change-estimate-report schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InvalidInput;
        }

        ChangeEstimateReport report;
        try
        {
            report = ContractJson.Deserialize<ChangeEstimateReport>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize change estimate: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        try
        {
            ChangeEstimateExplanation explanation = ChangeEstimateExplainer.Explain(report, itemId);
            string output = format == "markdown"
                ? ChangeEstimateExplanationMarkdownRenderer.Render(explanation)
                : new ChangeEstimateExplanationJsonRenderer(compact).Render(explanation);
            await standardOutput.WriteLineAsync(output.TrimEnd()).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (KeyNotFoundException exception)
        {
            await standardError.WriteLineAsync($"fairbill: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
    }

    private const string ExplainHelpText = """
        Usage:
          fairbill change explain <change-estimate.json> --item <id> [options]

        Options:
          --item <id>               Exact change work-item ID (required)
          --format <json|markdown>  Output format (default: json)
          --compact                 Emit compact JSON
          -h, --help                Show this help
        """;

    private static async Task<int> UsageErrorAsync(TextWriter standardError, string message)
    {
        await standardError.WriteLineAsync($"fairbill: {message}").ConfigureAwait(false);
        await standardError.WriteLineAsync("Run 'fairbill change explain --help' for usage.").ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h";
}
