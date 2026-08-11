namespace EffortHours.Analyzers.Java;

internal static class JavaTokenUtilities
{
    public static int FindMatching(
        IReadOnlyList<JavaToken> tokens,
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

    public static string QualifiedName(IReadOnlyList<JavaToken> tokens, int start)
    {
        if (start >= tokens.Count || tokens[start].Kind != JavaTokenKind.Identifier) return string.Empty;
        List<string> parts = [tokens[start].Text];
        int index = start + 1;
        while (index + 1 < tokens.Count && tokens[index].Text == "." &&
            (tokens[index + 1].Kind == JavaTokenKind.Identifier || tokens[index + 1].Text == "*"))
        {
            parts.Add(tokens[index + 1].Text);
            index += 2;
        }

        return string.Join('.', parts);
    }

    public static int QualifiedNameLength(IReadOnlyList<JavaToken> tokens, int start)
    {
        int index = start + 1;
        while (index + 1 < tokens.Count && tokens[index].Text == "." &&
            (tokens[index + 1].Kind == JavaTokenKind.Identifier || tokens[index + 1].Text == "*"))
            index += 2;
        return index - start;
    }

    public static bool HasModifierBefore(
        IReadOnlyList<JavaToken> tokens,
        int index,
        string modifier,
        int limit = 16)
    {
        for (int cursor = index - 1, seen = 0; cursor >= 0 && seen < limit; cursor--, seen++)
        {
            string text = tokens[cursor].Text;
            if (text is ";" or "{" or "}") return false;
            if (text == modifier) return true;
        }

        return false;
    }
}
