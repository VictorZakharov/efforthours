using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ScaffoldChangeCalibrationAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ChangeCalibrationScaffoldHelpText)
                .ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string estimatePath = arguments[0];
        string? repositoryFamilyId = null;
        string? caseId = null;
        List<string> coverageTags = [];
        bool blind = false;
        bool compact = false;
        string? outputPath = null;

        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (option == "--blind")
            {
                blind = true;
                continue;
            }

            if (option == "--compact")
            {
                compact = true;
                continue;
            }

            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ChangeCalibrationScaffoldHelpText)
                    .ConfigureAwait(false);
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
                case "--repository-family":
                    repositoryFamilyId = value;
                    break;
                case "--case":
                    caseId = value;
                    break;
                case "--tag":
                    coverageTags.Add(value);
                    break;
                case "--output":
                    outputPath = value;
                    break;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown calibration change-scaffold option '{option}'.")
                        .ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(repositoryFamilyId))
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration change-scaffold requires --repository-family <id>.")
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(caseId))
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration change-scaffold requires --case <id>.").ConfigureAwait(false);
        }

        if (coverageTags.Count == 0)
        {
            return await UsageErrorAsync(
                standardError,
                "Calibration change-scaffold requires at least one --tag <tag>.")
                .ConfigureAwait(false);
        }

        ChangeEstimateReport? estimate = await LoadChangeEstimateAsync(
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
            packet = ChangeCalibrationAuthoring.Scaffold(
                estimate,
                repositoryFamilyId,
                caseId,
                coverageTags,
                blind);
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
                "Change calibration authoring produced an invalid packet.").ConfigureAwait(false);
            foreach (string error in schema.Errors)
            {
                await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
            }

            return CliExitCodes.InternalError;
        }

        return await WriteCliOutputAsync(
            json,
            outputPath,
            "Change calibration authoring packet",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string ChangeCalibrationScaffoldHelpText = """
        Usage:
          eh calibration change-scaffold <change-estimate.json> [options]

        Produces an explicitly unreviewed Change EHE packet for an immutable final
        delta. Repository family owns partition isolation; tags select review strata
        but never affect candidate effort.

        Options:
          --repository-family <id>  Stable repository-family identity (required)
          --case <id>               Stable final-change case identity (required)
          --tag <tag>               Lowercase kebab-case coverage tag (repeatable; required)
          --blind                   Hide candidate hours, totals, and confidence
          --compact                 Emit compact JSON
          --output <path>           Write to an explicit path instead of stdout
          -h, --help                Show this help
        """;
}
