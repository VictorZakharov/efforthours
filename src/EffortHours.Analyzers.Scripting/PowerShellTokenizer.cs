namespace EffortHours.Analyzers.Scripting;

internal static class PowerShellTokenizer
{
    private const int MaximumTokens = 300_000;

    private static readonly string[] Operators =
    [
        "&&", "||", "::", "..", "??", "?.", "?[]", "@(", "@{", "$(", "${",
        ";", "|", "&", ">", "<", "(", ")", "[", "]", "{", "}", ",", "=", ":",
    ];

    public static ScriptTokenizationResult Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<ScriptToken> tokens = [];
        int index = 0;
        int line = 1;
        bool complete = true;
        bool tokenStart = true;

        while (index < source.Length && tokens.Count < MaximumTokens && complete)
        {
            char current = source[index];
            if (current is ' ' or '\t' or '\f')
            {
                index++;
                tokenStart = true;
                continue;
            }

            if (current == '`' && IsNewLine(source, index + 1))
            {
                index++;
                ConsumeNewLine(source, ref index, ref line);
                tokenStart = true;
                continue;
            }

            if (IsNewLine(source, index))
            {
                tokens.Add(new ScriptToken(ScriptTokenKind.NewLine, "\n", line));
                ConsumeNewLine(source, ref index, ref line);
                tokenStart = true;
                continue;
            }

            if (current == '#' && tokenStart)
            {
                while (index < source.Length && !IsNewLine(source, index)) index++;
                continue;
            }

            if (current == '<' && Peek(source, index, 1) == '#')
            {
                index += 2;
                while (index + 1 < source.Length &&
                    !(source[index] == '#' && source[index + 1] == '>'))
                {
                    if (IsNewLine(source, index)) ConsumeNewLine(source, ref index, ref line);
                    else index++;
                }
                complete = index + 1 < source.Length;
                if (complete) index += 2;
                tokenStart = true;
                continue;
            }

            if (current == '@' && Peek(source, index, 1) is '\'' or '"' && IsHereStringStart(source, index))
            {
                int startLine = line;
                complete = ReadHereString(source, ref index, ref line, out string value);
                if (complete) tokens.Add(new ScriptToken(ScriptTokenKind.HereDocument, value, startLine));
                tokenStart = false;
                continue;
            }

            if (current is '\'' or '"')
            {
                int startLine = line;
                complete = ReadQuoted(source, ref index, ref line, current, out string value);
                if (complete) tokens.Add(new ScriptToken(ScriptTokenKind.String, value, startLine));
                tokenStart = false;
                continue;
            }

            if (current == '$')
            {
                int variableStart = index++;
                if (index < source.Length && source[index] == '{')
                {
                    index++;
                    while (index < source.Length && source[index] != '}') index++;
                    complete = index < source.Length;
                    if (complete) index++;
                }
                else
                {
                    while (index < source.Length &&
                        (source[index] == '_' || char.IsLetterOrDigit(source[index]) ||
                         source[index] is ':' or '?' or '^' or '$')) index++;
                }
                tokens.Add(new ScriptToken(ScriptTokenKind.Variable, source[variableStart..index], line));
                tokenStart = false;
                continue;
            }

            string? scriptOperator = Operators.FirstOrDefault(candidate =>
                Matches(source, index, candidate));
            if (scriptOperator is not null)
            {
                tokens.Add(new ScriptToken(ScriptTokenKind.Operator, scriptOperator, line));
                index += scriptOperator.Length;
                tokenStart = true;
                continue;
            }

            int wordStart = index;
            while (index < source.Length &&
                !char.IsWhiteSpace(source[index]) &&
                source[index] is not ('\'' or '"' or '$' or '#' or ';' or '|' or '&' or '<' or '>' or '(' or ')' or '[' or ']' or '{' or '}' or ',' or '=' or ':'))
            {
                index++;
            }

            if (index == wordStart)
            {
                index++;
                continue;
            }

            tokens.Add(new ScriptToken(ScriptTokenKind.Word, source[wordStart..index], line));
            tokenStart = false;
        }

        return new ScriptTokenizationResult(
            tokens,
            complete,
            tokens.Count >= MaximumTokens);
    }

    private static bool ReadQuoted(
        string source,
        ref int index,
        ref int line,
        char quote,
        out string value)
    {
        int start = index++;
        while (index < source.Length)
        {
            if (quote == '"' && source[index] == '`' && index + 1 < source.Length)
            {
                index += 2;
            }
            else if (source[index] == quote)
            {
                if (quote == '\'' && Peek(source, index, 1) == '\'')
                {
                    index += 2;
                    continue;
                }
                index++;
                value = source[start..index];
                return true;
            }
            else if (IsNewLine(source, index))
            {
                ConsumeNewLine(source, ref index, ref line);
            }
            else
            {
                index++;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool ReadHereString(
        string source,
        ref int index,
        ref int line,
        out string value)
    {
        int start = index;
        char quote = source[index + 1];
        index += 2;
        if (index < source.Length && IsNewLine(source, index))
            ConsumeNewLine(source, ref index, ref line);
        while (index < source.Length)
        {
            int lineStart = index;
            while (index < source.Length && source[index] is ' ' or '\t') index++;
            if (index + 1 < source.Length && source[index] == quote && source[index + 1] == '@')
            {
                index += 2;
                value = source[start..index];
                return true;
            }
            index = lineStart;
            while (index < source.Length && !IsNewLine(source, index)) index++;
            if (index < source.Length) ConsumeNewLine(source, ref index, ref line);
        }

        value = string.Empty;
        return false;
    }

    private static bool IsHereStringStart(string source, int index)
    {
        int probe = index + 2;
        while (probe < source.Length && source[probe] is ' ' or '\t') probe++;
        return probe >= source.Length || IsNewLine(source, probe);
    }

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';

    private static bool IsNewLine(string source, int index) =>
        index < source.Length && source[index] is '\r' or '\n';

    private static void ConsumeNewLine(string source, ref int index, ref int line)
    {
        if (source[index] == '\r' && Peek(source, index, 1) == '\n') index += 2;
        else index++;
        line++;
    }
}
