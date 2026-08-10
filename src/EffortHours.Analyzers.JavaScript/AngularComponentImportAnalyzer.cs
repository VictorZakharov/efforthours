namespace EffortHours.Analyzers.JavaScript;

internal static class AngularComponentImportAnalyzer
{
    public static HashSet<string> FindDecoratorNames(JavaScriptTokenization tokens)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        for (int importIndex = 0; importIndex + 1 < tokens.Tokens.Count; importIndex++)
        {
            if (!tokens.Is(importIndex, "import") ||
                tokens.Is(importIndex + 1, "(") ||
                tokens.Is(importIndex + 1, "type"))
            {
                continue;
            }

            int fromIndex = FindImportSource(tokens, importIndex);
            if (fromIndex < 0 || !IsAngularCoreModule(tokens, fromIndex + 1))
            {
                continue;
            }

            int openingBrace = FindOpeningBrace(tokens, importIndex, fromIndex);
            int closingBrace = FindClosingBrace(tokens, openingBrace);
            if (openingBrace < 0 || closingBrace < 0 || closingBrace > fromIndex)
            {
                continue;
            }

            ReadNamedComponentImport(tokens, openingBrace, closingBrace, names);
            importIndex = fromIndex + 1;
        }

        return names;
    }

    private static int FindImportSource(JavaScriptTokenization tokens, int importIndex)
    {
        int depth = 0;
        int limit = Math.Min(tokens.Tokens.Count - 1, importIndex + 128);
        for (int index = importIndex + 1; index < limit; index++)
        {
            if (tokens.Is(index, "{") || tokens.Is(index, "[") || tokens.Is(index, "("))
            {
                depth++;
            }
            else if (tokens.Is(index, "}") || tokens.Is(index, "]") || tokens.Is(index, ")"))
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0 && tokens.Is(index, "from") &&
                index + 1 < tokens.Tokens.Count &&
                tokens.Tokens[index + 1].Kind == JavaScriptTokenKind.String)
            {
                return index;
            }
            else if (depth == 0 && (tokens.Is(index, ";") || tokens.Is(index, "import")))
            {
                break;
            }
        }

        return -1;
    }

    private static int FindOpeningBrace(
        JavaScriptTokenization tokens,
        int importIndex,
        int fromIndex)
    {
        for (int index = importIndex + 1; index < fromIndex; index++)
        {
            if (tokens.Is(index, "{"))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindClosingBrace(JavaScriptTokenization tokens, int openingBrace)
    {
        if (openingBrace < 0)
        {
            return -1;
        }

        int depth = 0;
        for (int index = openingBrace; index < tokens.Tokens.Count; index++)
        {
            if (tokens.Is(index, "{"))
            {
                depth++;
            }
            else if (tokens.Is(index, "}") && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static void ReadNamedComponentImport(
        JavaScriptTokenization tokens,
        int openingBrace,
        int closingBrace,
        HashSet<string> names)
    {
        int index = openingBrace + 1;
        while (index < closingBrace)
        {
            while (index < closingBrace && tokens.Is(index, ","))
            {
                index++;
            }

            bool typeOnly = index < closingBrace && tokens.Is(index, "type");
            if (typeOnly)
            {
                index++;
            }

            if (index >= closingBrace ||
                tokens.Tokens[index].Kind != JavaScriptTokenKind.Identifier)
            {
                index++;
                continue;
            }

            string importedName = tokens.Tokens[index].Span(tokens.Source).ToString();
            string localName = importedName;
            if (index + 2 < closingBrace && tokens.Is(index + 1, "as") &&
                tokens.Tokens[index + 2].Kind == JavaScriptTokenKind.Identifier)
            {
                localName = tokens.Tokens[index + 2].Span(tokens.Source).ToString();
                index += 2;
            }

            if (!typeOnly && importedName == "Component")
            {
                names.Add(localName);
            }

            while (index < closingBrace && !tokens.Is(index, ","))
            {
                index++;
            }
        }
    }

    private static bool IsAngularCoreModule(JavaScriptTokenization tokens, int index)
    {
        if (index < 0 || index >= tokens.Tokens.Count ||
            tokens.Tokens[index].Kind != JavaScriptTokenKind.String)
        {
            return false;
        }

        ReadOnlySpan<char> literal = tokens.Tokens[index].Span(tokens.Source);
        return literal.Length >= 2 &&
            literal[0] == literal[^1] &&
            literal[0] is '\'' or '"' &&
            literal[1..^1].Equals("@angular/core", StringComparison.Ordinal);
    }
}
