namespace EffortHours.Analyzers.Php;

internal static class PhpTokenizer
{
    public const int MaximumTokens = 250_000;
    private const int MaximumHeredocDelimiterLength = 128;

    public static PhpTokenizationResult Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<PhpToken> tokens = [];
        Stack<char> delimiters = [];
        bool hasOpenTag = source.Contains("<?", StringComparison.Ordinal);
        bool inPhp = !hasOpenTag;
        bool complete = true;
        bool truncated = false;
        int documentationComments = 0;
        int inlineHtmlRegions = 0;
        int phpRegions = inPhp ? 1 : 0;
        int line = 1;
        int index = 0;
        while (index < source.Length)
        {
            if (tokens.Count >= MaximumTokens)
            {
                truncated = true;
                break;
            }

            if (!inPhp)
            {
                int open = source.IndexOf("<?", index, StringComparison.Ordinal);
                int end = open < 0 ? source.Length : open;
                if (source.AsSpan(index, end - index).ContainsAnyExcept(" \t\r\n")) inlineHtmlRegions++;
                CountLines(source, index, end, ref line);
                if (open < 0) break;
                index = SkipOpenTag(source, open);
                inPhp = true;
                phpRegions++;
                continue;
            }

            if (source.AsSpan(index).StartsWith("?>", StringComparison.Ordinal))
            {
                index += 2;
                inPhp = false;
                continue;
            }

            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (char.IsWhiteSpace(current))
            {
                if (current == '\n') line++;
                else if (current == '\r')
                {
                    line++;
                    if (next == '\n') index++;
                }

                index++;
                continue;
            }

            if (current == '/' && next == '/' || current == '#' && next != '[')
            {
                index += current == '#' ? 1 : 2;
                while (index < source.Length && source[index] is not ('\r' or '\n')) index++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                if (index + 2 < source.Length && source[index + 2] == '*') documentationComments++;
                index += 2;
                bool closed = false;
                while (index + 1 < source.Length)
                {
                    if (source[index] == '\n') line++;
                    else if (source[index] == '\r')
                    {
                        line++;
                        if (source[index + 1] == '\n') index++;
                    }

                    if (source[index] == '*' && source[index + 1] == '/')
                    {
                        index += 2;
                        closed = true;
                        break;
                    }

                    index++;
                }

                if (!closed)
                {
                    complete = false;
                    break;
                }

                continue;
            }

            int tokenLine = line;
            if (current is '\'' or '"' or '`')
            {
                if (!ReadQuoted(source, ref index, ref line, current)) complete = false;
                tokens.Add(new PhpToken(PhpTokenKind.String, "<string>", tokenLine));
                if (!complete) break;
                continue;
            }

            if (current == '<' && source.AsSpan(index).StartsWith("<<<", StringComparison.Ordinal))
            {
                if (!ReadHeredoc(source, ref index, ref line)) complete = false;
                tokens.Add(new PhpToken(PhpTokenKind.String, "<heredoc>", tokenLine));
                if (!complete) break;
                continue;
            }

            if (current == '$' && index + 1 < source.Length && IsIdentifierStart(source[index + 1]))
            {
                int start = index++;
                ReadIdentifier(source, ref index);
                tokens.Add(new PhpToken(PhpTokenKind.Variable, source[start..index], tokenLine));
                continue;
            }

            if (IsIdentifierStart(current))
            {
                int start = index;
                ReadIdentifier(source, ref index);
                tokens.Add(new PhpToken(PhpTokenKind.Identifier, source[start..index], tokenLine));
                continue;
            }

            if (char.IsAsciiDigit(current))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_')) index++;
                tokens.Add(new PhpToken(PhpTokenKind.Number, source[start..index], tokenLine));
                continue;
            }

            string symbol = ReadSymbol(source, ref index);
            if (!UpdateDelimiters(symbol, delimiters)) complete = false;
            tokens.Add(new PhpToken(PhpTokenKind.Symbol, symbol, tokenLine));
            if (!complete) break;
        }

        return new PhpTokenizationResult(
            tokens,
            truncated,
            complete && delimiters.Count == 0,
            documentationComments,
            inlineHtmlRegions,
            phpRegions);
    }

    private static int SkipOpenTag(string source, int index)
    {
        if (source.AsSpan(index).StartsWith("<?php", StringComparison.OrdinalIgnoreCase)) return index + 5;
        return index + 2;
    }

    private static void ReadIdentifier(string source, ref int index)
    {
        index++;
        while (index < source.Length && IsIdentifierPart(source[index])) index++;
    }

    private static bool ReadQuoted(
        string source,
        ref int index,
        ref int line,
        char quote)
    {
        index++;
        bool escaped = false;
        while (index < source.Length)
        {
            char current = source[index++];
            if (current == '\n') line++;
            else if (current == '\r')
            {
                line++;
                if (index < source.Length && source[index] == '\n') index++;
            }

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\') escaped = true;
            else if (current == quote) return true;
        }

        return false;
    }

    private static bool ReadHeredoc(string source, ref int index, ref int line)
    {
        int cursor = index + 3;
        while (cursor < source.Length && source[cursor] is ' ' or '\t') cursor++;
        char quote = cursor < source.Length && source[cursor] is '\'' or '"' ? source[cursor++] : '\0';
        int delimiterStart = cursor;
        while (cursor < source.Length &&
            (char.IsAsciiLetterOrDigit(source[cursor]) || source[cursor] == '_')) cursor++;
        int length = cursor - delimiterStart;
        if (length is <= 0 or > MaximumHeredocDelimiterLength) return false;
        string delimiter = source[delimiterStart..cursor];
        if (quote != '\0' && (cursor >= source.Length || source[cursor++] != quote)) return false;
        while (cursor < source.Length && source[cursor] is ' ' or '\t') cursor++;
        if (cursor >= source.Length || source[cursor] is not ('\r' or '\n')) return false;
        SkipNewLine(source, ref cursor, ref line);
        while (cursor <= source.Length)
        {
            int lineStart = cursor;
            int lineEnd = cursor;
            while (lineEnd < source.Length && source[lineEnd] is not ('\r' or '\n')) lineEnd++;
            string candidate = source[lineStart..lineEnd].TrimStart(' ', '\t');
            if (candidate.Equals(delimiter, StringComparison.Ordinal) ||
                candidate.Equals(delimiter + ";", StringComparison.Ordinal))
            {
                index = lineEnd;
                return true;
            }

            if (lineEnd >= source.Length) break;
            cursor = lineEnd;
            SkipNewLine(source, ref cursor, ref line);
        }

        index = source.Length;
        return false;
    }

    private static void SkipNewLine(string source, ref int index, ref int line)
    {
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index += 2;
        else index++;
        line++;
    }

    private static string ReadSymbol(string source, ref int index)
    {
        ReadOnlySpan<string> symbols =
        [
            "?->", "??=", "===", "!==", "<=>", "...", "**=", "<<=", ">>=",
            "#[", "->", "::", "=>", "??", "==", "!=", "<=", ">=", "&&", "||",
            "++", "--", "+=", "-=", "*=", "/=", ".=", "%=", "**", "<<", ">>",
        ];
        foreach (string candidate in symbols)
        {
            if (source.AsSpan(index).StartsWith(candidate, StringComparison.Ordinal))
            {
                index += candidate.Length;
                return candidate;
            }
        }

        return source[index++].ToString();
    }

    private static bool UpdateDelimiters(string symbol, Stack<char> delimiters)
    {
        char current = symbol == "#[" ? '[' : symbol[0];
        if (current is '(' or '[' or '{')
        {
            delimiters.Push(current);
            return true;
        }

        if (current is not (')' or ']' or '}')) return true;
        if (delimiters.Count == 0) return false;
        char expected = current switch { ')' => '(', ']' => '[', _ => '{' };
        return delimiters.Pop() == expected;
    }

    private static bool IsIdentifierStart(char value) =>
        value == '\\' || value == '_' || char.IsLetter(value) || value >= '\u0080';

    private static bool IsIdentifierPart(char value) =>
        value == '\\' || value == '_' || char.IsLetterOrDigit(value) || value >= '\u0080';

    private static void CountLines(string source, int start, int end, ref int line)
    {
        for (int index = start; index < end; index++)
        {
            if (source[index] == '\n') line++;
            else if (source[index] == '\r')
            {
                line++;
                if (index + 1 < end && source[index + 1] == '\n') index++;
            }
        }
    }
}
