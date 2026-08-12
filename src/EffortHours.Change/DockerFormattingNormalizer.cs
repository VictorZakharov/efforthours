using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class DockerFormattingNormalizer
{
    public static bool TryCreateDockerfileSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] lines = NormalizeLines(source);
        StringBuilder result = new(source.Length);
        char escape = '\\';
        bool sawInstruction = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string trimmed = lines[index].Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith('#'))
            {
                if (!sawInstruction && TryDirective(trimmed, ref escape, out string? directive))
                    Append(result, directive!);
                continue;
            }

            sawInstruction = true;
            StringBuilder logical = new(lines[index].TrimStart());
            while (HasContinuation(logical, escape))
            {
                RemoveContinuation(logical, escape);
                bool found = false;
                while (++index < lines.Length)
                {
                    string continuation = lines[index].TrimStart();
                    if (continuation.Length == 0 || continuation.StartsWith('#')) continue;
                    if (logical.Length > 0) logical.Append(' ');
                    logical.Append(continuation);
                    found = true;
                    break;
                }

                if (!found)
                {
                    signature = string.Empty;
                    return false;
                }
            }

            string instruction = logical.ToString();
            if (instruction.Contains("<<", StringComparison.Ordinal))
            {
                signature = string.Empty;
                return false;
            }

            int separator = instruction.IndexOfAny([' ', '\t']);
            string keyword = (separator < 0 ? instruction : instruction[..separator]).Trim();
            if (keyword.Length == 0 || keyword.Any(character => !char.IsAsciiLetter(character)))
            {
                signature = string.Empty;
                return false;
            }

            string arguments = separator < 0 ? string.Empty : instruction[(separator + 1)..].Trim();
            Append(result, keyword.ToUpperInvariant());
            Append(result, arguments);
        }

        signature = result.ToString();
        return true;
    }

    public static bool TryCreateComposeSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] lines = NormalizeLines(source);
        StringBuilder result = new(source.Length);
        List<int> indents = [];
        for (int index = 0; index < lines.Length; index++)
        {
            string raw = lines[index];
            int indent = 0;
            while (indent < raw.Length && raw[indent] == ' ') indent++;
            if (indent < raw.Length && raw[indent] == '\t')
            {
                signature = string.Empty;
                return false;
            }

            if (!TryStripComment(raw[indent..], out string content))
            {
                signature = string.Empty;
                return false;
            }

            content = content.TrimEnd();
            if (content.Length == 0) continue;
            if (ContainsBlockScalarIndicator(content))
            {
                signature = string.Empty;
                return false;
            }

            int depth = StructuralDepth(indents, indent);
            if (depth < 0)
            {
                signature = string.Empty;
                return false;
            }

            if (!TryNormalizeMappingLine(content.TrimStart(), out string normalized))
            {
                signature = string.Empty;
                return false;
            }

            Append(result, depth.ToString(CultureInfo.InvariantCulture));
            Append(result, normalized);
        }

        signature = result.ToString();
        return true;
    }

    public static string CreateDockerIgnoreSignature(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        foreach (string line in NormalizeLines(source))
        {
            string pattern = line.Trim();
            if (pattern.Length == 0 || pattern.StartsWith('#')) continue;
            Append(result, pattern);
        }

        return result.ToString();
    }

    private static bool TryDirective(string line, ref char escape, out string? signature)
    {
        string value = line[1..].Trim();
        int equals = value.IndexOf('=');
        if (equals <= 0)
        {
            signature = null;
            return false;
        }

        string key = value[..equals].Trim().ToLowerInvariant();
        string argument = value[(equals + 1)..].Trim();
        if (key is not ("syntax" or "escape" or "check"))
        {
            signature = null;
            return false;
        }

        if (key == "escape")
        {
            if (argument is not ("\\" or "`"))
            {
                signature = null;
                return false;
            }

            escape = argument[0];
        }

        signature = $"directive:{key}={argument}";
        return true;
    }

    private static int StructuralDepth(List<int> indents, int indent)
    {
        if (indents.Count == 0)
        {
            indents.Add(indent);
            return 0;
        }

        if (indent > indents[^1])
        {
            indents.Add(indent);
            return indents.Count - 1;
        }

        int existing = indents.LastIndexOf(indent);
        if (existing < 0) return -1;
        if (existing + 1 < indents.Count) indents.RemoveRange(existing + 1, indents.Count - existing - 1);
        return existing;
    }

    private static bool TryStripComment(string value, out string content)
    {
        char quote = '\0';
        bool escaped = false;
        int flowDepth = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (quote == '"' && escaped) { escaped = false; continue; }
            if (quote == '"' && current == '\\') { escaped = true; continue; }
            if (quote != '\0')
            {
                if (current == quote)
                {
                    if (quote == '\'' && index + 1 < value.Length && value[index + 1] == '\'') index++;
                    else quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"') quote = current;
            else if (current is '[' or '{') flowDepth++;
            else if (current is ']' or '}')
            {
                if (--flowDepth < 0) { content = string.Empty; return false; }
            }
            else if (current == '#' && (index == 0 || char.IsWhiteSpace(value[index - 1])))
            {
                content = value[..index];
                return quote == '\0' && flowDepth == 0;
            }
        }

        content = value;
        return quote == '\0' && flowDepth == 0 && !escaped;
    }

    private static bool TryNormalizeMappingLine(string content, out string normalized)
    {
        bool sequence = content.StartsWith("- ", StringComparison.Ordinal) || content == "-";
        string payload = sequence ? content[1..].TrimStart() : content;
        int colon = FindMappingColon(payload);
        if (colon < 0)
        {
            normalized = sequence ? "- " + payload.Trim() : payload.Trim();
            return true;
        }

        string key = payload[..colon].Trim();
        if (key.Length == 0)
        {
            normalized = string.Empty;
            return false;
        }

        string value = payload[(colon + 1)..].Trim();
        normalized = (sequence ? "- " : string.Empty) + key + ":" +
            (value.Length == 0 ? string.Empty : " " + value);
        return true;
    }

    private static int FindMappingColon(string value)
    {
        char quote = '\0';
        bool escaped = false;
        int flowDepth = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (quote == '"' && escaped) { escaped = false; continue; }
            if (quote == '"' && current == '\\') { escaped = true; continue; }
            if (quote != '\0')
            {
                if (current == quote) quote = '\0';
                continue;
            }

            if (current is '\'' or '"') quote = current;
            else if (current is '[' or '{') flowDepth++;
            else if (current is ']' or '}') flowDepth--;
            else if (current == ':' && flowDepth == 0 &&
                (index + 1 == value.Length || char.IsWhiteSpace(value[index + 1]))) return index;
        }

        return -1;
    }

    private static bool ContainsBlockScalarIndicator(string content)
    {
        int colon = FindMappingColon(content.StartsWith("- ", StringComparison.Ordinal)
            ? content[2..]
            : content);
        if (colon < 0) return false;
        int valueStart = (content.StartsWith("- ", StringComparison.Ordinal) ? 2 : 0) + colon + 1;
        string value = content[valueStart..].TrimStart();
        return value.StartsWith('|') || value.StartsWith('>');
    }

    private static bool HasContinuation(StringBuilder value, char escape)
    {
        int index = value.Length - 1;
        while (index >= 0 && char.IsWhiteSpace(value[index])) index--;
        if (index < 0 || value[index] != escape) return false;
        int count = 0;
        while (index >= 0 && value[index--] == escape) count++;
        return count % 2 == 1;
    }

    private static void RemoveContinuation(StringBuilder value, char escape)
    {
        while (value.Length > 0 && char.IsWhiteSpace(value[^1])) value.Length--;
        if (value.Length > 0 && value[^1] == escape) value.Length--;
        while (value.Length > 0 && char.IsWhiteSpace(value[^1])) value.Length--;
    }

    private static string[] NormalizeLines(string source) => source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n');

    private static void Append(StringBuilder result, string token)
    {
        result.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':').Append(token).Append(';');
    }
}
