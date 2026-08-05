using System.Text.Json;
using Fairbill.Contracts.V1;
using Json.Schema;

namespace Fairbill.Contracts;

public sealed record SchemaValidationResult(bool IsValid, IReadOnlyList<string> Errors);

public static class ContractSchemaValidator
{
    private static readonly Lazy<Dictionary<string, JsonSchema>> Schemas =
        new(CreateSchemas, LazyThreadSafetyMode.ExecutionAndPublication);

    public static SchemaValidationResult Validate(string schemaName, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        ArgumentNullException.ThrowIfNull(json);

        if (!Schemas.Value.TryGetValue(schemaName, out JsonSchema? schema))
        {
            throw new ArgumentOutOfRangeException(nameof(schemaName), schemaName, "Unknown Fairbill schema name.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            EvaluationOptions options = CreateEvaluationOptions();
            EvaluationResults results = schema.Evaluate(
                document.RootElement,
                options);

            if (results.IsValid)
            {
                return new SchemaValidationResult(true, []);
            }

            string[] errors = [.. FlattenErrors(results)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];

            return new SchemaValidationResult(
                false,
                errors.Length == 0 ? ["The document does not satisfy the selected schema."] : errors);
        }
        catch (JsonException exception)
        {
            return new SchemaValidationResult(false, [$"Invalid JSON: {exception.Message}"]);
        }
    }

    private static Dictionary<string, JsonSchema> CreateSchemas()
    {
        Dictionary<string, JsonSchema> schemas = new(StringComparer.Ordinal);

        foreach (string name in ContractSchemaCatalog.Names)
        {
            JsonSchema schema = JsonSchema.FromText(ContractSchemaCatalog.Read(name));
            schemas.Add(name, schema);
        }

        return schemas;
    }

    private static EvaluationOptions CreateEvaluationOptions()
    {
        EvaluationOptions options = new()
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true,
        };

        foreach (JsonSchema schema in Schemas.Value.Values)
        {
            options.SchemaRegistry.Register(schema);
        }

        return options;
    }

    private static IEnumerable<string> FlattenErrors(EvaluationResults result)
    {
        if (result.Errors is not null)
        {
            foreach (string error in result.Errors.Values)
            {
                yield return $"{result.InstanceLocation}: {error}";
            }
        }

        if (result.Details is null)
        {
            yield break;
        }

        foreach (EvaluationResults detail in result.Details)
        {
            foreach (string error in FlattenErrors(detail))
            {
                yield return error;
            }
        }
    }
}
