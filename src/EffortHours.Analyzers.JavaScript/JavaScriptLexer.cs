namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptLexer
{
    public static JavaScriptTokenization Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<JavaScriptToken> tokens = new(Math.Min(source.Length / 6, 32_768));
        bool hasHashBang = source.StartsWith("#!", StringComparison.Ordinal);
        int index = 0;
        int line = 1;
        if (hasHashBang)
        {
            SkipLine(source, ref index);
        }

        while (index < source.Length)
        {
            char current = source[index];
            if (current is ' ' or '\t' or '\v' or '\f')
            {
                index++;
                continue;
            }

            if (current is '\r' or '\n')
            {
                ConsumeLineBreak(source, ref index);
                line++;
                continue;
            }

            if (current == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    SkipLine(source, ref index);
                    continue;
                }

                if (source[index + 1] == '*')
                {
                    SkipBlockComment(source, ref index, ref line);
                    continue;
                }

                if (CanStartRegularExpression(source, tokens))
                {
                    SkipRegularExpression(source, ref index, ref line);
                    continue;
                }
            }

            int tokenLine = line;
            if (current is '\'' or '"')
            {
                int start = index;
                SkipQuotedString(source, ref index, ref line, current);
                tokens.Add(new JavaScriptToken(
                    JavaScriptTokenKind.String,
                    start,
                    index - start,
                    tokenLine));
                continue;
            }

            if (current == '`')
            {
                int start = index;
                SkipTemplate(source, ref index, ref line);
                tokens.Add(new JavaScriptToken(
                    JavaScriptTokenKind.Template,
                    start,
                    index - start,
                    tokenLine));
                continue;
            }

            if (IsIdentifierStart(current))
            {
                int start = index++;
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                tokens.Add(new JavaScriptToken(
                    JavaScriptTokenKind.Identifier,
                    start,
                    index - start,
                    tokenLine));
                continue;
            }

            if (char.IsAsciiDigit(current) ||
                (current == '.' && index + 1 < source.Length && char.IsAsciiDigit(source[index + 1])))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_' or '+' or '-'))
                {
                    index++;
                }

                tokens.Add(new JavaScriptToken(
                    JavaScriptTokenKind.Number,
                    start,
                    index - start,
                    tokenLine));
                continue;
            }

            int punctuatorLength = GetPunctuatorLength(source, index);
            tokens.Add(new JavaScriptToken(
                JavaScriptTokenKind.Punctuator,
                index,
                punctuatorLength,
                tokenLine));
            index += punctuatorLength;
        }

        return new JavaScriptTokenization(source, tokens, hasHashBang);
    }

    private static bool CanStartRegularExpression(
        string source,
        List<JavaScriptToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return true;
        }

        JavaScriptToken previous = tokens[^1];
        ReadOnlySpan<char> value = previous.Span(source);
        if (previous.Kind == JavaScriptTokenKind.Punctuator)
        {
            return IsAny(value,
                "(", "[", "{", ",", ";", ":", "=", "!", "?", "&&", "||", "??",
                "=>", "+", "-", "*", "%", "&", "|", "^", "~", "<", ">");
        }

        return previous.Kind == JavaScriptTokenKind.Identifier && IsAny(
            value,
            "return", "throw", "case", "delete", "void", "typeof", "new", "in",
            "instanceof", "yield", "await", "else", "do");
    }

    private static void SkipQuotedString(
        string source,
        ref int index,
        ref int line,
        char quote)
    {
        index++;
        while (index < source.Length)
        {
            char current = source[index++];
            if (current == '\\' && index < source.Length)
            {
                if (source[index] is '\r' or '\n')
                {
                    ConsumeLineBreak(source, ref index);
                    line++;
                }
                else
                {
                    index++;
                }

                continue;
            }

            if (current == quote)
            {
                return;
            }

            if (current == '\r')
            {
                if (index < source.Length && source[index] == '\n')
                {
                    index++;
                }

                line++;
            }
            else if (current == '\n')
            {
                line++;
            }
        }
    }

    private static void SkipTemplate(string source, ref int index, ref int line)
    {
        index++;
        while (index < source.Length)
        {
            char current = source[index++];
            if (current == '\\' && index < source.Length)
            {
                index++;
                continue;
            }

            if (current == '`')
            {
                return;
            }

            if (current == '\r')
            {
                if (index < source.Length && source[index] == '\n')
                {
                    index++;
                }

                line++;
            }
            else if (current == '\n')
            {
                line++;
            }
        }
    }

    private static void SkipRegularExpression(string source, ref int index, ref int line)
    {
        index++;
        bool inCharacterClass = false;
        while (index < source.Length)
        {
            char current = source[index++];
            if (current == '\\' && index < source.Length)
            {
                index++;
                continue;
            }

            if (current is '\r' or '\n')
            {
                line++;
                return;
            }

            if (current == '[')
            {
                inCharacterClass = true;
            }
            else if (current == ']')
            {
                inCharacterClass = false;
            }
            else if (current == '/' && !inCharacterClass)
            {
                while (index < source.Length && IsIdentifierPart(source[index]))
                {
                    index++;
                }

                return;
            }
        }
    }

    private static void SkipLine(string source, ref int index)
    {
        while (index < source.Length && source[index] is not ('\r' or '\n'))
        {
            index++;
        }
    }

    private static void SkipBlockComment(string source, ref int index, ref int line)
    {
        index += 2;
        while (index < source.Length)
        {
            if (source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
            {
                index += 2;
                return;
            }

            if (source[index] is '\r' or '\n')
            {
                ConsumeLineBreak(source, ref index);
                line++;
            }
            else
            {
                index++;
            }
        }
    }

    private static void ConsumeLineBreak(string source, ref int index)
    {
        if (source[index++] == '\r' && index < source.Length && source[index] == '\n')
        {
            index++;
        }
    }

    private static int GetPunctuatorLength(string source, int index)
    {
        int remaining = source.Length - index;
        if (remaining >= 4 && source.AsSpan(index, 4).SequenceEqual(">>>="))
        {
            return 4;
        }

        if (remaining >= 3 && IsAny(
            source.AsSpan(index, 3),
            "===", "!==", ">>>", "**=", "&&=", "||=", "??=", "..."))
        {
            return 3;
        }

        if (remaining >= 2 && IsAny(
            source.AsSpan(index, 2),
            "=>", "?.", "&&", "||", "??", "==", "!=", "<=", ">=", "++", "--",
            "**", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<", ">>"))
        {
            return 2;
        }

        return 1;
    }

    private static bool IsIdentifierStart(char value) =>
        value is '_' or '$' || char.IsLetter(value) || value > 127;

    private static bool IsIdentifierPart(char value) =>
        IsIdentifierStart(value) || char.IsDigit(value) || value is '\u200c' or '\u200d';

    private static bool IsAny(ReadOnlySpan<char> value, params ReadOnlySpan<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            if (value.SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed record JavaScriptTokenization(
    string Source,
    IReadOnlyList<JavaScriptToken> Tokens,
    bool HasHashBang)
{
    public bool Is(int index, string value) => index >= 0 && index < Tokens.Count &&
        Tokens[index].Span(Source).SequenceEqual(value);

    public bool IsIdentifier(int index) => index >= 0 && index < Tokens.Count &&
        Tokens[index].Kind == JavaScriptTokenKind.Identifier;

    public string? ReadModuleSpecifier(int index)
    {
        if (index < 0 || index >= Tokens.Count || Tokens[index].Kind != JavaScriptTokenKind.String)
        {
            return null;
        }

        ReadOnlySpan<char> span = Tokens[index].Span(Source);
        if (span.Length < 2 || span[1..^1].Contains('\\'))
        {
            return null;
        }

        string value = span[1..^1].ToString();
        if (value.Length == 0 || value.StartsWith('.') || value.StartsWith('/') || value.Contains(':'))
        {
            return null;
        }

        string[] segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return value.StartsWith('@') && segments.Length >= 2
            ? $"{segments[0]}/{segments[1]}"
            : segments.FirstOrDefault();
    }
}

internal readonly record struct JavaScriptToken(
    JavaScriptTokenKind Kind,
    int Start,
    int Length,
    int Line)
{
    public ReadOnlySpan<char> Span(string source) => source.AsSpan(Start, Length);
}

internal enum JavaScriptTokenKind
{
    Identifier,
    String,
    Template,
    Number,
    Punctuator,
}
