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
        await standardError.WriteLineAsync($"eh: {message}").ConfigureAwait(false);
        await standardError.WriteLineAsync("Run 'eh --help' for usage.").ConfigureAwait(false);
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

    private const string HelpText = """
        EffortHours - estimate equivalent non-AI human effort represented by software.

        Usage:
          eh scan <repository> [options]
          eh estimate <repository-or-evidence.json> [options]
          eh change <repository> <--commit|--range|--base/--head|--pr> [options]
          eh change <--base-path/--head-path|--base-evidence/--head-evidence> [options]
          eh change portfolio <repository> <--pr|--author> [options]
          eh change portfolio --manifest <portfolio.json> [options]
          eh change explain <change-estimate.json> --item <id> [options]
          eh report <estimate.json> [options]
          eh explain <repository-or-evidence.json> --item <id> [options]
          eh review packet <repository-or-evidence.json> [options]
          eh review query <repository-or-evidence.json> --input-digest <digest> [selector] [options]
          eh review validate <packet.json> <adjustment.json> [options]
          eh review measure <packet.json> <adjustment.json> --subject <id> --session <id> --context <mode> [options]
          eh review benchmark <measurement.json>... [options]
          eh calibration scaffold <estimate.json> [--blind] [--compact] [--output <path>]
          eh calibration compile <review-plan.json> <estimate.json>... [--compact] [--output <path>]
          eh calibration review-scaffold <corpus.json> [--blind] [--compact] [--output <path>]
          eh calibration review-compile <plan.json> <corpus.json> [--compact] [--output <path>]
          eh calibration mutations <suite.json> <estimate.json>... [--compact] [--output <path>]
          eh calibration validate <corpus.json> [--compact] [--output <path>]
          eh calibration evaluate <corpus.json> <estimate.json>... --partition <name> [--compact] [--output <path>]
          eh schema list
          eh schema show <name>
          eh model info
          eh model show
          eh rate info
          eh rate show
          eh version

        Static analysis is deterministic, local, and read-only by default. The .NET
        analyzer parses projects and C# syntax without evaluating MSBuild. The
        JavaScript/TypeScript analyzer parses manifests, configuration, JS/JSX ASTs,
        and TS/TSX token streams without running package managers or executable
        configuration. EffortHours does not execute target code, access Git history,
        install dependencies, or emit source excerpts unless one admitted file is
        explicitly requested through a digest-bound review query. The current seed
        estimator is explicitly uncalibrated and must not be treated as a production
        estimate.
        """;

    private const string SchemaHelpText = """
        Usage:
          eh schema list
          eh schema show <name>
        """;

    private const string ModelHelpText = """
        Usage:
          eh model info
          eh model show
        """;

    private const string RateHelpText = """
        Usage:
          eh rate info
          eh rate show
        """;
}
