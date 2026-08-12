namespace EffortHours.Analyzers.Scripting;

internal static class ShellTokenizer
{
    private const int MaximumTokens = 300_000;

    private static readonly string[] Operators =
    [
        ";;&", "<<-", "<<<", "&&", "||", ";;", ";&", "|&", ">>", "<<",
        "<>", ">&", "<&", "&>", ">|", "((", "))", "[[", "]]", "$(", "${",
        ";", "|", "&", "<", ">", "(", ")", "{", "}",
    ];

    public static ScriptTokenizationResult Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<ScriptToken> tokens = [];
        List<HereDocumentDelimiter> pendingHereDocuments = [];
        int index = 0;
        int line = 1;
        bool complete = true;
        bool tokenStart = true;
        HereDocumentDelimiter? awaitingDelimiter = null;

        while (index < source.Length && tokens.Count < MaximumTokens && complete)
        {
            char current = source[index];
            if (current is ' ' or '\t' or '\f')
            {
                tokenStart = true;
                index++;
                continue;
            }

            if (current == '\\' && IsNewLine(source, index + 1))
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
                if (pendingHereDocuments.Count > 0)
                {
                    complete = SkipHereDocuments(
                        source,
                        ref index,
                        ref line,
                        pendingHereDocuments,
                        tokens);
                    pendingHereDocuments.Clear();
                }
                continue;
            }

            if (current == '#' && tokenStart)
            {
                while (index < source.Length && !IsNewLine(source, index)) index++;
                continue;
            }

            if (current is '\'' or '"' or '`')
            {
                int startLine = line;
                complete = ReadQuoted(source, ref index, ref line, current, out string value);
                if (complete)
                {
                    ScriptToken token = new(ScriptTokenKind.String, value, startLine);
                    tokens.Add(token);
                    CaptureHereDocumentDelimiter(token, ref awaitingDelimiter, pendingHereDocuments);
                }
                tokenStart = false;
                continue;
            }

            if (current == '$')
            {
                int variableStart = index++;
                if (index < source.Length && source[index] == '{')
                {
                    index++;
                    int depth = 1;
                    while (index < source.Length && depth > 0)
                    {
                        if (source[index] == '{') depth++;
                        else if (source[index] == '}') depth--;
                        index++;
                    }
                    complete = depth == 0;
                }
                else if (index < source.Length && source[index] == '(')
                {
                    index++;
                    if (index < source.Length && source[index] == '(') index++;
                }
                else
                {
                    while (index < source.Length &&
                        (source[index] == '_' || char.IsLetterOrDigit(source[index]) ||
                         source[index] is '@' or '*' or '#' or '?' or '-' or '$' or '!')) index++;
                }

                tokens.Add(new ScriptToken(
                    ScriptTokenKind.Variable,
                    source[variableStart..index],
                    line));
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
                if (scriptOperator is "<<" or "<<-")
                {
                    awaitingDelimiter = new HereDocumentDelimiter(
                        string.Empty,
                        StripTabs: scriptOperator == "<<-");
                }
                continue;
            }

            int wordStart = index;
            while (index < source.Length &&
                !char.IsWhiteSpace(source[index]) &&
                source[index] is not ('\'' or '"' or '`' or '$' or '#' or ';' or '|' or '&' or '<' or '>' or '(' or ')' or '{' or '}'))
            {
                if (source[index] == '\\' && index + 1 < source.Length)
                {
                    index += 2;
                }
                else
                {
                    index++;
                }
            }

            if (index == wordStart)
            {
                index++;
                continue;
            }

            ScriptToken word = new(ScriptTokenKind.Word, source[wordStart..index], line);
            tokens.Add(word);
            CaptureHereDocumentDelimiter(word, ref awaitingDelimiter, pendingHereDocuments);
            tokenStart = false;
        }

        bool truncated = tokens.Count >= MaximumTokens;
        return new ScriptTokenizationResult(tokens, complete && awaitingDelimiter is null, truncated);
    }

    private static void CaptureHereDocumentDelimiter(
        ScriptToken token,
        ref HereDocumentDelimiter? awaiting,
        ICollection<HereDocumentDelimiter> pending)
    {
        if (awaiting is null)
        {
            return;
        }

        string delimiter = token.Text.Trim('\'', '"');
        if (delimiter.Length > 0)
        {
            pending.Add(awaiting with { Value = delimiter });
        }
        awaiting = null;
    }

    private static bool SkipHereDocuments(
        string source,
        ref int index,
        ref int line,
        IReadOnlyList<HereDocumentDelimiter> delimiters,
        List<ScriptToken> tokens)
    {
        foreach (HereDocumentDelimiter delimiter in delimiters)
        {
            bool found = false;
            while (index <= source.Length)
            {
                int start = index;
                while (index < source.Length && !IsNewLine(source, index)) index++;
                string candidate = source[start..index];
                string compared = delimiter.StripTabs ? candidate.TrimStart('\t') : candidate;
                if (compared == delimiter.Value)
                {
                    found = true;
                    if (index < source.Length) ConsumeNewLine(source, ref index, ref line);
                    break;
                }

                if (index >= source.Length) break;
                ConsumeNewLine(source, ref index, ref line);
            }

            if (!found)
            {
                return false;
            }

            tokens.Add(new ScriptToken(ScriptTokenKind.HereDocument, "heredoc", line));
        }

        return true;
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
            if (quote != '\'' && source[index] == '\\' && index + 1 < source.Length)
            {
                index += 2;
            }
            else if (source[index] == quote)
            {
                index++;
                value = source[start..index];
                return true;
            }
            else if (quote == '\'' && IsNewLine(source, index))
            {
                ConsumeNewLine(source, ref index, ref line);
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

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static bool IsNewLine(string source, int index) =>
        index < source.Length && source[index] is '\r' or '\n';

    private static void ConsumeNewLine(string source, ref int index, ref int line)
    {
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
            index += 2;
        else
            index++;
        line++;
    }

    private sealed record HereDocumentDelimiter(string Value, bool StripTabs);
}
