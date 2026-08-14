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
    private async Task<int> EstimateAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(EstimateHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string inputPath = arguments[0];
        EstimationProfile profile = EstimationProfile.Implementation;
        string format = "json";
        EstimateViewKind? view = null;
        bool compact = false;
        bool noRate = false;
        decimal? hourlyRate = null;
        string currency = "USD";
        bool currencyProvided = false;
        string? outputPath = null;

        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(EstimateHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option == "--compact")
            {
                compact = true;
                continue;
            }

            if (option == "--no-rate")
            {
                noRate = true;
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

                case "--hourly-rate":
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedRate) ||
                        parsedRate < 0m)
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Hourly rate must be a non-negative decimal using '.' as the decimal separator.").ConfigureAwait(false);
                    }

                    hourlyRate = parsedRate;
                    break;

                case "--currency":
                    currency = value.ToUpperInvariant();
                    currencyProvided = true;
                    if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Currency must be a three-letter uppercase currency code.").ConfigureAwait(false);
                    }

                    break;

                case "--output":
                    outputPath = value;
                    break;

                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown estimate option '{option}'.").ConfigureAwait(false);
            }
        }

        if (compact && format != "json")
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--compact' can only be used with JSON output.").ConfigureAwait(false);
        }

        if (noRate && (hourlyRate is not null || currencyProvided))
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--no-rate' cannot be combined with '--hourly-rate' or '--currency'.")
                .ConfigureAwait(false);
        }

        if (currencyProvided && hourlyRate is null)
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--currency' requires a caller-supplied '--hourly-rate'.")
                .ConfigureAwait(false);
        }

        RepositoryEvidence? evidence = await LoadEvidenceAsync(
            inputPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (evidence is null)
        {
            return CliExitCodes.InvalidInput;
        }

        RateCard? rateCard = noRate
            ? null
            : hourlyRate is null
                ? DefaultRateCatalog.RateCard
                : new RateCard
                {
                    Id = "user-supplied-cli-rate",
                    Name = "User-supplied CLI rate",
                    Currency = currency,
                    HourlyRate = hourlyRate.Value,
                    Methodology =
                        "Explicit caller override; this rate is not a EffortHours market-rate claim.",
                };

        EstimateReport report = _estimator.Estimate(evidence, profile, rateCard);
        string output = RenderEstimate(report, view, format, compact);
        return await WriteCliOutputAsync(
            output,
            outputPath,
            "estimate",
            standardOutput,
            standardError,
            cancellationToken,
            canonicalJson: format == "json").ConfigureAwait(false);
    }

    private async Task<RepositoryEvidence?> LoadEvidenceAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        RepositoryEvidence evidence;
        if (Directory.Exists(inputPath))
        {
            evidence = await _scanner.ScanAsync(
                inputPath,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (File.Exists(inputPath))
        {
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

            SchemaValidationResult schemaResult = ContractSchemaValidator.Validate(
                SchemaNames.RepositoryEvidence,
                json);
            if (!schemaResult.IsValid)
            {
                await standardError.WriteLineAsync(
                    "Evidence does not satisfy the repository evidence schema:")
                    .ConfigureAwait(false);
                foreach (string error in schemaResult.Errors)
                {
                    await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
                }

                return null;
            }

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
        }
        else
        {
            await standardError.WriteLineAsync(
                $"Repository or evidence path was not found: {inputPath}").ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(evidence);
        if (semanticErrors.Count == 0)
        {
            return evidence;
        }

        await standardError.WriteLineAsync("Evidence is semantically invalid:").ConfigureAwait(false);
        foreach (string error in semanticErrors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }

        return null;
    }

    private const string EstimateHelpText = """
        Usage:
          eh estimate <repository-or-evidence.json> [options]

        Options:
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --format <json|markdown>                Output format (default: json)
          --view <name>                           full (default), repository, category, scope,
                                                  work-item, or review
          --compact                               Emit compact JSON
          --hourly-rate <number>                  Override the bundled 2026 US rate
          --currency <code>                       Currency for an overridden rate (default: USD)
          --no-rate                               Omit rate and cost projection
          --output <path>                         Write output to an explicit path instead of stdout
          -h, --help                              Show this help
        """;

}
