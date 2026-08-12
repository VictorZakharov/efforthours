namespace EffortHours.Analyzers.Cpp;

internal static class CppBuildSyntax
{
    public static IEnumerable<IReadOnlyList<string>> Calls(string source, string name)
    {
        int offset = 0;
        while (offset < source.Length)
        {
            int start = FindCallStart(source, name, offset);
            if (start < 0) yield break;
            int cursor = start + name.Length;
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor])) cursor++;
            if (cursor >= source.Length || source[cursor] != '(')
            {
                offset = start + name.Length;
                continue;
            }
            int end = FindClosing(source, cursor);
            if (end < 0) yield break;
            yield return SplitArguments(source[(cursor + 1)..end]);
            offset = end + 1;
        }
    }

    public static IReadOnlyList<string> SplitArguments(string value)
    {
        List<string> arguments = [];
        int start = -1;
        char quote = '\0';
        for (int index = 0; index <= value.Length; index++)
        {
            char current = index < value.Length ? value[index] : ' ';
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || value[index - 1] != '\\')) quote = '\0';
                continue;
            }
            if (current is '"' or '\'')
            {
                quote = current;
                if (start < 0) start = index;
            }
            else if (char.IsWhiteSpace(current) || current == ',')
            {
                if (start >= 0)
                {
                    arguments.Add(value[start..index].Trim().Trim('"', '\''));
                    start = -1;
                }
            }
            else if (start < 0) start = index;
        }
        return arguments;
    }

    public static bool IsLiteral(string value) => value.Length > 0 &&
        !value.Contains('$') && !value.Contains('*') && !value.Contains('?') &&
        !value.Contains('<') && !value.Contains('>') && !value.Contains('(') && !value.Contains(')');

    private static int FindClosing(string source, int opening)
    {
        int depth = 0;
        char quote = '\0';
        for (int index = opening; index < source.Length; index++)
        {
            char current = source[index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || source[index - 1] != '\\')) quote = '\0';
                continue;
            }
            if (TrySkipBracketArgument(source, ref index)) continue;
            if (current is '"' or '\'') quote = current;
            else if (current == '#')
            {
                int newline = source.IndexOf('\n', index + 1);
                if (newline < 0) return -1;
                index = newline;
            }
            else if (current == '(') depth++;
            else if (current == ')' && --depth == 0) return index;
        }
        return -1;
    }

    private static int FindCallStart(string source, string name, int offset)
    {
        for (int index = offset; index + name.Length <= source.Length; index++)
        {
            char current = source[index];
            if (current == '#')
            {
                int newline = source.IndexOf('\n', index + 1);
                if (newline < 0) return -1;
                index = newline;
                continue;
            }
            if (TrySkipBracketArgument(source, ref index)) continue;
            if (current is '"' or '\'')
            {
                SkipQuoted(source, ref index, current);
                continue;
            }
            if ((index > 0 && IsName(source[index - 1])) ||
                !source.AsSpan(index, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            int after = index + name.Length;
            if (after < source.Length && IsName(source[after])) continue;
            int cursor = after;
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor])) cursor++;
            if (cursor < source.Length && source[cursor] == '(') return index;
        }
        return -1;
    }

    private static void SkipQuoted(string source, ref int index, char quote)
    {
        for (index++; index < source.Length; index++)
        {
            if (source[index] == '\\') index++;
            else if (source[index] == quote) return;
        }
    }

    private static bool TrySkipBracketArgument(string source, ref int index)
    {
        if (source[index] != '[') return false;
        int cursor = index + 1;
        while (cursor < source.Length && source[cursor] == '=') cursor++;
        if (cursor >= source.Length || source[cursor] != '[') return false;
        string closing = "]" + new string('=', cursor - index - 1) + "]";
        int end = source.IndexOf(closing, cursor + 1, StringComparison.Ordinal);
        index = end < 0 ? source.Length : end + closing.Length - 1;
        return true;
    }

    private static bool IsName(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';
}
