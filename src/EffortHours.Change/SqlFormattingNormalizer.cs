using System.Text;

namespace EffortHours.Change;

internal static class SqlFormattingNormalizer
{
    private const int MaximumDollarTagLength = 64;

    public static bool TryCreateSignature(string text, out string signature)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder result = new(text.Length);
        for (int index = 0; index < text.Length;)
        {
            char current = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            int start = index;
            if (current == '-' && next == '-')
            {
                index += 2;
                while (index < text.Length && text[index] is not ('\r' or '\n'))
                {
                    index++;
                }

                AppendToken(result, text.AsSpan(start, index - start));
                continue;
            }

            if (current == '/' && next == '*')
            {
                index += 2;
                int depth = 1;
                while (index < text.Length && depth > 0)
                {
                    if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
                    {
                        depth++;
                        index += 2;
                    }
                    else if (index + 1 < text.Length && text[index] == '*' && text[index + 1] == '/')
                    {
                        depth--;
                        index += 2;
                    }
                    else
                    {
                        index++;
                    }
                }

                if (depth > 0)
                {
                    signature = string.Empty;
                    return false;
                }

                AppendToken(result, text.AsSpan(start, index - start));
                continue;
            }

            if (current is '\'' or '"' or '`' or '[')
            {
                char closing = current == '[' ? ']' : current;
                index++;
                bool closed = false;
                while (index < text.Length)
                {
                    char character = text[index++];
                    if (character == '\\' && current is '\'' or '"' or '`' && index < text.Length)
                    {
                        index++;
                        continue;
                    }

                    if (character != closing)
                    {
                        continue;
                    }

                    if (index < text.Length && text[index] == closing)
                    {
                        index++;
                        continue;
                    }

                    closed = true;
                    break;
                }

                if (!closed)
                {
                    signature = string.Empty;
                    return false;
                }

                AppendToken(result, text.AsSpan(start, index - start));
                continue;
            }

            if (current == '$' && TryReadDollarQuoted(text, index, out int afterDollar))
            {
                if (afterDollar < 0)
                {
                    signature = string.Empty;
                    return false;
                }

                index = afterDollar;
                AppendToken(result, text.AsSpan(start, index - start));
                continue;
            }

            if (char.IsAsciiLetterOrDigit(current) || current is '_' or '@' or '#')
            {
                index++;
                while (index < text.Length &&
                    (char.IsAsciiLetterOrDigit(text[index]) || text[index] is '_' or '$' or '@' or '#'))
                {
                    index++;
                }

                AppendToken(result, text.AsSpan(start, index - start));
                continue;
            }

            index++;
            if (index < text.Length && IsTwoCharacterSymbol(current, text[index]))
            {
                index++;
                if (current == '-' && index < text.Length && text[index - 1] == '>' && text[index] == '>')
                {
                    index++;
                }
            }

            AppendToken(result, text.AsSpan(start, index - start));
        }

        signature = result.ToString();
        return true;
    }

    private static bool TryReadDollarQuoted(string text, int index, out int nextIndex)
    {
        int delimiterEnd = index + 1;
        while (delimiterEnd < text.Length &&
            delimiterEnd - index - 1 <= MaximumDollarTagLength &&
            (char.IsAsciiLetterOrDigit(text[delimiterEnd]) || text[delimiterEnd] == '_'))
        {
            delimiterEnd++;
        }

        if (delimiterEnd >= text.Length ||
            text[delimiterEnd] != '$' ||
            delimiterEnd - index - 1 > MaximumDollarTagLength)
        {
            nextIndex = index;
            return false;
        }

        string delimiter = text[index..(delimiterEnd + 1)];
        int closing = text.IndexOf(delimiter, delimiterEnd + 1, StringComparison.Ordinal);
        nextIndex = closing < 0 ? -1 : closing + delimiter.Length;
        return true;
    }

    private static void AppendToken(StringBuilder result, ReadOnlySpan<char> token)
    {
        result.Append(token.Length).Append(':').Append(token).Append(';');
    }

    private static bool IsTwoCharacterSymbol(char first, char second) => (first, second) is
        (':', ':') or (':', '=') or ('-', '>') or ('=', '>') or
        ('<', '=') or ('>', '=') or ('<', '>') or ('!', '=') or
        ('|', '|') or ('&', '&');
}
