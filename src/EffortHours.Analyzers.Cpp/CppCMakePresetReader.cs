using System.Text.Json;

namespace EffortHours.Analyzers.Cpp;

internal static class CppCMakePresetReader
{
    private static readonly string[] Collections =
    [
        "configurePresets", "buildPresets", "testPresets", "packagePresets", "workflowPresets",
    ];

    public static void Parse(string text, CppBuildAccumulator project)
    {
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException();

        project.BuildSystems.Add("cmake-presets");
        foreach (string propertyName in Collections)
        {
            if (!document.RootElement.TryGetProperty(propertyName, out JsonElement collection)) continue;
            if (collection.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
            foreach (JsonElement preset in collection.EnumerateArray().Take(10_000))
            {
                if (preset.ValueKind != JsonValueKind.Object)
                {
                    project.Unresolved++;
                    continue;
                }
                project.ConfigurationVariants++;
                if (HasDynamicBoundary(preset)) project.Unresolved++;
            }
        }

        if (document.RootElement.TryGetProperty("include", out JsonElement includes))
        {
            project.LocalReferences += includes.ValueKind == JsonValueKind.Array
                ? includes.GetArrayLength()
                : 1;
            project.Unresolved++;
        }
    }

    private static bool HasDynamicBoundary(JsonElement preset) =>
        preset.TryGetProperty("condition", out _) ||
        preset.TryGetProperty("inherits", out _) ||
        preset.TryGetProperty("environment", out _) ||
        preset.TryGetProperty("cacheVariables", out _) ||
        preset.TryGetProperty("toolchainFile", out _);
}
