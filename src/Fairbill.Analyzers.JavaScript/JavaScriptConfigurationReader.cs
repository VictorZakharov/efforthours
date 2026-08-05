using System.Text.Json;
using Fairbill.Contracts.V1;

namespace Fairbill.Analyzers.JavaScript;

internal sealed class JavaScriptConfigurationReader(RepositoryTextReader textReader)
{
    private const long MaximumConfigurationBytes = 4 * 1024 * 1024;

    private readonly RepositoryTextReader _textReader = textReader;

    public async Task<JavaScriptConfigurationReadResult> ReadAsync(
        RepositoryEvidence evidence,
        CancellationToken cancellationToken)
    {
        List<JavaScriptConfigurationModel> configurations = [];
        List<Diagnostic> diagnostics = [];
        foreach (EvidenceFact fileFact in evidence.Facts
            .Where(IsTypeScriptConfiguration)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryTextReadResult read = await _textReader.ReadAsync(
                fileFact,
                MaximumConfigurationBytes,
                "FB4006",
                cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    read.Text!,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip,
                        MaxDepth = 64,
                    });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(JavaScriptEvidence.Diagnostic(
                        "FB4007",
                        DiagnosticSeverity.Warning,
                        $"Configuration '{fileFact.Scope}' is not a JSON object and was skipped.",
                        fileFact.Scope));
                    continue;
                }

                configurations.Add(ReadConfiguration(fileFact.Scope, document.RootElement));
            }
            catch (JsonException exception)
            {
                diagnostics.Add(JavaScriptEvidence.Diagnostic(
                    "FB4007",
                    DiagnosticSeverity.Warning,
                    $"Configuration '{fileFact.Scope}' is invalid JSON-with-comments and was skipped.",
                    fileFact.Scope,
                    ToLine(exception.LineNumber)));
            }
        }

        return new JavaScriptConfigurationReadResult(configurations, diagnostics);
    }

    private static JavaScriptConfigurationModel ReadConfiguration(string path, JsonElement root)
    {
        List<string> tags = [];
        if (root.TryGetProperty("compilerOptions", out JsonElement options) &&
            options.ValueKind == JsonValueKind.Object)
        {
            AddBooleanTag(options, "strict", tags);
            AddBooleanTag(options, "allowJs", tags);
            AddBooleanTag(options, "checkJs", tags);
            AddBooleanTag(options, "noEmit", tags);
            AddBooleanTag(options, "declaration", tags);
            AddBooleanTag(options, "composite", tags);
            AddStringTag(options, "jsx", tags);
            AddStringTag(options, "module", tags);
            AddStringTag(options, "target", tags);
            AddStringTag(options, "moduleResolution", tags);
        }

        return new JavaScriptConfigurationModel
        {
            Path = path,
            Scope = GetDirectory(path),
            Kind = Path.GetFileName(path).StartsWith("jsconfig", StringComparison.OrdinalIgnoreCase)
                ? "javascript"
                : "typescript",
            Extends = ReadString(root, "extends"),
            References = ReadReferences(root),
            Tags = [.. tags.Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal)],
        };
    }

    private static IReadOnlyList<string> ReadReferences(JsonElement root)
    {
        if (!root.TryGetProperty("references", out JsonElement references) ||
            references.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. references.EnumerateArray()
            .Where(reference => reference.ValueKind == JsonValueKind.Object)
            .Select(reference => ReadString(reference, "path"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static void AddBooleanTag(JsonElement options, string propertyName, List<string> tags)
    {
        if (!options.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return;
        }

        tags.Add($"compiler-option:{ToKebabCase(propertyName)}={value.GetBoolean().ToString().ToLowerInvariant()}");
    }

    private static void AddStringTag(JsonElement options, string propertyName, List<string> tags)
    {
        string? value = ReadString(options, propertyName);
        if (value is null || value.Length > 80 || value.Any(char.IsControl))
        {
            return;
        }

        tags.Add($"compiler-option:{ToKebabCase(propertyName)}={value.ToLowerInvariant()}");
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string ToKebabCase(string value)
    {
        Span<char> buffer = stackalloc char[value.Length * 2];
        int written = 0;
        foreach (char character in value)
        {
            if (char.IsUpper(character))
            {
                buffer[written++] = '-';
                buffer[written++] = char.ToLowerInvariant(character);
            }
            else
            {
                buffer[written++] = character;
            }
        }

        return new string(buffer[..written]);
    }

    private static bool IsTypeScriptConfiguration(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File ||
            fact.Tags.Contains("classification:generated", StringComparer.Ordinal) ||
            fact.Tags.Contains("classification:vendored", StringComparer.Ordinal) ||
            fact.Tags.Contains("content:binary", StringComparer.Ordinal))
        {
            return false;
        }

        string fileName = Path.GetFileName(fact.Scope);
        return (fileName.StartsWith("tsconfig", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("jsconfig", StringComparison.OrdinalIgnoreCase)) &&
            fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDirectory(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "." : path[..separator];
    }

    private static int? ToLine(long? zeroBasedLine) => zeroBasedLine is null
        ? null
        : checked((int)zeroBasedLine.Value + 1);
}
