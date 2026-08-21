using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public sealed class ChangePortfolioComparisonJsonRenderer(bool compact = false)
{
    public string Render(ChangePortfolioComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "The change portfolio comparison report is invalid: " +
                string.Join(" ", errors),
                nameof(report));
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioComparisonReport,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "The change portfolio comparison report does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}
