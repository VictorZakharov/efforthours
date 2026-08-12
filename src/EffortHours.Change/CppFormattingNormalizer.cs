using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class CppFormattingNormalizer
{
    private static readonly string[] MultiCharacterOperators =
    [
        "<=>", ">>=", "<<=", "->*", "...", "::", "->", ".*", "++", "--", "<<", ">>",
        "<=", ">=", "==", "!=", "&&", "||", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "##", "<:", ":>", "<%", "%>", "%:",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        Stack<char> delimiters = [];
        Stack<bool> conditionals = [];
        int index = 0;
        bool lineStart = true;
        bool preprocessor = false;
        bool valid = true;
        while (index < source.Length && valid)
        {
            char current = source[index];
            if (current is ' ' or '\t' or '\v' or '\f') index++;
            else if (current is '\r' or '\n')
            {
                ConsumeNewLine(source, ref index);
                lineStart = true;
                preprocessor = false;
            }
            else if (current == '\\' && Peek(source, index, 1) is '\r' or '\n')
            {
                index++;
                ConsumeNewLine(source, ref index);
            }
            else if (lineStart && current == '#')
            {
                valid = UpdateConditionalState(source, index, conditionals);
                Append(result, "P", "#");
                index++;
                lineStart = false;
                preprocessor = true;
            }
            else if (current == '/' && Peek(source, index, 1) == '/')
            {
                valid = ReadLineComment(source, ref index, result);
            }
            else if (current == '/' && Peek(source, index, 1) == '*')
            {
                valid = ReadBlockComment(source, ref index, result);
            }
            else if (TryReadRawString(source, ref index, result, out bool rawRecognized))
            {
                valid = rawRecognized;
                lineStart = false;
            }
            else if (IsQuotedStringStart(source, index))
            {
                valid = ReadQuotedString(source, ref index, result);
                lineStart = false;
            }
            else if (IsIdentifierStart(current))
            {
                int start = index++;
                while (index < source.Length && IsIdentifierPart(source[index])) index++;
                Append(result, "I", source[start..index]);
                lineStart = false;
            }
            else if (char.IsDigit(current) || current == '.' && char.IsDigit(Peek(source, index, 1)))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_' or '\'' ||
                     source[index] is '+' or '-' && index > start &&
                     source[index - 1] is 'e' or 'E' or 'p' or 'P')) index++;
                Append(result, "N", source[start..index]);
                lineStart = false;
            }
            else
            {
                string token = MultiCharacterOperators.FirstOrDefault(value => Matches(source, index, value)) ??
                    current.ToString();
                index += token.Length;
                if (!preprocessor && token.Length == 1 && token[0] is '(' or '[' or '{')
                    delimiters.Push(token[0]);
                else if (!preprocessor && token.Length == 1 && token[0] is ')' or ']' or '}')
                {
                    char expected = token[0] switch { ')' => '(', ']' => '[', _ => '{' };
                    valid = delimiters.Count > 0 && delimiters.Pop() == expected;
                }
                Append(result, "O", token);
                lineStart = false;
            }
        }
        valid &= delimiters.Count == 0 && conditionals.Count == 0;
        signature = valid ? result.ToString() : string.Empty;
        return valid;
    }

    private static bool ReadLineComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) is '/' or '!';
        index += 2;
        while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
        if (documentation) Append(result, "D", source[start..index]);
        return true;
    }

    private static bool UpdateConditionalState(string source, int index, Stack<bool> conditionals)
    {
        int cursor = index + 1;
        while (cursor < source.Length && source[cursor] is ' ' or '\t' or '\v' or '\f') cursor++;
        int start = cursor;
        while (cursor < source.Length && (char.IsAsciiLetter(source[cursor]) || source[cursor] == '_'))
            cursor++;
        string name = source[start..cursor];
        switch (name)
        {
            case "if":
            case "ifdef":
            case "ifndef":
                conditionals.Push(false);
                return true;
            case "elif":
            case "elifdef":
            case "elifndef":
                return conditionals.Count > 0 && !conditionals.Peek();
            case "else":
                if (conditionals.Count == 0 || conditionals.Pop()) return false;
                conditionals.Push(true);
                return true;
            case "endif":
                if (conditionals.Count == 0) return false;
                conditionals.Pop();
                return true;
            default:
                return true;
        }
    }

    private static bool ReadBlockComment(string source, ref int index, StringBuilder result)
    {
        int start = index;
        bool documentation = Peek(source, index, 2) is '*' or '!';
        index += 2;
        while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/')) index++;
        if (index + 1 >= source.Length) return false;
        index += 2;
        if (documentation) Append(result, "D", source[start..index]);
        return true;
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
            valid = true;
            return false;
        }
        int start = index;
        index += prefix;
        int delimiterStart = index;
        while (index < source.Length && source[index] != '(' &&
            source[index] is not ('\r' or '\n') && index - delimiterStart <= 16) index++;
        if (index >= source.Length || source[index] != '(' || index - delimiterStart > 16)
        {
            index = start;
            valid = false;
            return true;
        }
        string delimiter = source[delimiterStart..index];
        if (!ValidRawDelimiter(delimiter))
        {
            index = start;
            valid = false;
            return true;
        }
        index++;
        string closing = ")" + delimiter + '"';
        int end = source.IndexOf(closing, index, StringComparison.Ordinal);
        if (end < 0)
        {
            index = source.Length;
            valid = false;
            return true;
        }
        index = end + closing.Length;
        Append(result, "S", source[start..index]);
        valid = true;
        return true;
    }

    private static bool ReadQuotedString(string source, ref int index, StringBuilder result)
    {
        int start = index;
        if (source[index] == 'u' && Peek(source, index, 1) == '8') index += 2;
        else if (source[index] is 'u' or 'U' or 'L') index++;
        char quote = source[index++];
        bool closed = false;
        while (index < source.Length)
        {
            if (source[index] == '\\')
            {
                index++;
                if (index < source.Length)
                {
                    if (source[index] is '\r' or '\n') ConsumeNewLine(source, ref index);
                    else index++;
                }
            }
            else if (source[index] == quote)
            {
                index++;
                closed = true;
                break;
            }
            else if (source[index] is '\r' or '\n') break;
            else index++;
        }
        if (closed) Append(result, quote == '\'' ? "C" : "S", source[start..index]);
        return closed;
    }

    private static int RawPrefixLength(string source, int index)
    {
        if (Peek(source, index) == 'R' && Peek(source, index, 1) == '"') return 2;
        if (Peek(source, index) == 'u' && Peek(source, index, 1) == '8' &&
            Peek(source, index, 2) == 'R' && Peek(source, index, 3) == '"') return 4;
        if (Peek(source, index) is 'u' or 'U' or 'L' && Peek(source, index, 1) == 'R' &&
            Peek(source, index, 2) == '"') return 3;
        return -1;
    }

    private static bool ValidRawDelimiter(string delimiter) => delimiter.All(character =>
        character is >= (char)0x21 and <= (char)0x7e && character is not ('(' or ')' or '\\'));

    private static bool IsQuotedStringStart(string source, int index)
    {
        if (Peek(source, index) is '"' or '\'') return true;
        if (Peek(source, index) == 'u' && Peek(source, index, 1) == '8' &&
            Peek(source, index, 2) is '"' or '\'') return true;
        return Peek(source, index) is 'u' or 'U' or 'L' &&
            Peek(source, index, 1) is '"' or '\'';
    }

    private static void Append(StringBuilder result, string kind, string value)
    {
        result.Append(kind);
        result.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':');
        result.Append(value);
    }

    private static void ConsumeNewLine(string source, ref int index)
    {
        if (index < source.Length && source[index] == '\r' && Peek(source, index, 1) == '\n') index++;
        if (index < source.Length) index++;
    }

    private static bool Matches(string source, int index, string value) =>
        index + value.Length <= source.Length && source.AsSpan(index, value.Length).SequenceEqual(value);

    private static char Peek(string source, int index, int offset = 0) =>
        index + offset < source.Length ? source[index + offset] : '\0';

    private static bool IsIdentifierStart(char character) =>
        character == '_' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        character == '_' || char.IsLetterOrDigit(character);
}
