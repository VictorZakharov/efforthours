using System.Globalization;
using System.Reflection;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Pricing;
using EffortHours.Reporting;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ReportAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ReportHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string inputPath = arguments[0];
        EstimateViewKind? view = EstimateViewKind.Review;
        string format = "json";
        bool compact = false;
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ReportHelpText).ConfigureAwait(false);
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
                case "--view":
                    if (value.Equals("full", StringComparison.OrdinalIgnoreCase))
                    {
                        view = null;
                    }
                    else if (!TryParseView(value, out EstimateViewKind parsedView))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "View must be 'full', 'repository', 'category', 'scope', " +
                            "'work-item', or 'review'.").ConfigureAwait(false);
                    }
                    else
                    {
                        view = parsedView;
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
                        $"Unknown report option '{option}'.").ConfigureAwait(false);
            }
        }

        if (compact && format != "json")
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--compact' can only be used with JSON output.").ConfigureAwait(false);
        }

        EstimateReport? report = await LoadEstimateAsync(
            inputPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (report is null)
        {
            return CliExitCodes.InvalidInput;
        }

        string output = RenderEstimate(report, view, format, compact);
        await standardOutput.WriteLineAsync(output.TrimEnd()).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private static async Task<EstimateReport?> LoadEstimateAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Estimate path was not found: {inputPath}")
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
            await standardError.WriteLineAsync($"Could not read estimate: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.EstimateReport,
            json);
        if (!schema.IsValid)
        {
            SchemaValidationResult changeSchema = ContractSchemaValidator.Validate(
                SchemaNames.ChangeEstimateReport,
                json);
            if (changeSchema.IsValid)
            {
                await standardError.WriteLineAsync(
                    "Input is a Change estimate report. Use a Change-specific command; " +
                    "for calibration authoring, run 'eh calibration change-scaffold'.")
                    .ConfigureAwait(false);
                return null;
            }

            await standardError.WriteLineAsync("Estimate does not satisfy the estimate-report schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        EstimateReport report;
        try
        {
            report = ContractJson.Deserialize<EstimateReport>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync($"Could not deserialize estimate: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(report);
        if (semanticErrors.Count == 0)
        {
            return report;
        }

        await standardError.WriteLineAsync("Estimate is semantically invalid:").ConfigureAwait(false);
        foreach (string error in semanticErrors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private static string RenderEstimate(
        EstimateReport report,
        EstimateViewKind? view,
        string format,
        bool compact)
    {
        if (view is null)
        {
            return format == "markdown"
                ? new MarkdownReportRenderer().Render(report)
                : new JsonReportRenderer(compact).Render(report);
        }

        EstimateViewReport projection = EstimateProjector.Project(report, view.Value);
        return format == "markdown"
            ? EstimateViewMarkdownRenderer.Render(projection)
            : new EstimateViewJsonRenderer(compact).Render(projection);
    }

    private const string ReportHelpText = """
        Usage:
          eh report <estimate.json> [options]

        Options:
          --view <name>             review (default), full, repository, category, scope,
                                    or work-item
          --format <json|markdown>  Output format (default: json)
          --compact                 Emit compact JSON
          -h, --help                Show this help
        """;

}
