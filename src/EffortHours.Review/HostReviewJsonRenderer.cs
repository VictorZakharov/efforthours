using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

public sealed class HostReviewJsonRenderer(bool compact = false)
{
    public string Render(HostReviewPacket packet) => RenderDocument(
        packet,
        SchemaNames.HostReviewPacket,
        ContractValidation.Validate,
        "packet");

    public string Render(HostReviewQueryResult result) => RenderDocument(
        result,
        SchemaNames.HostReviewQueryResult,
        ContractValidation.Validate,
        "query result");

    public string Render(HostReviewAdjustmentLedger ledger) => RenderDocument(
        ledger,
        SchemaNames.HostReviewAdjustment,
        ContractValidation.Validate,
        "adjustment ledger");

    public string Render(HostReviewValidationReport report) => RenderDocument(
        report,
        SchemaNames.HostReviewValidation,
        ContractValidation.Validate,
        "validation report");

    private string RenderDocument<T>(
        T document,
        string schemaName,
        Func<T, IReadOnlyList<string>> validate,
        string description)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<string> semanticErrors = validate(document);
        if (semanticErrors.Count > 0)
        {
            throw new ArgumentException(
                $"The host-review {description} is invalid: " +
                string.Join(" ", semanticErrors),
                nameof(document));
        }

        string json = compact
            ? ContractJson.SerializeCompact(document)
            : ContractJson.Serialize(document);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(schemaName, json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                $"The host-review {description} does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        return json;
    }
}
