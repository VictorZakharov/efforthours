using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ReviewValidateAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ReviewValidateHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments.Length < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Review validation requires a packet path and an adjustment path.")
                .ConfigureAwait(false);
        }

        string packetPath = arguments[0];
        string adjustmentPath = arguments[1];
        string? outputPath = null;
        bool compact = false;
        for (int index = 2; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ReviewValidateHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option == "--compact")
            {
                compact = true;
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

            return await UsageErrorAsync(
                standardError,
                $"Unknown review validate option '{option}'.").ConfigureAwait(false);
        }

        string? packetJson = await ReadReviewFileAsync(
            packetPath,
            "packet",
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (packetJson is null)
        {
            return CliExitCodes.InvalidInput;
        }

        SchemaValidationResult packetSchema = ContractSchemaValidator.Validate(
            SchemaNames.HostReviewPacket,
            packetJson);
        if (!packetSchema.IsValid)
        {
            await WriteSchemaErrorsAsync(
                "Packet",
                packetSchema.Errors,
                standardError).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        HostReviewPacket packet;
        try
        {
            packet = ContractJson.Deserialize<HostReviewPacket>(packetJson);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize host-review packet: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        IReadOnlyList<string> packetErrors = ContractValidation.Validate(packet);
        if (packetErrors.Count > 0)
        {
            await WriteSchemaErrorsAsync(
                "Packet semantic validation",
                packetErrors,
                standardError).ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string? adjustmentJson = await ReadReviewFileAsync(
            adjustmentPath,
            "adjustment ledger",
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (adjustmentJson is null)
        {
            return CliExitCodes.InvalidInput;
        }

        HostReviewValidationReport validation;
        SchemaValidationResult adjustmentSchema = ContractSchemaValidator.Validate(
            SchemaNames.HostReviewAdjustment,
            adjustmentJson);
        if (!adjustmentSchema.IsValid)
        {
            validation = InvalidAdjustmentReport(packet, adjustmentSchema.Errors);
        }
        else
        {
            try
            {
                HostReviewAdjustmentLedger ledger =
                    ContractJson.Deserialize<HostReviewAdjustmentLedger>(adjustmentJson);
                validation = HostReviewAdjustmentValidator.Validate(packet, ledger);
            }
            catch (System.Text.Json.JsonException exception)
            {
                validation = InvalidAdjustmentReport(
                    packet,
                    [$"Could not deserialize adjustment ledger: {exception.Message}"]);
            }
        }

        string output = new HostReviewJsonRenderer(compact).Render(validation);
        int writeResult = await WriteCliOutputAsync(
            output,
            outputPath,
            "host-review validation report",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (writeResult != CliExitCodes.Success)
        {
            return writeResult;
        }

        if (validation.IsValid)
        {
            return CliExitCodes.Success;
        }

        await standardError.WriteLineAsync(
            "eh: host-review adjustment validation failed; no adjustment was applied.")
            .ConfigureAwait(false);
        return CliExitCodes.InvalidInput;
    }

    private static HostReviewValidationReport InvalidAdjustmentReport(
        HostReviewPacket packet,
        IEnumerable<string> errors) => new()
        {
            ProtocolVersion = packet.ProtocolVersion,
            InputDigest = packet.InputDigest,
            IsValid = false,
            AdjustmentCount = 0,
            AdjustmentsApplied = false,
            Errors =
            [
                .. errors
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ],
            Warnings = ["No adjustment was applied to the local estimate."],
        };

    private static async Task<string?> ReadReviewFileAsync(
        string path,
        string description,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            await standardError.WriteLineAsync(
                $"Host-review {description} path was not found: {path}").ConfigureAwait(false);
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not read host-review {description}: {exception.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private static async Task WriteSchemaErrorsAsync(
        string description,
        IEnumerable<string> errors,
        TextWriter standardError)
    {
        await standardError.WriteLineAsync($"{description} is invalid:").ConfigureAwait(false);
        foreach (string error in errors)
        {
            await standardError.WriteLineAsync($"- {error}").ConfigureAwait(false);
        }
    }

    private const string ReviewValidateHelpText = """
        Usage:
          eh review validate <packet.json> <adjustment.json> [options]

        Options:
          --compact        Emit compact JSON
          --output <path>  Write to an explicit path instead of stdout
          -h, --help       Show this help

        Validation checks identity, shape, and evidence lineage. It never applies
        the proposed adjustments or claims that they are correct or calibrated.
        """;
}
