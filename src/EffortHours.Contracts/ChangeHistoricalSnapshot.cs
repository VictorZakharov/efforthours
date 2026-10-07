using System.Text.Json;
using System.Text.Json.Nodes;

namespace EffortHours.Contracts;

public static class ChangeHistoricalSnapshot
{
    public static string Digest(JsonElement snapshot) =>
        ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(snapshot));

    public static string NoteDigest(JsonElement original, string description)
    {
        JsonObject proposed = JsonNode.Parse(original.GetRawText())!.AsObject();
        proposed["description"] = description;
        return Digest(JsonSerializer.SerializeToElement(proposed));
    }
}
