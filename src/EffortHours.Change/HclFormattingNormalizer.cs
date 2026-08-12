using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class HclFormattingNormalizer
{
    private const int MaximumHeredocDelimiterLength = 128;

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        Stack<char> delimiters = [];
        int index = 0;
        bool pendingNewLine = false;
        while (index < source.Length)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (current is '\r' or '\n')
            {
                SkipNewLine(source, ref index);
                pendingNewLine = result.Length > 0;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (pendingNewLine)
            {
                Append(result, "semantic-newline");
                pendingNewLine = false;
            }

            int start = index;
            if (current == '#' || current == '/' && next == '/')
            {
                index += current == '#' ? 1 : 2;
                while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
                Append(result, source[start..index].TrimEnd());
                continue;
            }

            if (current == '/' && next == '*')
            {
                if (!ReadBlockComment(source, ref index, result))
                {
                    signature = string.Empty;
                    return false;
                }

                continue;
            }

            if (current == '"')
            {
                if (!ReadQuoted(source, ref index, result))
                {
                    signature = string.Empty;
                    return false;
                }

                continue;
            }

            if (current == '<' && next == '<')
            {
                if (!ReadHeredoc(source, ref index, result))
                {
                    signature = string.Empty;
                    return false;
                }

                pendingNewLine = true;
                continue;
            }

            string token;
            if (char.IsLetter(current) || current == '_')
            {
                index++;
                while (index < source.Length &&
                    (char.IsLetterOrDigit(source[index]) || source[index] is '_' or '-')) index++;
                token = source[start..index];
            }
            else if (char.IsAsciiDigit(current) || current == '.' && char.IsAsciiDigit(next))
            {
                index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_' or '+' or '-'))
                {
                    index++;
                }

                token = source[start..index];
            }
            else
            {
                token = ReadSymbol(source, ref index);
                if (!UpdateDelimiters(token, delimiters))
                {
                    signature = string.Empty;
                    return false;
                }
            }

            Append(result, token);
        }

        if (delimiters.Count > 0)
        {
            signature = string.Empty;
            return false;
        }

        signature = result.ToString();
        return true;
    }

    private static bool ReadQuoted(string source, ref int index, StringBuilder result)
    {
        int start = index++;
        bool escaped = false;
        while (index < source.Length)
        {
            char current = source[index++];
            if (current is '\r' or '\n') return false;
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\') escaped = true;
            else if (current == '"')
            {
                Append(result, source[start..index]);
                return true;
            }
        }

        return false;
    }

    private static bool ReadBlockComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        index += 2;
        while (index + 1 < source.Length)
        {
            if (source[index] == '*' && source[index + 1] == '/')
            {
                index += 2;
                Append(result, NormalizeNewLines(source[start..index]));
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool ReadHeredoc(string source, ref int index, StringBuilder result)
    {
        int start = index;
        int cursor = index + 2;
        bool indented = cursor < source.Length && source[cursor] == '-';
        if (indented) cursor++;
        while (cursor < source.Length && source[cursor] is ' ' or '\t') cursor++;
        int delimiterStart = cursor;
        while (cursor < source.Length && source[cursor] is not ('\r' or '\n') &&
            cursor - delimiterStart <= MaximumHeredocDelimiterLength) cursor++;
        string delimiter = source[delimiterStart..cursor].Trim().Trim('"');
        if (delimiter.Length == 0 || delimiter.Length > MaximumHeredocDelimiterLength ||
            cursor >= source.Length) return false;

        SkipNewLine(source, ref cursor);
        while (cursor < source.Length)
        {
            int lineStart = cursor;
            while (cursor < source.Length && source[cursor] is not ('\r' or '\n')) cursor++;
            ReadOnlySpan<char> line = source.AsSpan(lineStart, cursor - lineStart);
            if ((indented ? line.TrimStart() : line).SequenceEqual(delimiter))
            {
                if (cursor < source.Length) SkipNewLine(source, ref cursor);
                index = cursor;
                Append(result, NormalizeNewLines(source[start..cursor]));
                return true;
            }

            if (cursor < source.Length) SkipNewLine(source, ref cursor);
        }

        return false;
    }

    private static string ReadSymbol(string source, ref int index)
    {
        char first = source[index++];
        if (index + 1 < source.Length && first == '.' && source[index] == '.' && source[index + 1] == '.')
        {
            index += 2;
            return "...";
        }

        if (index < source.Length && (first, source[index]) is
            ('=', '=') or ('!', '=') or ('<', '=') or ('>', '=') or
            ('&', '&') or ('|', '|') or ('=', '>') or (':', ':'))
        {
            return string.Concat(first, source[index++]);
        }

        return first.ToString();
    }

    private static bool UpdateDelimiters(string token, Stack<char> delimiters)
    {
        if (token.Length != 1) return true;
        char value = token[0];
        if (value is '(' or '[' or '{')
        {
            delimiters.Push(value);
            return true;
        }

        if (value is not (')' or ']' or '}')) return true;
        if (delimiters.Count == 0) return false;
        char expected = value switch { ')' => '(', ']' => '[', _ => '{' };
        return delimiters.Pop() == expected;
    }

    private static void SkipNewLine(string source, ref int index)
    {
        if (index < source.Length && source[index] == '\r') index++;
        if (index < source.Length && source[index] == '\n') index++;
    }

    private static string NormalizeNewLines(string value) => value
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n');

    private static void Append(StringBuilder result, string token)
    {
        result.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':').Append(token).Append(';');
    }
}
