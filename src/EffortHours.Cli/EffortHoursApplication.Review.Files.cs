using EffortHours.Contracts;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<ReviewDocument<T>?> ReadReviewDocumentAsync<T>(
        string path,
        string description,
        string schemaName,
        Func<T, IReadOnlyList<string>> validate,
        TextWriter standardError,
        CancellationToken cancellationToken)
        where T : class
    {
        string? json = await ReadReviewFileAsync(
            path,
            description,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(schemaName, json);
        if (!schema.IsValid)
        {
            await WriteSchemaErrorsAsync(
                $"Host-review {description}",
                schema.Errors,
                standardError).ConfigureAwait(false);
            return null;
        }

        try
        {
            T value = ContractJson.Deserialize<T>(json);
            IReadOnlyList<string> errors = validate(value);
            if (errors.Count > 0)
            {
                await WriteSchemaErrorsAsync(
                    $"Host-review {description} semantic validation",
                    errors,
                    standardError).ConfigureAwait(false);
                return null;
            }

            return new ReviewDocument<T>(value, json);
        }
        catch (System.Text.Json.JsonException exception)
        {
            await standardError.WriteLineAsync(
                $"Could not deserialize host-review {description}: {exception.Message}")
                .ConfigureAwait(false);
            return null;
        }
    }

    private sealed record ReviewDocument<T>(T Value, string Json)
        where T : class;
}
