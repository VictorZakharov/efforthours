using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Analyzers.Python;

internal static class JupyterNotebookParser
{
    private const int MaximumCells = 10_000;
    private const int MaximumCellCharacters = 1 * 1024 * 1024;

    public static JupyterNotebookParseResult Parse(string json, string path)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("cells", out JsonElement cells) ||
                cells.ValueKind != JsonValueKind.Array)
            {
                return JupyterNotebookParseResult.Failure("The notebook root must contain a cells array.");
            }

            JsonElement root = document.RootElement;
            string declaredLanguage = DeclaredLanguage(root);
            bool pythonNotebook = declaredLanguage == "python";
            bool safeguard = cells.GetArrayLength() > MaximumCells;
            int totalCells = cells.GetArrayLength();
            int codeCells = 0;
            int pythonCells = 0;
            int markdownCells = 0;
            int duplicateMarkdownCells = 0;
            int rawCells = 0;
            int unsupportedCells = 0;
            int duplicateCells = 0;
            int outputCells = 0;
            int executionCells = 0;
            int attachmentCells = 0;
            int magicLines = 0;
            int shellLines = 0;
            int markdownLines = 0;
            int markdownHeadings = 0;
            int markdownLinks = 0;
            HashSet<string> uniqueCode = new(StringComparer.Ordinal);
            HashSet<string> uniqueMarkdown = new(StringComparer.Ordinal);
            PythonSourceMetrics combined = new();
            string confidence = "medium";
            using IncrementalHash projection = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Append(projection, $"language:{declaredLanguage}\n");

            int index = 0;
            foreach (JsonElement cell in cells.EnumerateArray())
            {
                if (index++ >= MaximumCells) break;
                if (cell.ValueKind != JsonValueKind.Object ||
                    !cell.TryGetProperty("cell_type", out JsonElement typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String)
                {
                    safeguard = true;
                    confidence = "low";
                    continue;
                }

                string cellType = typeElement.GetString() ?? string.Empty;
                if (!TryReadSource(cell, out string source))
                {
                    safeguard = true;
                    confidence = "low";
                    continue;
                }

                string normalized = NormalizeText(source);
                if (HasNonEmptyContainer(cell, "attachments")) attachmentCells++;

                if (cellType == "code")
                {
                    codeCells++;
                    if (HasNonEmptyContainer(cell, "outputs")) outputCells++;
                    if (cell.TryGetProperty("execution_count", out JsonElement execution) &&
                        execution.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                        executionCells++;

                    PythonCellProjection prepared = PreparePythonCell(normalized, pythonNotebook);
                    magicLines += prepared.MagicLines;
                    shellLines += prepared.ShellEscapeLines;
                    if (!prepared.IsPython)
                    {
                        unsupportedCells++;
                        confidence = "low";
                        continue;
                    }

                    pythonCells++;
                    if (prepared.Source.Length == 0) continue;
                    string digest = Digest(prepared.Source);
                    if (!uniqueCode.Add(digest))
                    {
                        duplicateCells++;
                        continue;
                    }

                    Append(projection, "cell:python\n");
                    Append(projection, prepared.Source);
                    AppendSemanticTags(cell, projection);

                    PythonSyntaxAnalysis syntax = PythonSyntaxAnalyzer.Analyze(prepared.Source, path);
                    if (syntax.Confidence == "low") confidence = "low";
                    Add(combined, syntax.Metrics);
                }
                else if (cellType == "markdown")
                {
                    markdownCells++;
                    if (!uniqueMarkdown.Add(Digest(normalized)))
                    {
                        duplicateMarkdownCells++;
                        continue;
                    }
                    Append(projection, "cell:markdown\n");
                    Append(projection, normalized);
                    AppendSemanticTags(cell, projection);
                    CountMarkdown(normalized, ref markdownLines, ref markdownHeadings, ref markdownLinks);
                }
                else
                {
                    rawCells++;
                    confidence = "low";
                }
            }

            if (safeguard) confidence = "low";
            bool widgets = root.TryGetProperty("metadata", out JsonElement metadata) &&
                metadata.ValueKind == JsonValueKind.Object &&
                (metadata.TryGetProperty("widgets", out _) ||
                 metadata.TryGetProperty("application/vnd.jupyter.widget-state+json", out _));
            string projectionDigest = Convert.ToHexString(projection.GetHashAndReset()).ToLowerInvariant();
            return new JupyterNotebookParseResult(
                null,
                confidence,
                declaredLanguage,
                totalCells,
                codeCells,
                pythonCells,
                markdownCells,
                uniqueMarkdown.Count,
                duplicateMarkdownCells,
                rawCells,
                unsupportedCells,
                uniqueCode.Count,
                duplicateCells,
                outputCells,
                executionCells,
                attachmentCells,
                magicLines,
                shellLines,
                markdownLines,
                markdownHeadings,
                markdownLinks,
                widgets,
                safeguard,
                projectionDigest,
                new PythonSyntaxAnalysis(confidence, combined));
        }
        catch (JsonException exception)
        {
            return JupyterNotebookParseResult.Failure($"Invalid bounded notebook JSON: {exception.GetType().Name}.");
        }
    }

    private static string DeclaredLanguage(JsonElement root)
    {
        if (!root.TryGetProperty("metadata", out JsonElement metadata) ||
            metadata.ValueKind != JsonValueKind.Object) return "unknown";
        string language = NestedString(metadata, "language_info", "name");
        string kernelLanguage = NestedString(metadata, "kernelspec", "language");
        string kernelName = NestedString(metadata, "kernelspec", "name");
        string[] families = [.. new[] { language, kernelLanguage, kernelName }
            .Where(value => value.Length > 0)
            .Select(LanguageFamily)
            .Where(value => value != "unknown")
            .Distinct(StringComparer.Ordinal)];
        return families.Length switch
        {
            0 => "unknown",
            1 => families[0],
            _ => "mixed",
        };
    }

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

    private static string NestedString(JsonElement parent, string objectName, string propertyName) =>
        parent.TryGetProperty(objectName, out JsonElement nested) && nested.ValueKind == JsonValueKind.Object &&
        nested.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

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

    private static PythonCellProjection PreparePythonCell(string source, bool pythonNotebook)
    {
        string[] lines = source.Split('\n');
        int first = Array.FindIndex(lines, line => line.Trim().Length > 0);
        bool explicitPython = false;
        int magic = 0;
        int shell = 0;
        if (first >= 0 && lines[first].TrimStart().StartsWith("%%", StringComparison.Ordinal))
        {
            string cellMagic = lines[first].TrimStart()[2..].Split([' ', '\t'], 2)[0].ToLowerInvariant();
            magic++;
            explicitPython = cellMagic is "python" or "python3" or "ipython";
            if (!explicitPython) return new PythonCellProjection(false, string.Empty, magic, shell);
            lines[first] = string.Empty;
        }

        if (!pythonNotebook && !explicitPython)
            return new PythonCellProjection(false, string.Empty, magic, shell);
        StringBuilder kept = new(source.Length);
        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith('!')) { shell++; continue; }
            if (trimmed.StartsWith('%') || trimmed.StartsWith('?')) { magic++; continue; }
            kept.AppendLine(line);
        }

        return new PythonCellProjection(true, kept.ToString(), magic, shell);
    }

    private static void CountMarkdown(string source, ref int lines, ref int headings, ref int links)
    {
        foreach (string line in source.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            lines++;
            if (trimmed.StartsWith('#')) headings++;
            links += Count(trimmed, "](");
        }
    }

    private static int Count(string value, string candidate)
    {
        int result = 0;
        int offset = 0;
        while ((offset = value.IndexOf(candidate, offset, StringComparison.Ordinal)) >= 0)
        {
            result++;
            offset += candidate.Length;
        }

        return result;
    }

    private static string NormalizeText(string value)
    {
        string[] lines = [.. value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n').Select(line => line.TrimEnd())];
        int start = 0;
        int end = lines.Length;
        while (start < end && lines[start].Length == 0) start++;
        while (end > start && lines[end - 1].Length == 0) end--;
        return string.Join('\n', lines[start..end]);
    }

    private static void AppendSemanticTags(JsonElement cell, IncrementalHash hash)
    {
        if (!cell.TryGetProperty("metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty("tags", out JsonElement tags) || tags.ValueKind != JsonValueKind.Array) return;
        foreach (string tag in tags.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Order(StringComparer.Ordinal))
            Append(hash, $"tag:{tag}\n");
    }

    private static bool HasNonEmptyContainer(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => false,
        };
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void Append(IncrementalHash hash, string value) =>
        hash.AppendData(Encoding.UTF8.GetBytes(value));

    private static void Add(PythonSourceMetrics target, PythonSourceMetrics source)
    {
        target.Imports += source.Imports; target.InternalImports += source.InternalImports;
        target.Functions += source.Functions; target.Methods += source.Methods;
        target.Classes += source.Classes; target.PublicSymbols += source.PublicSymbols;
        target.AsyncUnits += source.AsyncUnits; target.BranchPoints += source.BranchPoints;
        target.Decorators += source.Decorators; target.TypeAnnotations += source.TypeAnnotations;
        target.Calls += source.Calls; target.EntryPoints += source.EntryPoints;
        target.ApiEndpoints += source.ApiEndpoints; target.ApiTypes += source.ApiTypes;
        target.CliCommands += source.CliCommands; target.DataModels += source.DataModels;
        target.DataCalls += source.DataCalls; target.Migrations += source.Migrations;
        target.IntegrationCalls += source.IntegrationCalls; target.SecurityUsages += source.SecurityUsages;
        target.BackgroundUsages += source.BackgroundUsages; target.ValidationTypes += source.ValidationTypes;
        target.ValidationRules += source.ValidationRules; target.TestCases += source.TestCases;
        target.ParameterizedCases += source.ParameterizedCases; target.Assertions += source.Assertions;
        target.MockUsages += source.MockUsages; target.DataAnalysisCalls += source.DataAnalysisCalls;
        target.VisualizationCalls += source.VisualizationCalls;
        target.ImportsSeen.UnionWith(source.ImportsSeen);
        target.Technologies.UnionWith(source.Technologies);
    }

    private sealed record PythonCellProjection(bool IsPython, string Source, int MagicLines, int ShellEscapeLines);
}

internal sealed record JupyterNotebookParseResult(
    string? Error,
    string Confidence,
    string DeclaredLanguage,
    int TotalCells,
    int CodeCells,
    int PythonCodeCells,
    int MarkdownCells,
    int UniqueMarkdownCells,
    int DuplicateMarkdownCells,
    int RawCells,
    int UnsupportedCodeCells,
    int UniqueCodeCells,
    int DuplicateCodeCells,
    int OutputCells,
    int ExecutionCountCells,
    int AttachmentCells,
    int MagicLines,
    int ShellEscapeLines,
    int MarkdownLines,
    int MarkdownHeadings,
    int MarkdownLinks,
    bool HasWidgetState,
    bool SafeguardReached,
    string MaintainedProjectionDigest,
    PythonSyntaxAnalysis Syntax)
{
    public static JupyterNotebookParseResult Failure(string error) => new(
        error, "low", "unknown", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        false, true, string.Empty, new PythonSyntaxAnalysis("low", new PythonSourceMetrics()));
}
