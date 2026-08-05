using System.Globalization;
using System.Reflection;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;
using Fairbill.Estimation;
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
                "schema" => await SchemaAsync(
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
        decimal? hourlyRate = null;
        string currency = "USD";

        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(EstimateHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
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
                    if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Currency must be a three-letter uppercase currency code.").ConfigureAwait(false);
                    }

                    break;

                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown estimate option '{option}'.").ConfigureAwait(false);
            }
        }

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
                return CliExitCodes.InvalidInput;
            }

            SchemaValidationResult schemaResult = ContractSchemaValidator.Validate(
                SchemaNames.RepositoryEvidence,
                json);
            if (!schemaResult.IsValid)
            {
                await standardError.WriteLineAsync("Evidence does not satisfy the repository evidence schema:")
                    .ConfigureAwait(false);
                foreach (string error in schemaResult.Errors)
                {
                    await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
                }

                return CliExitCodes.InvalidInput;
            }

            try
            {
                evidence = ContractJson.Deserialize<RepositoryEvidence>(json);
            }
            catch (System.Text.Json.JsonException exception)
            {
                await standardError.WriteLineAsync($"Could not deserialize evidence: {exception.Message}")
                    .ConfigureAwait(false);
                return CliExitCodes.InvalidInput;
            }
        }
        else
        {
            await standardError.WriteLineAsync($"Repository or evidence path was not found: {inputPath}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(evidence);
        if (semanticErrors.Count > 0)
        {
            await standardError.WriteLineAsync("Evidence is semantically invalid:").ConfigureAwait(false);
            foreach (string error in semanticErrors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InvalidInput;
        }

        RateCard? rateCard = hourlyRate is null
            ? null
            : new RateCard
            {
                Id = "user-supplied-cli-rate",
                Name = "User-supplied CLI rate",
                Currency = currency,
                HourlyRate = hourlyRate.Value,
                Methodology = "Explicit caller override; this rate is not a Fairbill market-rate claim.",
            };

        EstimateReport report = _estimator.Estimate(evidence, profile, rateCard);
        IReportRenderer renderer = format == "markdown"
            ? new MarkdownReportRenderer()
            : new JsonReportRenderer();
        string output = renderer.Render(report);
        await standardOutput.WriteLineAsync(output.TrimEnd()).ConfigureAwait(false);
        return CliExitCodes.Success;
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

    private const string HelpText = """
        Fairbill - estimate equivalent non-AI human effort represented by software.

        Usage:
          fairbill scan <repository> [options]
          fairbill estimate <repository-or-evidence.json> [options]
          fairbill schema list
          fairbill schema show <name>
          fairbill version

        Static analysis is deterministic, local, and read-only by default. The .NET
        analyzer parses projects and C# syntax without evaluating MSBuild. The
        JavaScript/TypeScript analyzer parses manifests, configuration, JS/JSX ASTs,
        and TS/TSX token streams without running package managers or executable
        configuration. Fairbill does not execute target code, access Git history,
        install dependencies, or emit source excerpts. The current seed estimator is
        explicitly uncalibrated and must not be treated as a production estimate.
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
          --hourly-rate <number>                  Optional caller-supplied hourly rate
          --currency <code>                       Three-letter currency code (default: USD)
          -h, --help                              Show this help
        """;

    private const string SchemaHelpText = """
        Usage:
          fairbill schema list
          fairbill schema show <name>
        """;
}
