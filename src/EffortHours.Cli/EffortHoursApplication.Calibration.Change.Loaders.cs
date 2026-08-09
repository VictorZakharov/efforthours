using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<ChangeEstimateReport?> LoadChangeEstimateAsync(
        string inputPath,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            await standardError.WriteLineAsync(
                $"Change estimate path was not found: {inputPath}").ConfigureAwait(false);
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync(
                $"Could not read change estimate: {exception.Message}").ConfigureAwait(false);
            return null;
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

            return null;
        }

        try
        {
            return ContractJson.Deserialize<ChangeEstimateReport>(json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize change estimate: {exception.Message}").ConfigureAwait(false);
            return null;
        }
    }
}
