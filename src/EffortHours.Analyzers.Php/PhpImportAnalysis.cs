namespace EffortHours.Analyzers.Php;

internal sealed class PhpImportContext
{
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ImportsSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);

    public string ResolveType(string name)
    {
        string candidate = name.TrimStart('\\');
        if (candidate.Length == 0) return string.Empty;
        int separator = candidate.IndexOf('\\');
        string root = separator < 0 ? candidate : candidate[..separator];
        string suffix = separator < 0 ? string.Empty : candidate[separator..];
        if (Aliases.TryGetValue(root, out string? imported)) return imported + suffix;
        return name.StartsWith('\\') ? candidate : string.Empty;
    }

    public string ResolveCall(string call, IReadOnlyDictionary<string, string> instances)
    {
        int staticSeparator = call.IndexOf("::", StringComparison.Ordinal);
        if (staticSeparator >= 0)
        {
            string owner = call[..staticSeparator];
            string resolved = ResolveType(owner);
            return resolved.Length > 0 ? resolved + call[staticSeparator..] : string.Empty;
        }

        int memberSeparator = call.IndexOf("->", StringComparison.Ordinal);
        if (memberSeparator >= 0)
        {
            string variable = call[..memberSeparator];
            return instances.TryGetValue(variable, out string? type)
                ? type + call[memberSeparator..]
                : string.Empty;
        }

        return call;
    }
}

internal static class PhpImportAnalysis
{
    public static PhpImportContext Read(IReadOnlyList<PhpToken> tokens)
    {
        PhpImportContext context = new();
        int braceDepth = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            string text = tokens[index].Text;
            if (text == "{") braceDepth++;
            else if (text == "}") braceDepth = Math.Max(0, braceDepth - 1);
            if (!text.Equals("use", StringComparison.OrdinalIgnoreCase) || braceDepth > 1 ||
                index + 1 >= tokens.Count || tokens[index + 1].Text == "(") continue;

            int cursor = index + 1;
            if (cursor < tokens.Count && tokens[cursor].Text.Equals("function", StringComparison.OrdinalIgnoreCase))
                cursor++;
            else if (cursor < tokens.Count && tokens[cursor].Text.Equals("const", StringComparison.OrdinalIgnoreCase))
                cursor++;
            while (cursor < tokens.Count && tokens[cursor].Text != ";")
            {
                if (tokens[cursor].Kind != PhpTokenKind.Identifier)
                {
                    cursor++;
                    continue;
                }

                string imported = tokens[cursor++].Text.TrimStart('\\');
                if (imported.Length == 0) continue;
                string alias = SimpleName(imported);
                if (cursor + 1 < tokens.Count &&
                    tokens[cursor].Text.Equals("as", StringComparison.OrdinalIgnoreCase) &&
                    tokens[cursor + 1].Kind == PhpTokenKind.Identifier)
                {
                    alias = tokens[cursor + 1].Text;
                    cursor += 2;
                }

                context.ImportsSeen.Add(imported);
                context.Aliases[alias] = imported;
                string? technology = PhpTechnologyCatalog.FromQualifiedName(imported);
                if (technology is not null) context.Technologies.Add(technology);
                while (cursor < tokens.Count && tokens[cursor].Text is not ("," or ";")) cursor++;
                if (cursor < tokens.Count && tokens[cursor].Text == ",") cursor++;
            }

            index = Math.Max(index, cursor);
        }

        return context;
    }

    private static string SimpleName(string name)
    {
        int separator = name.LastIndexOf('\\');
        return separator < 0 ? name : name[(separator + 1)..];
    }
}
