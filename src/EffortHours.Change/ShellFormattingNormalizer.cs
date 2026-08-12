using System.Text;

namespace EffortHours.Change;

internal static class ShellFormattingNormalizer
{
    private static readonly string[] Operators =
    [
        ";;&", "&&", "||", ";;", ";&", "|&", ">>", "<<", "<&", ">&",
        ";", "|", "&", "<", ">", "(", ")", "{", "}",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        signature = string.Empty;
        if (source.Contains("<<", StringComparison.Ordinal)) return false;

        StringBuilder result = new(source.Length);
        StringBuilder word = new();
        int index = 0;
        bool tokenStart = true;
        bool complete = true;
        while (index < source.Length && complete)
        {
            char current = source[index];
            if (current == '\\' && IsNewLine(source, index + 1))
            {
                FlushWord(result, word);
                index++;
                ConsumeNewLine(source, ref index);
                tokenStart = true;
                continue;
            }
            if (IsNewLine(source, index))
            {
                FlushWord(result, word);
                AppendSeparator(result);
                ConsumeNewLine(source, ref index);
                tokenStart = true;
                continue;
            }
            if (current is ' ' or '\t' or '\f')
            {
                FlushWord(result, word);
                tokenStart = true;
                index++;
                continue;
            }
            if (current == '#' && tokenStart)
            {
                FlushWord(result, word);
                if (index == 0 && Peek(source, index, 1) == '!')
                {
                    int start = index;
                    while (index < source.Length && !IsNewLine(source, index)) index++;
                    AppendToken(result, $"s:{source[start..index].Trim()}");
                    continue;
                }
                while (index < source.Length && !IsNewLine(source, index)) index++;
                continue;
            }
            if (current is '\'' or '"' or '`')
            {
                FlushWord(result, word);
                complete = AppendQuoted(source, ref index, current, result);
                tokenStart = false;
                continue;
            }

            string? scriptOperator = Operators.FirstOrDefault(candidate =>
                Matches(source, index, candidate));
            if (scriptOperator is not null)
            {
                FlushWord(result, word);
                if (scriptOperator == ";") AppendSeparator(result);
                else AppendToken(result, $"o:{scriptOperator}");
                index += scriptOperator.Length;
                tokenStart = true;
                continue;
            }

            word.Append(current);
            tokenStart = false;
            index++;
        }

        FlushWord(result, word);
        TrimSeparators(result);
        signature = result.ToString();
        return complete;
    }

    private static bool AppendQuoted(
        string source,
        ref int index,
        char quote,
        StringBuilder result)
    {
        int start = index++;
        while (index < source.Length)
        {
            if (quote != '\'' && source[index] == '\\' && index + 1 < source.Length)
                index += 2;
            else if (source[index] == quote)
            {
                index++;
                AppendToken(result, $"q:{source[start..index]}");
                return true;
            }
            else index++;
        }
        return false;
    }

    private static void FlushWord(StringBuilder result, StringBuilder word)
    {
        if (word.Length == 0) return;
        AppendToken(result, $"w:{word}");
        word.Clear();
    }

    private static void AppendToken(StringBuilder result, string token)
    {
        if (token is "o:{" or "o:}" && result.Length > 0 && result[^1] == ';')
            result.Length--;
        if (result.Length > 0 && result[^1] != '\u001f') result.Append('\u001f');
        result.Append(token);
    }

    private static void AppendSeparator(StringBuilder result)
    {
        if (result.Length == 0 || result[^1] == ';' || EndsWith(result, "o:{")) return;
        result.Append(';');
    }

    private static void TrimSeparators(StringBuilder result)
    {
        while (result.Length > 0 && result[^1] is ';' or '\u001f') result.Length--;
    }

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static bool EndsWith(StringBuilder value, string suffix) =>
        value.Length >= suffix.Length &&
        value.ToString(value.Length - suffix.Length, suffix.Length) == suffix;

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';

    private static bool IsNewLine(string source, int index) =>
        index < source.Length && source[index] is '\r' or '\n';

    private static void ConsumeNewLine(string source, ref int index)
    {
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
            index += 2;
        else
            index++;
    }
}
