using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public sealed class EstimateExplanationJsonRenderer(bool compact = false)
{
    public string Render(EstimateExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(explanation);
        if (semanticErrors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate explanation is invalid: " + string.Join(" ", semanticErrors),
                nameof(explanation));
        }

        string json = compact
            ? ContractJson.SerializeCompact(explanation)
            : ContractJson.Serialize(explanation);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.EstimateExplanation,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The estimate explanation does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}
