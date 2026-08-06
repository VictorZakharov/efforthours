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

public sealed class FairbillApplication
{
    private readonly IEstimator _estimator;
    private readonly IRepositoryScanner _scanner;

    public FairbillApplication()
        : this(new SeedEstimator(), new RepositoryAnalysisPipeline())
    {
    }

    public FairbillApplication(IEstimator estimator)
        : this(estimator, new RepositoryAnalysisPipeline())
    {
    }

    public FairbillApplication(IEstimator estimator, IRepositoryScanner scanner)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
    }

    public async Task<int> RunAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(HelpText).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        try
        {
            return arguments[0].ToLowerInvariant() switch
            {
                "scan" => await ScanAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "estimate" => await EstimateAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "report" => await ReportAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "explain" => await ExplainAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "calibration" => await CalibrationAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "schema" => await SchemaAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError).ConfigureAwait(false),
                "model" => await ModelAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError).ConfigureAwait(false),
                "rate" => await RateAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError).ConfigureAwait(false),
                "version" or "--version" or "-v" => await VersionAsync(standardOutput).ConfigureAwait(false),
                _ => await UsageErrorAsync(
                    standardError,
                    $"Unknown command '{arguments[0]}'.").ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException)
        {
            await standardError.WriteLineAsync("The operation was cancelled.").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await standardError.WriteLineAsync($"fairbill: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InternalError;
        }
        catch (Exception exception)
        {
            await standardError.WriteLineAsync(
                $"fairbill: an unexpected internal error occurred: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InternalError;
        }
    }

    private async Task<int> ScanAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ScanHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string repositoryPath = arguments[0];
        string? outputPath = null;
        RepositoryScanOptions options = new();
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            switch (option)
            {
                case "--output":
                    if (index + 1 >= arguments.Length)
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Option '--output' requires a value.").ConfigureAwait(false);
                    }

                    outputPath = arguments[++index];
                    break;

                case "--cache":
                    if (index + 1 >= arguments.Length)
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Option '--cache' requires a value.").ConfigureAwait(false);
                    }

                    options = options with { CachePath = arguments[++index] };
                    break;

                case "--no-gitignore":
                    options = options with { RespectGitIgnore = false };
                    break;

                case "--no-fairbillignore":
                    options = options with { RespectFairbillIgnore = false };
                    break;

                case "help" or "--help" or "-h":
                    await standardOutput.WriteLineAsync(ScanHelpText).ConfigureAwait(false);
                    return CliExitCodes.Success;

                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown scan option '{option}'.").ConfigureAwait(false);
            }
        }

        if (!Directory.Exists(repositoryPath))
        {
            await standardError.WriteLineAsync($"Repository directory was not found: {repositoryPath}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        RepositoryEvidence evidence;
        try
        {
            evidence = await _scanner.ScanAsync(
                repositoryPath,
                options,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await standardError.WriteLineAsync($"Could not scan repository: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(evidence);
        string json = ContractJson.Serialize(evidence);
        SchemaValidationResult schemaResult = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        if (semanticErrors.Count > 0 || !schemaResult.IsValid)
        {
            await standardError.WriteLineAsync("The scanner produced invalid repository evidence.")
                .ConfigureAwait(false);
            foreach (string error in semanticErrors.Concat(schemaResult.Errors))
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        if (outputPath is null)
        {
            await standardOutput.WriteLineAsync(json).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (Directory.Exists(outputPath))
        {
            await standardError.WriteLineAsync($"Output path is a directory: {outputPath}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        try
        {
            string fullOutputPath = Path.GetFullPath(outputPath);
            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(
                fullOutputPath,
                json + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"Could not write evidence: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        return CliExitCodes.Success;
    }

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
                        "Explicit caller override; this rate is not a Fairbill market-rate claim.",
                };

        EstimateReport report = _estimator.Estimate(evidence, profile, rateCard);
        string output = RenderEstimate(report, view, format, compact);
        return await WriteCliOutputAsync(
            output,
            outputPath,
            "estimate",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

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

    private static async Task<int> WriteCliOutputAsync(
        string content,
        string? outputPath,
        string artifactName,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (outputPath is null)
        {
            await standardOutput.WriteLineAsync(content.TrimEnd()).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        try
        {
            string fullOutputPath = Path.GetFullPath(outputPath);
            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(
                fullOutputPath,
                content.TrimEnd() + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not write {artifactName}: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        return CliExitCodes.Success;
    }

    private static async Task<int> CalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        return arguments[0] switch
        {
            "scaffold" => await ScaffoldCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "compile" => await CompileCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "validate" => await ValidateCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "evaluate" => await EvaluateCalibrationAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            _ => await UsageErrorAsync(
                standardError,
                "Expected 'calibration scaffold', 'calibration compile', " +
                "'calibration validate', or 'calibration evaluate'.")
                .ConfigureAwait(false),
        };
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

    private static async Task<int> ScaffoldCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationScaffoldHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string estimatePath = arguments[0];
        bool blind = false;
        bool compact = false;
        string? outputPath = null;
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            switch (option)
            {
                case "--blind":
                    blind = true;
                    break;
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
                    await standardOutput.WriteLineAsync(CalibrationScaffoldHelpText)
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration scaffold option '{option}'.").ConfigureAwait(false);
            }
        }

        EstimateReport? estimate = await LoadEstimateAsync(
            estimatePath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (estimate is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationAuthoringPacket packet;
        try
        {
            packet = CalibrationAuthoring.Scaffold(estimate, blind);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(packet)
            : ContractJson.Serialize(packet);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration authoring produced an invalid packet.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration authoring packet",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ValidateCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationValidateHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string corpusPath = arguments[0];
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
                await standardOutput.WriteLineAsync(CalibrationValidateHelpText).ConfigureAwait(false);
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

            return await UsageErrorAsync(
                standardError,
                $"Unknown calibration validate option '{option}'.").ConfigureAwait(false);
        }

        CalibrationCorpus? corpus = await LoadCalibrationCorpusAsync(
            corpusPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (corpus is null)
        {
            return CliExitCodes.InvalidInput;
        }

        CalibrationValidationSummary summary;
        try
        {
            summary = CalibrationEvaluator.Summarize(corpus);
        }
        catch (CalibrationEvaluationException exception)
        {
            await WriteCalibrationErrorsAsync(standardError, exception.Errors).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string json = compact
            ? ContractJson.SerializeCompact(summary)
            : ContractJson.Serialize(summary);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationValidation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The calibration validator produced an invalid summary.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration validation summary",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> EvaluateCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(CalibrationEvaluateHelpText).ConfigureAwait(false);
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
                await standardOutput.WriteLineAsync(CalibrationEvaluateHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option.StartsWith("--", StringComparison.Ordinal))
            {
                return await UsageErrorAsync(
                    standardError,
                    $"Unknown calibration evaluate option '{option}'.").ConfigureAwait(false);
            }

            candidatePaths.Add(option);
        }

        if (partition is null)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration evaluation requires '--partition <development|validation|test>'.")
                .ConfigureAwait(false);
        }

        if (candidatePaths.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration evaluation requires at least one estimate path.").ConfigureAwait(false);
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

        CalibrationEvaluationReport report;
        try
        {
            report = CalibrationEvaluator.Evaluate(corpus, candidates, partition.Value);
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
            SchemaNames.CalibrationEvaluation,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "The calibration evaluator produced an invalid report.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "calibration evaluation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CalibrationCorpus?> LoadCalibrationCorpusAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Calibration corpus path was not found: {inputPath}")
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
            await standardError.WriteLineAsync($"Could not read calibration corpus: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration corpus does not satisfy the calibration-corpus schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationCorpus corpus;
        try
        {
            corpus = ContractJson.Deserialize<CalibrationCorpus>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize calibration corpus: {exception.Message}").ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(corpus);
        if (errors.Count == 0)
        {
            return corpus;
        }

        await WriteCalibrationErrorsAsync(standardError, errors).ConfigureAwait(false);
        return null;
    }

    private static async Task<CalibrationReviewPlan?> LoadCalibrationReviewPlanAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync($"Calibration review-plan path was not found: {inputPath}")
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
            await standardError.WriteLineAsync($"Could not read calibration review plan: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            json);
        if (!schema.IsValid)
        {
            await standardError.WriteLineAsync(
                "Calibration review plan does not satisfy the calibration-review-plan schema:")
                .ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return null;
        }

        CalibrationReviewPlan plan;
        try
        {
            plan = ContractJson.Deserialize<CalibrationReviewPlan>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize calibration review plan: {exception.Message}").ConfigureAwait(false);
            return null;
        }

        IReadOnlyList<string> errors = ContractValidation.Validate(plan);
        if (errors.Count == 0)
        {
            return plan;
        }

        await WriteCalibrationErrorsAsync(standardError, errors).ConfigureAwait(false);
        return null;
    }

    private static async Task WriteCalibrationErrorsAsync(
        TextWriter standardError,
        IReadOnlyList<string> errors)
    {
        await standardError.WriteLineAsync("Calibration input is semantically invalid:")
            .ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }
    }

    private static async Task<int> SchemaAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(SchemaHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        switch (arguments[0])
        {
            case "list" when arguments.Length == 1:
                foreach (string name in ContractSchemaCatalog.Names)
                {
                    await standardOutput.WriteLineAsync(name).ConfigureAwait(false);
                }

                return CliExitCodes.Success;

            case "show" when arguments.Length == 2:
                try
                {
                    await standardOutput.WriteLineAsync(ContractSchemaCatalog.Read(arguments[1]).TrimEnd())
                        .ConfigureAwait(false);
                    return CliExitCodes.Success;
                }
                catch (ArgumentOutOfRangeException)
                {
                    await standardError.WriteLineAsync($"Unknown schema '{arguments[1]}'.").ConfigureAwait(false);
                    return CliExitCodes.InvalidInput;
                }

            default:
                return await UsageErrorAsync(standardError, "Expected 'schema list' or 'schema show <name>'.")
                    .ConfigureAwait(false);
        }
    }

    private static async Task<int> ModelAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ModelHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments is ["info"])
        {
            await standardOutput.WriteLineAsync(
                ContractJson.Serialize(SeedRuleCatalog.Info)).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (arguments is ["show"])
        {
            await standardOutput.WriteLineAsync(
                SeedRuleCatalog.ReadJson().TrimEnd()).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        return await UsageErrorAsync(standardError, "Expected 'model info' or 'model show'.")
            .ConfigureAwait(false);
    }

    private static async Task<int> RateAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(RateHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments is ["info"])
        {
            await standardOutput.WriteLineAsync(
                ContractJson.Serialize(DefaultRateCatalog.Info)).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (arguments is ["show"])
        {
            await standardOutput.WriteLineAsync(
                DefaultRateCatalog.ReadJson().TrimEnd()).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        return await UsageErrorAsync(standardError, "Expected 'rate info' or 'rate show'.")
            .ConfigureAwait(false);
    }

    private static async Task<int> VersionAsync(TextWriter standardOutput)
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        await standardOutput.WriteLineAsync(version).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private static async Task<int> UsageErrorAsync(TextWriter standardError, string message)
    {
        await standardError.WriteLineAsync($"fairbill: {message}").ConfigureAwait(false);
        await standardError.WriteLineAsync("Run 'fairbill --help' for usage.").ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h";

    private static bool TryParseProfile(string value, out EstimationProfile profile)
    {
        switch (value.ToLowerInvariant())
        {
            case "implementation":
                profile = EstimationProfile.Implementation;
                return true;
            case "recreation":
                profile = EstimationProfile.Recreation;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    private static bool TryParseView(string value, out EstimateViewKind view)
    {
        switch (value.ToLowerInvariant())
        {
            case "repository":
                view = EstimateViewKind.Repository;
                return true;
            case "category":
                view = EstimateViewKind.Category;
                return true;
            case "scope":
                view = EstimateViewKind.Scope;
                return true;
            case "work-item":
                view = EstimateViewKind.WorkItem;
                return true;
            case "review":
                view = EstimateViewKind.Review;
                return true;
            default:
                view = default;
                return false;
        }
    }

    private static bool TryParseCalibrationPartition(
        string value,
        out CalibrationPartition partition)
    {
        switch (value.ToLowerInvariant())
        {
            case "development":
                partition = CalibrationPartition.Development;
                return true;
            case "validation":
                partition = CalibrationPartition.Validation;
                return true;
            case "test":
                partition = CalibrationPartition.Test;
                return true;
            default:
                partition = default;
                return false;
        }
    }

    private const string HelpText = """
        Fairbill - estimate equivalent non-AI human effort represented by software.

        Usage:
          fairbill scan <repository> [options]
          fairbill estimate <repository-or-evidence.json> [options]
          fairbill report <estimate.json> [options]
          fairbill explain <repository-or-evidence.json> --item <id> [options]
          fairbill calibration scaffold <estimate.json> [--blind] [--compact] [--output <path>]
          fairbill calibration compile <review-plan.json> <estimate.json>... [--compact] [--output <path>]
          fairbill calibration validate <corpus.json> [--compact] [--output <path>]
          fairbill calibration evaluate <corpus.json> <estimate.json>... --partition <name> [--compact] [--output <path>]
          fairbill schema list
          fairbill schema show <name>
          fairbill model info
          fairbill model show
          fairbill rate info
          fairbill rate show
          fairbill version

        Static analysis is deterministic, local, and read-only by default. The .NET
        analyzer parses projects and C# syntax without evaluating MSBuild. The
        JavaScript/TypeScript analyzer parses manifests, configuration, JS/JSX ASTs,
        and TS/TSX token streams without running package managers or executable
        configuration. Fairbill does not execute target code, access Git history,
        install dependencies, or emit source excerpts. The current seed estimator is
        explicitly uncalibrated and must not be treated as a production estimate.
        """;

    private const string CalibrationHelpText = """
        Usage:
          fairbill calibration scaffold <estimate.json> [--blind] [--compact] [--output <path>]
          fairbill calibration compile <review-plan.json> <estimate.json>... [--compact] [--output <path>]
          fairbill calibration validate <corpus.json> [--compact] [--output <path>]
          fairbill calibration evaluate <corpus.json> <estimate.json>... --partition <name> [--compact] [--output <path>]

        Calibration is offline and effort-only. Reviewed labels are weak supervision,
        not historical labor or literal ground truth.
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

    private const string CalibrationScaffoldHelpText = """
        Usage:
          fairbill calibration scaffold <estimate.json> [options]

        Produces a schema-versioned, explicitly unreviewed authoring packet. Candidate
        values are reference material and cannot be consumed as calibration labels.

        Options:
          --blind          Hide candidate hours, category totals, and confidence while reviewing
          --compact        Emit compact JSON
          --output <path>  Write the packet to an explicit path instead of stdout
          -h, --help       Show this help
        """;

    private const string CalibrationValidateHelpText = """
        Usage:
          fairbill calibration validate <corpus.json> [--compact] [--output <path>]
        """;

    private const string CalibrationEvaluateHelpText = """
        Usage:
          fairbill calibration evaluate <corpus.json> <estimate.json>... [options]

        Options:
          --partition <development|validation|test>  Required repository-held-out partition
          --compact                                  Emit compact JSON
          --output <path>                            Write report to a path instead of stdout
          -h, --help                                 Show this help
        """;

    private const string ScanHelpText = """
        Usage:
          fairbill scan <repository> [options]

        Options:
          --output <path>       Write evidence JSON to an explicit path
          --cache <path>        Use an explicit cache outside the repository
          --no-gitignore        Do not apply nested .gitignore files
          --no-fairbillignore   Do not apply nested .fairbillignore files
          -h, --help            Show this help
        """;

    private const string EstimateHelpText = """
        Usage:
          fairbill estimate <repository-or-evidence.json> [options]

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

    private const string ReportHelpText = """
        Usage:
          fairbill report <estimate.json> [options]

        Options:
          --view <name>             review (default), full, repository, category, scope,
                                    or work-item
          --format <json|markdown>  Output format (default: json)
          --compact                 Emit compact JSON
          -h, --help                Show this help
        """;

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

    private const string SchemaHelpText = """
        Usage:
          fairbill schema list
          fairbill schema show <name>
        """;

    private const string ModelHelpText = """
        Usage:
          fairbill model info
          fairbill model show
        """;

    private const string RateHelpText = """
        Usage:
          fairbill rate info
          fairbill rate show
        """;
}
