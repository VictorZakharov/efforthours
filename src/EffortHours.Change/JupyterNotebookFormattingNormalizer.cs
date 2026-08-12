using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal static class JupyterNotebookFormattingNormalizer
{
    private const int MaximumCells = 10_000;
    private const int MaximumCellCharacters = 1 * 1024 * 1024;

    public static bool TryCreateSignature(string json, out string signature)
    {
        ArgumentNullException.ThrowIfNull(json);
        signature = string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("cells", out JsonElement cells) ||
                cells.ValueKind != JsonValueKind.Array ||
                cells.GetArrayLength() > MaximumCells) return false;

            string language = DeclaredLanguage(root);
            StringBuilder maintained = new();
            HashSet<string> codeCells = new(StringComparer.Ordinal);
            HashSet<string> markdownCells = new(StringComparer.Ordinal);
            maintained.Append("language:").Append(language).Append(';');
            foreach (JsonElement cell in cells.EnumerateArray())
            {
                if (cell.ValueKind != JsonValueKind.Object ||
                    !cell.TryGetProperty("cell_type", out JsonElement type) ||
                    type.ValueKind != JsonValueKind.String ||
                    !TryReadSource(cell, out string source)) return false;
                string cellType = type.GetString() ?? string.Empty;
                if (cellType == "code")
                {
                    PythonProjection projection = PreparePython(source, language == "python");
                    if (!projection.IsPython) continue;
                    if (!PythonFormattingNormalizer.TryCreateSignature(projection.Source, out string code)) return false;
                    if (!codeCells.Add(code)) continue;
                    maintained.Append("cell:code;");
                    AppendField(maintained, "python", code);
                    AppendTags(cell, maintained);
                }
                else if (cellType == "markdown")
                {
                    string markdown = NormalizeText(source);
                    if (!markdownCells.Add(markdown)) continue;
                    maintained.Append("cell:markdown;");
                    AppendField(maintained, "markdown", markdown);
                    AppendTags(cell, maintained);
                }
            }

            signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(maintained.ToString())))
                .ToLowerInvariant();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryClassifyDifference(string left, string right, out string role)
    {
        role = string.Empty;
        if (!TryCreateSections(left, out NotebookSections leftSections) ||
            !TryCreateSections(right, out NotebookSections rightSections)) return false;
        bool code = leftSections.Code != rightSections.Code;
        bool markdown = leftSections.Markdown != rightSections.Markdown;
        bool metadata = leftSections.Metadata != rightSections.Metadata;
        role = !code && markdown && !metadata
            ? "documentation"
            : !code && !markdown && metadata
                ? "configuration"
                : code && !markdown && !metadata
                    ? "code"
                    : "mixed";
        return true;
    }

    private static bool TryCreateSections(string json, out NotebookSections sections)
    {
        sections = new NotebookSections(string.Empty, string.Empty, string.Empty);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("cells", out JsonElement cells) ||
                cells.ValueKind != JsonValueKind.Array || cells.GetArrayLength() > MaximumCells) return false;
            string language = DeclaredLanguage(root);
            StringBuilder code = new();
            StringBuilder markdown = new();
            StringBuilder metadata = new(language);
            HashSet<string> codeCells = new(StringComparer.Ordinal);
            HashSet<string> markdownCells = new(StringComparer.Ordinal);
            foreach (JsonElement cell in cells.EnumerateArray())
            {
                if (cell.ValueKind != JsonValueKind.Object ||
                    !cell.TryGetProperty("cell_type", out JsonElement type) || type.ValueKind != JsonValueKind.String ||
                    !TryReadSource(cell, out string source)) return false;
                string cellType = type.GetString() ?? string.Empty;
                if (cellType == "code")
                {
                    PythonProjection projection = PreparePython(source, language == "python");
                    if (!projection.IsPython) continue;
                    if (!PythonFormattingNormalizer.TryCreateSignature(projection.Source, out string value)) return false;
                    if (!codeCells.Add(value)) continue;
                    AppendField(code, "code", value);
                    AppendTags(cell, metadata);
                }
                else if (cellType == "markdown")
                {
                    string value = NormalizeText(source);
                    if (!markdownCells.Add(value)) continue;
                    AppendField(markdown, "markdown", value);
                    AppendTags(cell, metadata);
                }
            }

            sections = new NotebookSections(Digest(code), Digest(markdown), Digest(metadata));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string DeclaredLanguage(JsonElement root)
    {
        if (!root.TryGetProperty("metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object)
            return "unknown";
        string[] values =
        [
            NestedString(metadata, "language_info", "name"),
            NestedString(metadata, "kernelspec", "language"),
            NestedString(metadata, "kernelspec", "name"),
        ];
        string[] families = [.. values.Where(value => value.Length > 0)
            .Select(LanguageFamily).Where(value => value != "unknown").Distinct(StringComparer.Ordinal)];
        return families.Length switch { 0 => "unknown", 1 => families[0], _ => "mixed" };
    }

    private static string NestedString(JsonElement parent, string objectName, string propertyName) =>
        parent.TryGetProperty(objectName, out JsonElement nested) && nested.ValueKind == JsonValueKind.Object &&
        nested.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string LanguageFamily(string value)
    {
        string lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("python", StringComparison.Ordinal) || lower.StartsWith("ipython", StringComparison.Ordinal)) return "python";
        if (lower is "r" or "ir" || lower.StartsWith("irkernel", StringComparison.Ordinal)) return "r";
        if (lower.StartsWith("julia", StringComparison.Ordinal)) return "julia";
        if (lower is "javascript" or "typescript" or "node" or "nodejs") return "javascript";
        if (lower.Contains("sql", StringComparison.Ordinal)) return "sql";
        return "unknown";
    }

    private static bool TryReadSource(JsonElement cell, out string source)
    {
        source = string.Empty;
        if (!cell.TryGetProperty("source", out JsonElement value)) return true;
        if (value.ValueKind == JsonValueKind.String)
        {
            source = value.GetString() ?? string.Empty;
            return source.Length <= MaximumCellCharacters;
        }

        if (value.ValueKind != JsonValueKind.Array) return false;
        StringBuilder builder = new();
        foreach (JsonElement part in value.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.String) return false;
            builder.Append(part.GetString());
            if (builder.Length > MaximumCellCharacters) return false;
        }

        source = builder.ToString();
        return true;
    }

    private static PythonProjection PreparePython(string source, bool pythonNotebook)
    {
        string[] lines = NormalizeText(source).Split('\n');
        int first = Array.FindIndex(lines, line => line.Trim().Length > 0);
        bool explicitPython = false;
        if (first >= 0 && lines[first].TrimStart().StartsWith("%%", StringComparison.Ordinal))
        {
            string magic = lines[first].TrimStart()[2..].Split([' ', '\t'], 2)[0].ToLowerInvariant();
            explicitPython = magic is "python" or "python3" or "ipython";
            if (!explicitPython) return new PythonProjection(false, string.Empty);
            lines[first] = string.Empty;
        }

        if (!pythonNotebook && !explicitPython) return new PythonProjection(false, string.Empty);
        string kept = string.Join('\n', lines.Where(line =>
        {
            string trimmed = line.TrimStart();
            return !trimmed.StartsWith('!') && !trimmed.StartsWith('%') && !trimmed.StartsWith('?');
        }));
        return new PythonProjection(true, kept);
    }

    private static string NormalizeText(string value) => string.Join('\n',
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n').Select(line => line.TrimEnd()));

    private static void AppendTags(JsonElement cell, StringBuilder builder)
    {
        if (!cell.TryGetProperty("metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty("tags", out JsonElement tags) || tags.ValueKind != JsonValueKind.Array) return;
        foreach (string tag in tags.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty).Order(StringComparer.Ordinal))
            AppendField(builder, "tag", tag);
    }

    private static void AppendField(StringBuilder builder, string name, string value) =>
        builder.Append(name).Append(':').Append(value.Length).Append(':').Append(value).Append(';');

    private static string Digest(StringBuilder value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();

    private sealed record PythonProjection(bool IsPython, string Source);

    private sealed record NotebookSections(string Code, string Markdown, string Metadata);
}
