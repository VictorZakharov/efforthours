using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class JavaFormattingNormalizer
{
    private static readonly string[] MultiCharacterOperators =
    [
        ">>>=", "<<=", ">>=", "...", "::", "->", "==", "!=", "<=", ">=", "&&", "||",
        "++", "--", "<<", ">>>", ">>", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Contains("\\u", StringComparison.Ordinal))
        {
            signature = string.Empty;
            return false;
        }

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
            else if (current == '"')
            {
                valid = ReadString(source, ref index, result);
            }
            else if (current == '\'')
            {
                valid = ReadQuoted(source, ref index, '\'', result);
            }
            else if (CharacterStartsIdentifier(current))
            {
                int start = index++;
                while (index < source.Length && CharacterContinuesIdentifier(source[index])) index++;
                Append(result, source[start..index]);
            }
            else if (char.IsDigit(current) || current == '.' && char.IsDigit(Peek(source, index, 1)))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_' or '+' or '-'))
                {
                    if (source[index] is '+' or '-' &&
                        source[index - 1] is not ('e' or 'E' or 'p' or 'P')) break;
                    index++;
                }
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

    private static bool ReadString(string source, ref int index, StringBuilder result)
    {
        if (Peek(source, index, 1) == '"' && Peek(source, index, 2) == '"')
            return ReadTextBlock(source, ref index, result);
        return ReadQuoted(source, ref index, '"', result);
    }

    private static bool ReadQuoted(
        string source,
        ref int index,
        char quote,
        StringBuilder result)
    {
        int start = index++;
        while (index < source.Length)
        {
            if (source[index] == '\\')
            {
                index += Math.Min(2, source.Length - index);
            }
            else if (source[index] == quote)
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

    private static bool ReadTextBlock(string source, ref int index, StringBuilder result)
    {
        int start = index;
        index += 3;
        while (index + 2 < source.Length)
        {
            if (source[index] == '"' && source[index + 1] == '"' && source[index + 2] == '"')
            {
                index += 3;
                Append(result, source[start..index]);
                return true;
            }

            if (source[index] == '\\') index += Math.Min(2, source.Length - index);
            else index++;
        }

        return false;
    }

    private static void ReadLineComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) == '/';
        index += 2;
        while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
        if (documentation)
            Append(result, "markdown-doc:" + CollapseWhitespace(source[start..index]));
    }

    private static bool ReadBlockComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool javadoc = Peek(source, index, 2) == '*';
        index += 2;
        while (index + 1 < source.Length)
        {
            if (source[index] == '*' && source[index + 1] == '/')
            {
                index += 2;
                if (javadoc) Append(result, "javadoc:" + CollapseWhitespace(source[start..index]));
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
            if (char.IsWhiteSpace(character))
            {
                pending = result.Length > 0;
            }
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

    private static bool CharacterStartsIdentifier(char value) => value is '_' or '$' || char.IsLetter(value);

    private static bool CharacterContinuesIdentifier(char value) =>
        value is '_' or '$' || char.IsLetterOrDigit(value);

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';
}
