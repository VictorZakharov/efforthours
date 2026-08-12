namespace EffortHours.Analyzers.Java;

internal static class KotlinTokenUtilities
{
    public static int FindMatching(
        IReadOnlyList<KotlinToken> tokens,
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

    public static string QualifiedName(IReadOnlyList<KotlinToken> tokens, int start)
    {
        if (start >= tokens.Count || tokens[start].Kind != KotlinTokenKind.Identifier) return string.Empty;
        List<string> parts = [tokens[start].Text];
        int index = start + 1;
        while (index + 1 < tokens.Count && tokens[index].Text == "." &&
            IsQualifiedNamePart(tokens[index + 1]))
        {
            parts.Add(tokens[index + 1].Text);
            index += 2;
        }

        return string.Join('.', parts);
    }

    public static int QualifiedNameLength(IReadOnlyList<KotlinToken> tokens, int start)
    {
        int index = start + 1;
        while (index + 1 < tokens.Count && tokens[index].Text == "." &&
            IsQualifiedNamePart(tokens[index + 1]))
            index += 2;
        return index - start;
    }

    private static bool IsQualifiedNamePart(KotlinToken token) =>
        token.Kind == KotlinTokenKind.Identifier || token.Text is "*" or "get" or "set";

    public static bool HasModifierBefore(
        IReadOnlyList<KotlinToken> tokens,
        int index,
        string modifier,
        int limit = 20)
    {
        for (int cursor = index - 1, seen = 0; cursor >= 0 && seen < limit; cursor--, seen++)
        {
            string text = tokens[cursor].Text;
            if (text is ";" or "{" or "}" || tokens[cursor].Line < tokens[index].Line - 2) return false;
            if (text == modifier) return true;
        }

        return false;
    }

    public static bool IsRestrictedVisibility(IReadOnlyList<KotlinToken> tokens, int index) =>
        HasModifierBefore(tokens, index, "private") ||
        HasModifierBefore(tokens, index, "internal") ||
        HasModifierBefore(tokens, index, "protected");

    public static bool IsWithin(int index, IEnumerable<(int Start, int End)> ranges) =>
        ranges.Any(range => index > range.Start && index < range.End);
}
