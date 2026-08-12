using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class RustFormattingNormalizer
{
    private static readonly string[] MultiCharacterOperators =
    [
        "<<=", ">>=", "..=", "...", "::", "->", "=>", "==", "!=", "<=", ">=", "&&",
        "||", "<<", ">>", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "..",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        Stack<char> delimiters = [];
        int index = 0;
        bool valid = true;
        while (index < source.Length && valid)
        {
            char current = source[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
            }
            else if (current == '/' && Peek(source, index, 1) == '/')
            {
                ReadLineComment(source, ref index, result);
            }
            else if (current == '/' && Peek(source, index, 1) == '*')
            {
                valid = ReadBlockComment(source, ref index, result);
            }
            else if (TryReadRawString(source, ref index, result, out bool rawRecognized))
            {
                valid = rawRecognized;
            }
            else if (IsQuotedStringStart(source, index))
            {
                valid = ReadQuotedString(source, ref index, result);
            }
            else if (current == '\'')
            {
                valid = ReadApostrophe(source, ref index, result);
            }
            else if (IsIdentifierStart(current))
            {
                int start = index++;
                if (source[start] == 'r' && Peek(source, index, 0) == '#' &&
                    IsIdentifierStart(Peek(source, index, 1))) index++;
                while (index < source.Length && IsIdentifierPart(source[index])) index++;
                Append(result, source[start..index]);
            }
            else if (char.IsDigit(current))
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

    private static void ReadLineComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) is '/' or '!';
        index += 2;
        while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
        if (documentation)
            Append(result, "rustdoc:" + CollapseWhitespace(source[start..index]));
    }

    private static bool ReadBlockComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) is '*' or '!';
        int depth = 0;
        while (index < source.Length)
        {
            if (source[index] == '/' && Peek(source, index, 1) == '*')
            {
                depth++;
                index += 2;
            }
            else if (source[index] == '*' && Peek(source, index, 1) == '/')
            {
                depth--;
                index += 2;
                if (depth == 0)
                {
                    if (documentation)
                        Append(result, "rustdoc:" + CollapseWhitespace(source[start..index]));
                    return true;
                }
            }
            else
            {
                index++;
            }
        }
        return false;
    }

    private static bool TryReadRawString(
        string source,
        ref int index,
        StringBuilder result,
        out bool valid)
    {
        int prefix = RawPrefixLength(source, index);
        if (prefix < 0)
        {
            valid = false;
            return false;
        }

        int start = index;
        index += prefix;
        int hashes = 0;
        while (Peek(source, index, 0) == '#') { hashes++; index++; }
        index++;
        while (index < source.Length)
        {
            if (source[index] == '"' && HasHashes(source, index + 1, hashes))
            {
                index += 1 + hashes;
                Append(result, source[start..index]);
                valid = true;
                return true;
            }
            index++;
        }
        valid = false;
        return true;
    }

    private static int RawPrefixLength(string source, int index)
    {
        int prefix = Peek(source, index, 0) == 'r' ? 1 :
            Peek(source, index, 0) is 'b' or 'c' && Peek(source, index, 1) == 'r' ? 2 : -1;
        if (prefix < 0) return -1;
        int cursor = index + prefix;
        while (Peek(source, cursor, 0) == '#') cursor++;
        return Peek(source, cursor, 0) == '"' ? prefix : -1;
    }

    private static bool HasHashes(string source, int index, int count)
    {
        for (int offset = 0; offset < count; offset++)
            if (Peek(source, index, offset) != '#') return false;
        return true;
    }

    private static bool IsQuotedStringStart(string source, int index) =>
        Peek(source, index, 0) == '"' ||
        Peek(source, index, 0) is 'b' or 'c' && Peek(source, index, 1) == '"';

    private static bool ReadQuotedString(string source, ref int index, StringBuilder result)
    {
        int start = index;
        if (source[index] is 'b' or 'c') index++;
        index++;
        while (index < source.Length)
        {
            if (source[index] == '\\')
            {
                index++;
                if (index < source.Length) index++;
            }
            else if (source[index] == '"')
            {
                index++;
                Append(result, source[start..index]);
                return true;
            }
            else if (source[index] is '\r' or '\n')
            {
                return false;
            }
            else
            {
                index++;
            }
        }
        return false;
    }

    private static bool ReadApostrophe(string source, ref int index, StringBuilder result)
    {
        int start = index++;
        if (index < source.Length && IsIdentifierStart(source[index]))
        {
            while (index < source.Length && IsIdentifierPart(source[index])) index++;
            if (index >= source.Length || source[index] != '\'')
            {
                Append(result, source[start..index]);
                return true;
            }
        }
        else if (index < source.Length && source[index] == '\\')
        {
            index += Math.Min(2, source.Length - index);
        }
        else if (index < source.Length)
        {
            index++;
        }

        if (index >= source.Length || source[index] != '\'') return false;
        index++;
        Append(result, source[start..index]);
        return true;
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

    private static void Append(StringBuilder result, string token)
    {
        result.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':');
        result.Append(token);
        result.Append(';');
    }

    private static bool IsIdentifierStart(char value) => value == '_' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value) => value == '_' || char.IsLetterOrDigit(value);

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';
}
