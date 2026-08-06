using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Reporting;

public sealed class ChangeEstimateJsonRenderer(bool compact = false)
{
    public string Render(ChangeEstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change estimate report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeEstimateReport,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The change estimate report does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}
