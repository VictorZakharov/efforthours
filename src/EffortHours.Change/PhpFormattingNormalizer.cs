using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class PhpFormattingNormalizer
{
    private static readonly string[] MultiCharacterOperators =
    [
        "?->", "??=", "===", "!==", "<=>", "...", "**=", "<<=", ">>=",
        "#[", "->", "::", "=>", "??", "==", "!=", "<=", ">=", "&&", "||",
        "++", "--", "+=", "-=", "*=", "/=", ".=", "%=", "**", "<<", ">>",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        Stack<char> delimiters = [];
        bool hasOpenTag = source.Contains("<?", StringComparison.Ordinal);
        bool inPhp = !hasOpenTag;
        int index = 0;
        bool valid = true;
        while (index < source.Length && valid)
        {
            if (!inPhp)
            {
                int open = source.IndexOf("<?", index, StringComparison.Ordinal);
                int end = open < 0 ? source.Length : open;
                if (end > index) Append(result, "inline-html:" + source[index..end]);
                if (open < 0)
                {
                    index = source.Length;
                    break;
                }

                string tag = source.AsSpan(open).StartsWith("<?php", StringComparison.OrdinalIgnoreCase)
                    ? "<?php"
                    : source.AsSpan(open).StartsWith("<?=", StringComparison.Ordinal) ? "<?= " : "<?";
                Append(result, tag.TrimEnd());
                index = open + (tag.StartsWith("<?php", StringComparison.Ordinal) ? 5 :
                    tag.StartsWith("<?=", StringComparison.Ordinal) ? 3 : 2);
                inPhp = true;
                continue;
            }

            if (source.AsSpan(index).StartsWith("?>", StringComparison.Ordinal))
            {
                Append(result, "?>");
                index += 2;
                inPhp = false;
                continue;
            }

            char current = source[index];
            char next = Peek(source, index, 1);
            if (char.IsWhiteSpace(current))
            {
                index++;
            }
            else if (current == '/' && next == '/' || current == '#' && next != '[')
            {
                ReadLineComment(source, ref index);
            }
            else if (current == '/' && next == '*')
            {
                valid = ReadBlockComment(source, ref index, result);
            }
            else if (current is '\'' or '"' or '`')
            {
                valid = ReadQuoted(source, ref index, current, result);
            }
            else if (current == '<' && source.AsSpan(index).StartsWith("<<<", StringComparison.Ordinal))
            {
                valid = ReadHeredoc(source, ref index, result);
            }
            else if (current == '$' && CharacterStartsIdentifier(next))
            {
                int start = index++;
                while (index < source.Length && CharacterContinuesIdentifier(source[index])) index++;
                Append(result, source[start..index]);
            }
            else if (CharacterStartsIdentifier(current) || current == '\\')
            {
                int start = index++;
                while (index < source.Length &&
                    (CharacterContinuesIdentifier(source[index]) || source[index] == '\\')) index++;
                Append(result, source[start..index]);
            }
            else if (char.IsAsciiDigit(current))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_')) index++;
                Append(result, source[start..index]);
            }
            else
            {
                string token = MultiCharacterOperators.FirstOrDefault(candidate =>
                    Matches(source, index, candidate)) ?? current.ToString();
                index += token.Length;
                valid = UpdateDelimiters(token, delimiters);
                Append(result, token);
            }
        }

        valid &= delimiters.Count == 0;
        signature = valid ? result.ToString() : string.Empty;
        return valid;
    }

    private static bool ReadQuoted(
        string source,
        ref int index,
        char quote,
        StringBuilder result)
    {
        int start = index++;
        bool escaped = false;
        while (index < source.Length)
        {
            char current = source[index++];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\') escaped = true;
            else if (current == quote)
            {
                Append(result, source[start..index]);
                return true;
            }
        }

        return false;
    }

    private static bool ReadHeredoc(string source, ref int index, StringBuilder result)
    {
        int start = index;
        int cursor = index + 3;
        while (cursor < source.Length && source[cursor] is ' ' or '\t') cursor++;
        char quote = cursor < source.Length && source[cursor] is '\'' or '"' ? source[cursor++] : '\0';
        int delimiterStart = cursor;
        while (cursor < source.Length &&
            (char.IsAsciiLetterOrDigit(source[cursor]) || source[cursor] == '_')) cursor++;
        if (cursor == delimiterStart || cursor - delimiterStart > 128) return false;
        string delimiter = source[delimiterStart..cursor];
        if (quote != '\0' && (cursor >= source.Length || source[cursor++] != quote)) return false;
        while (cursor < source.Length && source[cursor] is ' ' or '\t') cursor++;
        if (cursor >= source.Length || source[cursor] is not ('\r' or '\n')) return false;
        SkipNewLine(source, ref cursor);
        while (cursor <= source.Length)
        {
            int lineEnd = cursor;
            while (lineEnd < source.Length && source[lineEnd] is not ('\r' or '\n')) lineEnd++;
            string candidate = source[cursor..lineEnd].TrimStart(' ', '\t');
            if (candidate.Equals(delimiter, StringComparison.Ordinal) ||
                candidate.Equals(delimiter + ";", StringComparison.Ordinal))
            {
                index = lineEnd;
                Append(result, source[start..index]);
                return true;
            }

            if (lineEnd >= source.Length) break;
            cursor = lineEnd;
            SkipNewLine(source, ref cursor);
        }

        return false;
    }

    private static void ReadLineComment(string source, ref int index)
    {
        index += source[index] == '#' ? 1 : 2;
        while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
    }

    private static bool ReadBlockComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) == '*';
        index += 2;
        while (index + 1 < source.Length)
        {
            if (source[index] == '*' && source[index + 1] == '/')
            {
                index += 2;
                if (documentation) Append(result, "phpdoc:" + CollapseWhitespace(source[start..index]));
                return true;
            }
            index++;
        }
        return false;
    }

    private static string CollapseWhitespace(string value)
    {
        StringBuilder result = new(value.Length);
        bool pending = false;
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character)) pending = result.Length > 0;
            else
            {
                if (pending) result.Append(' ');
                result.Append(character);
                pending = false;
            }
        }
        return result.ToString();
    }

    private static bool UpdateDelimiters(string token, Stack<char> delimiters)
    {
        char value = token == "#[" ? '[' : token[0];
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
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index += 2;
        else index++;
    }

    private static void Append(StringBuilder result, string token)
    {
        result.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':');
        result.Append(token);
        result.Append(';');
    }

    private static bool CharacterStartsIdentifier(char value) =>
        value == '_' || char.IsLetter(value) || value >= '\u0080';

    private static bool CharacterContinuesIdentifier(char value) =>
        value == '_' || char.IsLetterOrDigit(value) || value >= '\u0080';

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length && source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';
}
