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

        RepositoryInputOptionsBuilder inputOptions = new(arguments);
        string? outputPath = null;
        RepositoryScanOptions options = new();
        for (int index = inputOptions.FirstOptionIndex; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (inputOptions.TryConsume(arguments, ref index, out string? inputError))
            {
                if (inputError is not null)
                {
                    return await UsageErrorAsync(standardError, inputError).ConfigureAwait(false);
                }

                continue;
            }

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

                case "--no-efforthoursignore":
                    options = options with { RespectEffortHoursIgnore = false };
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

        RepositoryInputSelection? selection = await BuildRepositoryInputAsync(
            inputOptions,
            standardError).ConfigureAwait(false);
        if (selection is null)
        {
            return CliExitCodes.UsageError;
        }

        await using RepositoryInputContext? input = await LoadRepositoryInputAsync(
            selection,
            allowEvidenceFile: false,
            options,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (input is null)
        {
            return CliExitCodes.InvalidInput;
        }

        RepositoryEvidence evidence = input.Evidence;
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

        if (outputPath is not null && Directory.Exists(outputPath))
        {
            await standardError.WriteLineAsync($"Output path is a directory: {outputPath}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "evidence",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string ScanHelpText = """
        Usage:
          eh scan <repository> [options]
          eh scan --repo <owner/name> [--revision <revision>] [--fetch-missing] [options]

        Options:
          --repo <owner/name>   Analyze an immutable GitHub repository snapshot without checkout
          --revision <value>    Git revision for --repo (default: HEAD)
          --fetch-missing       Resolve/fetch missing immutable objects into the private cache
          --output <path>       Write evidence JSON to an explicit path
          --cache <path>        Use an explicit cache outside the repository
          --no-gitignore        Do not apply nested .gitignore files
          --no-efforthoursignore   Do not apply nested .efforthoursignore files
          -h, --help            Show this help
        """;

}
