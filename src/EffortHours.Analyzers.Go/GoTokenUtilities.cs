namespace EffortHours.Analyzers.Go;

internal static class GoTokenUtilities
{
    public static int FindMatching(
        IReadOnlyList<GoToken> tokens,
        int start,
        string open,
        string close)
    {
        int depth = 0;
        for (int index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Text == open) depth++;
            else if (tokens[index].Text == close && --depth == 0) return index;
        }

        return -1;
    }

    public static string QualifiedName(IReadOnlyList<GoToken> tokens, int start)
    {
        if (start >= tokens.Count || tokens[start].Kind != GoTokenKind.Identifier) return string.Empty;
        List<string> parts = [tokens[start].Text];
        int index = start + 1;
        while (index + 1 < tokens.Count && tokens[index].Text == "." &&
            tokens[index + 1].Kind == GoTokenKind.Identifier)
        {
            parts.Add(tokens[index + 1].Text);
            index += 2;
        }

        return string.Join('.', parts);
    }

    public static int QualifiedNameLength(IReadOnlyList<GoToken> tokens, int start)
    {
        int index = start + 1;
        while (index + 1 < tokens.Count && tokens[index].Text == "." &&
            tokens[index + 1].Kind == GoTokenKind.Identifier) index += 2;
        return index - start;
    }

    public static string StringValue(string literal)
    {
        if (literal.Length < 2) return string.Empty;
        if (literal[0] == '`' && literal[^1] == '`') return literal[1..^1];
        if (literal[0] != '"' || literal[^1] != '"') return string.Empty;
        string value = literal[1..^1];
        return value.Contains('\\')
            ? value.Replace("\\/", "/", StringComparison.Ordinal)
            : value;
    }

    public static bool IsExported(string name) =>
        name.Length > 0 && char.IsUpper(name[0]);
}
