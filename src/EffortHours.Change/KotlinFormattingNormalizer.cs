using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class KotlinFormattingNormalizer
{
    private static readonly string[] MultiCharacterOperators =
    [
        "===", "!==", ">>>", "..<", "::", "->", "?.", "?:", "!!", "!is", "!in",
        "==", "!=", "<=", ">=", "&&", "||", "++", "--", "<<", ">>", "+=", "-=",
        "*=", "/=", "%=", "..",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        Stack<char> delimiters = [];
        int index = 0;
        bool valid = true;
        string? previous = null;
        bool newline = false;
        while (index < source.Length && valid)
        {
            char current = source[index];
            if (char.IsWhiteSpace(current))
            {
                newline |= current is '\r' or '\n';
                index++;
            }
            else if (current == '/' && Peek(source, index, 1) == '/')
            {
                ReadLineComment(source, ref index, result, ref newline);
            }
            else if (current == '/' && Peek(source, index, 1) == '*')
            {
                valid = ReadBlockComment(source, ref index, result, ref newline);
            }
            else
            {
                if (newline && previous is "return" or "throw" or "break" or "continue")
                    Append(result, "semantic-newline");
                newline = false;
                string token;
                if (current == '"') valid = ReadString(source, ref index, result, out token);
                else if (current == '\'') valid = ReadQuoted(source, ref index, '\'', result, out token);
                else if (current == '`') valid = ReadBacktickIdentifier(source, ref index, result, out token);
                else if (CharacterStartsIdentifier(current))
                {
                    int start = index++;
                    while (index < source.Length && CharacterContinuesIdentifier(source[index])) index++;
                    token = source[start..index];
                    Append(result, token);
                }
                else if (char.IsDigit(current))
                {
                    int start = index++;
                    while (index < source.Length &&
                        (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_' or '+' or '-'))
                    {
                        if (source[index] is '+' or '-' &&
                            source[index - 1] is not ('e' or 'E' or 'p' or 'P')) break;
                        index++;
                    }
                    token = source[start..index];
                    Append(result, token);
                }
                else
                {
                    token = MultiCharacterOperators.FirstOrDefault(candidate =>
                        Matches(source, index, candidate)) ?? current.ToString();
                    index += token.Length;
                    valid = UpdateDelimiters(token, delimiters);
                    if (token != ";" && (token != "," || !IsTrailingComma(source, index)))
                        Append(result, token);
                }
                previous = token;
            }
        }

        valid &= delimiters.Count == 0;
        signature = valid ? result.ToString() : string.Empty;
        return valid;
    }

    private static bool ReadString(
        string source,
        ref int index,
        StringBuilder result,
        out string token)
    {
        if (Peek(source, index, 1) == '"' && Peek(source, index, 2) == '"')
            return ReadRawString(source, ref index, result, out token);
        return ReadQuoted(source, ref index, '"', result, out token);
    }

    private static bool ReadQuoted(
        string source,
        ref int index,
        char quote,
        StringBuilder result,
        out string token)
    {
        int start = index++;
        while (index < source.Length)
        {
            if (source[index] == '\\') index += Math.Min(2, source.Length - index);
            else if (source[index] == quote)
            {
                index++;
                token = source[start..index];
                Append(result, token);
                return true;
            }
            else if (source[index] is '\r' or '\n') break;
            else index++;
        }

        token = string.Empty;
        return false;
    }

    private static bool ReadRawString(
        string source,
        ref int index,
        StringBuilder result,
        out string token)
    {
        int start = index;
        index += 3;
        while (index + 2 < source.Length)
        {
            if (source[index] == '"' && source[index + 1] == '"' && source[index + 2] == '"')
            {
                index += 3;
                token = source[start..index];
                Append(result, token);
                return true;
            }
            index++;
        }

        token = string.Empty;
        return false;
    }

    private static bool ReadBacktickIdentifier(
        string source,
        ref int index,
        StringBuilder result,
        out string token)
    {
        int start = index++;
        while (index < source.Length && source[index] is not ('`' or '\r' or '\n')) index++;
        if (index >= source.Length || source[index] != '`')
        {
            token = string.Empty;
            return false;
        }
        index++;
        token = source[start..index];
        Append(result, token);
        return true;
    }

    private static void ReadLineComment(
        string source,
        ref int index,
        StringBuilder result,
        ref bool newline)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) == '/';
        index += 2;
        while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
        if (documentation) Append(result, "kdoc-line:" + CollapseWhitespace(source[start..index]));
        newline = true;
    }

    private static bool ReadBlockComment(
        string source,
        ref int index,
        StringBuilder result,
        ref bool newline)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) == '*';
        index += 2;
        int depth = 1;
        while (index < source.Length && depth > 0)
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
            }
            else
            {
                newline |= source[index] is '\r' or '\n';
                index++;
            }
        }
        if (depth > 0) return false;
        if (documentation) Append(result, "kdoc:" + CollapseWhitespace(source[start..index]));
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

    private static bool CharacterStartsIdentifier(char value) => value == '_' || char.IsLetter(value);

    private static bool CharacterContinuesIdentifier(char value) => value == '_' || char.IsLetterOrDigit(value);

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static bool IsTrailingComma(string source, int index)
    {
        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index])) index++;
            else if (source[index] == '/' && Peek(source, index, 1) == '/')
            {
                index += 2;
                while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
            }
            else if (source[index] == '/' && Peek(source, index, 1) == '*')
            {
                index += 2;
                int depth = 1;
                while (index < source.Length && depth > 0)
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
                    }
                    else index++;
                }
            }
            else return source[index] is ')' or ']' or '}';
        }
        return false;
    }

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';
}
