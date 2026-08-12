using System.Text;

namespace EffortHours.Analyzers.Terraform;

internal static class HclTokenizer
{
    public const int MaximumTokens = 250_000;
    private const int MaximumValueLength = 512;
    private const int MaximumHeredocDelimiterLength = 128;

    public static HclTokenizationResult Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<HclToken> tokens = [];
        int line = 1;
        int comments = 0;
        int heredocs = 0;
        bool unterminated = false;
        bool truncated = false;

        for (int index = 0; index < text.Length;)
        {
            char current = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (current is '\r' or '\n')
            {
                if (current == '\r' && next == '\n') index++;
                if (!Add(tokens, new HclToken(HclTokenKind.NewLine, "\n", line)))
                {
                    truncated = true;
                    break;
                }

                line++;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '#' || current == '/' && next == '/')
            {
                comments++;
                index += current == '#' ? 1 : 2;
                while (index < text.Length && text[index] is not ('\r' or '\n')) index++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                comments++;
                index += 2;
                bool closed = false;
                while (index < text.Length)
                {
                    if (text[index] == '\n') line++;
                    if (index + 1 < text.Length && text[index] == '*' && text[index + 1] == '/')
                    {
                        index += 2;
                        closed = true;
                        break;
                    }

                    index++;
                }

                unterminated |= !closed;
                continue;
            }

            int tokenLine = line;
            if (current == '"')
            {
                index = ReadQuotedString(
                    text,
                    index,
                    ref line,
                    out string value,
                    out bool template,
                    out bool closed);
                unterminated |= !closed;
                if (!Add(tokens, new HclToken(HclTokenKind.String, value, tokenLine, template)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (current == '<' && next == '<' && TryReadHeredoc(
                text,
                index,
                ref line,
                out int afterHeredoc,
                out bool heredocTemplate,
                out bool heredocClosed))
            {
                heredocs++;
                unterminated |= !heredocClosed;
                index = afterHeredoc;
                if (!Add(tokens, new HclToken(
                    HclTokenKind.String,
                    "HEREDOC",
                    tokenLine,
                    heredocTemplate)))
                {
                    truncated = true;
                    break;
                }

                if (!Add(tokens, new HclToken(HclTokenKind.NewLine, "\n", line)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (IsIdentifierStart(current))
            {
                int start = index++;
                while (index < text.Length && IsIdentifierPart(text[index])) index++;
                string value = Bounded(text.AsSpan(start, index - start));
                if (!Add(tokens, new HclToken(HclTokenKind.Identifier, value, tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (char.IsAsciiDigit(current) || current == '.' && char.IsAsciiDigit(next))
            {
                int start = index++;
                while (index < text.Length &&
                    (char.IsAsciiLetterOrDigit(text[index]) || text[index] is '.' or '_' or '+' or '-'))
                {
                    index++;
                }

                if (!Add(tokens, new HclToken(
                    HclTokenKind.Number,
                    Bounded(text.AsSpan(start, index - start)),
                    tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            string symbol = ReadSymbol(text, ref index);
            if (!Add(tokens, new HclToken(HclTokenKind.Symbol, symbol, tokenLine)))
            {
                truncated = true;
                break;
            }
        }

        return new HclTokenizationResult
        {
            Tokens = tokens,
            Truncated = truncated,
            UnterminatedConstruct = unterminated,
            CommentCount = comments,
            HeredocCount = heredocs,
        };
    }

    private static int ReadQuotedString(
        string text,
        int index,
        ref int line,
        out string value,
        out bool template,
        out bool closed)
    {
        StringBuilder result = new();
        template = false;
        closed = false;
        index++;
        bool escaped = false;
        while (index < text.Length)
        {
            char current = text[index++];
            if (current == '\n') line++;
            if (escaped)
            {
                if (result.Length < MaximumValueLength) result.Append(current);
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                if (result.Length < MaximumValueLength) result.Append(current);
                continue;
            }

            if (current == '"')
            {
                closed = true;
                break;
            }

            if ((current == '$' || current == '%') && index < text.Length && text[index] == '{')
            {
                template = true;
            }

            if (result.Length < MaximumValueLength) result.Append(current);
        }

        value = result.ToString();
        return index;
    }

    private static bool TryReadHeredoc(
        string text,
        int index,
        ref int line,
        out int nextIndex,
        out bool template,
        out bool closed)
    {
        int cursor = index + 2;
        bool indented = cursor < text.Length && text[cursor] == '-';
        if (indented) cursor++;
        while (cursor < text.Length && text[cursor] is ' ' or '\t') cursor++;
        int delimiterStart = cursor;
        while (cursor < text.Length && text[cursor] is not ('\r' or '\n') &&
            cursor - delimiterStart <= MaximumHeredocDelimiterLength)
        {
            cursor++;
        }

        string delimiter = text[delimiterStart..cursor].Trim().Trim('"');
        if (delimiter.Length == 0 || delimiter.Length > MaximumHeredocDelimiterLength)
        {
            nextIndex = index;
            template = false;
            closed = false;
            return false;
        }

        cursor = SkipLineBreak(text, cursor, ref line);
        template = false;
        closed = false;
        while (cursor < text.Length)
        {
            int lineStart = cursor;
            int lineEnd = cursor;
            while (lineEnd < text.Length && text[lineEnd] is not ('\r' or '\n')) lineEnd++;
            ReadOnlySpan<char> candidate = text.AsSpan(lineStart, lineEnd - lineStart);
            if ((indented ? candidate.TrimStart() : candidate).SequenceEqual(delimiter))
            {
                nextIndex = SkipLineBreak(text, lineEnd, ref line);
                closed = true;
                return true;
            }

            template |= candidate.Contains("${", StringComparison.Ordinal) ||
                candidate.Contains("%{", StringComparison.Ordinal);
            cursor = SkipLineBreak(text, lineEnd, ref line);
        }

        nextIndex = text.Length;
        return true;
    }

    private static int SkipLineBreak(string text, int index, ref int line)
    {
        if (index < text.Length && text[index] == '\r') index++;
        if (index < text.Length && text[index] == '\n') index++;
        line++;
        return index;
    }

    private static string ReadSymbol(string text, ref int index)
    {
        char first = text[index++];
        if (index + 1 < text.Length && first == '.' && text[index] == '.' && text[index + 1] == '.')
        {
            index += 2;
            return "...";
        }

        if (index < text.Length)
        {
            char second = text[index];
            if ((first, second) is
                ('=', '=') or ('!', '=') or ('<', '=') or ('>', '=') or
                ('&', '&') or ('|', '|') or ('=', '>') or (':', ':'))
            {
                index++;
                return string.Concat(first, second);
            }
        }

        return first.ToString();
    }

    private static bool Add(List<HclToken> tokens, HclToken token)
    {
        if (tokens.Count >= MaximumTokens) return false;
        tokens.Add(token);
        return true;
    }

    private static string Bounded(ReadOnlySpan<char> value) =>
        new(value[..Math.Min(value.Length, MaximumValueLength)]);

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '-';
}
