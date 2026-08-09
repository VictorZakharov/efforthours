using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

public sealed class JsonReportRenderer(bool compact = false) : IReportRenderer
{
    public string Render(EstimateReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(report);
        if (semanticErrors.Count > 0)
        {
            throw new ArgumentException(
                "The estimate report is invalid: " + string.Join(" ", semanticErrors),
                nameof(report));
        }

        string json = compact
            ? ContractJson.SerializeCompact(report)
            : ContractJson.Serialize(report);
        SchemaValidationResult schemaResult = ContractSchemaValidator.Validate(
            SchemaNames.EstimateReport,
            json);

        if (!schemaResult.IsValid)
        {
            throw new InvalidOperationException(
                "The estimate report does not satisfy its JSON Schema: " +
                string.Join(" ", schemaResult.Errors));
        }

        return json;
    }
}
