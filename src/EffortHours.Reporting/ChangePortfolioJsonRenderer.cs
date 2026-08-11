using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public sealed class ChangePortfolioJsonRenderer(bool compact = false)
{
    public string Render(ChangePortfolioReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change portfolio report is invalid: " + string.Join(" ", errors),
                nameof(report));
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The change portfolio report does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}
