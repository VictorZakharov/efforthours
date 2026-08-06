using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Reporting;

public sealed class EstimateViewJsonRenderer(bool compact = false)
{
    public string Render(EstimateViewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(report);
        if (semanticErrors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate view is invalid: " + string.Join(" ", semanticErrors),
                nameof(report));
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.EstimateView,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The estimate view does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}
