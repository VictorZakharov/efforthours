using System.Text.Json;

namespace EffortHours.Analyzers.Unreal;

/// <summary>
/// Bounded structural reading of a <c>.uproject</c> or <c>.uplugin</c> JSON descriptor. Only
/// counts are retained; names, descriptions, engine associations, and paths are not emitted.
/// </summary>
internal sealed record UnrealProjectManifest(int EnabledPlugins, int Modules)
{
    public const long MaximumBytes = 1024 * 1024;

    public static UnrealProjectManifest? TryParse(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            using JsonDocument document = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 32,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            int plugins = Count(document.RootElement, "Plugins", plugin =>
                plugin.ValueKind == JsonValueKind.Object &&
                plugin.TryGetProperty("Enabled", out JsonElement enabled) &&
                enabled.ValueKind == JsonValueKind.True);
            int modules = Count(document.RootElement, "Modules", module => module.ValueKind == JsonValueKind.Object);
            return new UnrealProjectManifest(plugins, modules);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int Count(JsonElement root, string property, Func<JsonElement, bool> predicate) =>
        root.TryGetProperty(property, out JsonElement array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Count(predicate)
            : 0;
}
