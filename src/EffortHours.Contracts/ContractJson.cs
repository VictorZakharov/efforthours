using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EffortHours.Contracts;

public static class ContractJson
{
    public const string CanonicalDocumentId = "canonical-json-document/1.0.0";

    public static JsonSerializerOptions Options { get; } = CreateOptions(writeIndented: true);

    public static JsonSerializerOptions CompactOptions { get; } = CreateOptions(writeIndented: false);

    public static string Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return NormalizeLineEndings(JsonSerializer.Serialize(value, Options));
    }

    public static string SerializeCompact<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return NormalizeLineEndings(JsonSerializer.Serialize(value, CompactOptions));
    }

    public static string SerializeDocument<T>(T value, bool compact = false) =>
        ToCanonicalDocument(compact ? SerializeCompact(value) : Serialize(value));

    public static string ToCanonicalDocument(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return NormalizeLineEndings(json).TrimEnd() + '\n';
    }

    public static T Deserialize<T>(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new JsonException($"The JSON value could not be deserialized as {typeof(T).Name}.");
    }

    private static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            DictionaryKeyPolicy = JsonNamingPolicy.KebabCaseLower,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = writeIndented,
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
