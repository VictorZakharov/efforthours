namespace EffortHours.Analyzers.Sql;

internal enum SqlTokenKind
{
    Word,
    QuotedIdentifier,
    StringLiteral,
    Number,
    Symbol,
}

internal readonly record struct SqlToken(SqlTokenKind Kind, string Value, int Line);

internal sealed record SqlTokenizationResult
{
    public IReadOnlyList<SqlToken> Tokens { get; init; } = [];

    public required bool Truncated { get; init; }

    public required bool UnterminatedConstruct { get; init; }

    public required int BracketIdentifierCount { get; init; }

    public required int BacktickIdentifierCount { get; init; }

    public required int DollarQuotedStringCount { get; init; }
}

internal static class SqlTokenizer
{
    public const int MaximumTokens = 200_000;
    private const int MaximumTokenLength = 128;
    private const int MaximumDollarTagLength = 64;

    public static SqlTokenizationResult Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<SqlToken> tokens = [];
        int line = 1;
        int brackets = 0;
        int backticks = 0;
        int dollarQuotes = 0;
        bool unterminated = false;
        bool truncated = false;

        for (int index = 0; index < text.Length;)
        {
            char current = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (char.IsWhiteSpace(current))
            {
                if (current == '\n')
                {
                    line++;
                }

                index++;
                continue;
            }

            if (current == '-' && next == '-')
            {
                index += 2;
                while (index < text.Length && text[index] is not ('\r' or '\n'))
                {
                    index++;
                }

                continue;
            }

            if (current == '/' && next == '*')
            {
                int depth = 1;
                index += 2;
                while (index < text.Length && depth > 0)
                {
                    if (text[index] == '\n')
                    {
                        line++;
                        index++;
                    }
                    else if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
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

                unterminated |= depth > 0;
                continue;
            }

            int tokenLine = line;
            if (current == '\'')
            {
                index = ReadQuoted(text, index, '\'', '\'', ref line, out bool closed);
                unterminated |= !closed;
                if (!Add(tokens, new SqlToken(SqlTokenKind.StringLiteral, "STRING", tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (current == '"')
            {
                index = ReadQuoted(text, index, '"', '"', ref line, out bool closed);
                unterminated |= !closed;
                if (!Add(tokens, new SqlToken(SqlTokenKind.QuotedIdentifier, "IDENTIFIER", tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (current == '[')
            {
                index = ReadQuoted(text, index, '[', ']', ref line, out bool closed);
                brackets++;
                unterminated |= !closed;
                if (!Add(tokens, new SqlToken(SqlTokenKind.QuotedIdentifier, "IDENTIFIER", tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (current == '`')
            {
                index = ReadQuoted(text, index, '`', '`', ref line, out bool closed);
                backticks++;
                unterminated |= !closed;
                if (!Add(tokens, new SqlToken(SqlTokenKind.QuotedIdentifier, "IDENTIFIER", tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (current == '$' && TryReadDollarQuoted(
                text,
                index,
                ref line,
                out int afterDollarQuote,
                out bool dollarClosed))
            {
                dollarQuotes++;
                unterminated |= !dollarClosed;
                index = afterDollarQuote;
                if (!Add(tokens, new SqlToken(SqlTokenKind.StringLiteral, "DOLLAR_STRING", tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (IsWordStart(current))
            {
                int start = index++;
                while (index < text.Length && IsWordPart(text[index]))
                {
                    index++;
                }

                int length = Math.Min(index - start, MaximumTokenLength);
                string value = text.Substring(start, length).ToUpperInvariant();
                if (!Add(tokens, new SqlToken(SqlTokenKind.Word, value, tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (char.IsAsciiDigit(current))
            {
                index++;
                while (index < text.Length &&
                    (char.IsAsciiDigit(text[index]) || text[index] is '.' or '_' ||
                     char.IsAsciiHexDigit(text[index]) && index > 1 && text[index - 1] is 'x' or 'X'))
                {
                    index++;
                }

                if (!Add(tokens, new SqlToken(SqlTokenKind.Number, "NUMBER", tokenLine)))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            string symbol = ReadSymbol(text, ref index);
            if (!Add(tokens, new SqlToken(SqlTokenKind.Symbol, symbol, tokenLine)))
            {
                truncated = true;
                break;
            }
        }

        return new SqlTokenizationResult
        {
            Tokens = tokens,
            Truncated = truncated,
            UnterminatedConstruct = unterminated,
            BracketIdentifierCount = brackets,
            BacktickIdentifierCount = backticks,
            DollarQuotedStringCount = dollarQuotes,
        };
    }

    private static bool Add(List<SqlToken> tokens, SqlToken token)
    {
        if (tokens.Count >= MaximumTokens)
        {
            return false;
        }

        tokens.Add(token);
        return true;
    }

    private static int ReadQuoted(
        string text,
        int index,
        char opening,
        char closing,
        ref int line,
        out bool closed)
    {
        _ = opening;
        index++;
        closed = false;
        while (index < text.Length)
        {
            char current = text[index++];
            if (current == '\n')
            {
                line++;
            }

            if (current != closing)
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

        return index;
    }

    private static bool TryReadDollarQuoted(
        string text,
        int index,
        ref int line,
        out int nextIndex,
        out bool closed)
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
            closed = false;
            return false;
        }

        string delimiter = text[index..(delimiterEnd + 1)];
        int contentStart = delimiterEnd + 1;
        int closing = text.IndexOf(delimiter, contentStart, StringComparison.Ordinal);
        if (closing < 0)
        {
            line += CountLines(text.AsSpan(contentStart));
            nextIndex = text.Length;
            closed = false;
            return true;
        }

        line += CountLines(text.AsSpan(contentStart, closing - contentStart));
        nextIndex = closing + delimiter.Length;
        closed = true;
        return true;
    }

    private static string ReadSymbol(string text, ref int index)
    {
        char first = text[index++];
        if (index >= text.Length)
        {
            return first.ToString();
        }

        char second = text[index];
        if ((first, second) is
            (':', ':') or (':', '=') or ('-', '>') or ('=', '>') or
            ('<', '=') or ('>', '=') or ('<', '>') or ('!', '=') or
            ('|', '|') or ('&', '&'))
        {
            index++;
            if (first == '-' && second == '>' && index < text.Length && text[index] == '>')
            {
                index++;
                return "->>";
            }

            return string.Concat(first, second);
        }

        return first.ToString();
    }

    private static bool IsWordStart(char value) =>
        char.IsAsciiLetter(value) || value is '_' or '@' or '#';

    private static bool IsWordPart(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '_' or '$' or '@' or '#';

    private static int CountLines(ReadOnlySpan<char> value)
    {
        int count = 0;
        foreach (char character in value)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }
}
