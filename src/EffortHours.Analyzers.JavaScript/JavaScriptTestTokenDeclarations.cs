namespace EffortHours.Analyzers.JavaScript;

// TypeScript/fallback syntax has no compiler binding model. Admit static imports
// and test-file globals, and conservatively discard names shadowed anywhere.
internal static class JavaScriptTestTokenDeclarations
{
    public static void Analyze(JavaScriptTokenization tokens, bool testFile, JavaScriptSourceMetrics metrics)
    {
        Dictionary<string, JavaScriptTestDeclarations.Api> bindings = new(StringComparer.Ordinal);
        HashSet<int> imports = [];
        Dictionary<int, int> pairs = Pairs(tokens);
        for (int index = 0; index < tokens.Tokens.Count; index++)
        {
            if (!tokens.Is(index, "import") || tokens.Is(index + 1, "(")) continue;
            int end = index + 1;
            while (end < tokens.Tokens.Count && end < index + 256 &&
                tokens.Tokens[end].Kind != JavaScriptTokenKind.String && !tokens.Is(end, ";")) end++;
            if (end == tokens.Tokens.Count || tokens.Tokens[end].Kind != JavaScriptTokenKind.String) continue;
            bool framework = !tokens.Is(index + 1, "type") && JavaScriptTestDeclarations.IsFramework(StringValue(tokens, end));
            for (int part = index; part <= end; part++) imports.Add(part);
            for (int part = index + 1; part < end; part++)
            {
                if (!tokens.IsIdentifier(part) || tokens.Is(part, "from") || tokens.Is(part, "type")) continue;
                bool typeOnly = tokens.Is(part - 1, "type");
                bool primary = part == index + 1;
                string imported = Value(tokens, part);
                string local = imported;
                if (tokens.Is(part + 1, "as")) { local = Value(tokens, part + 2); part += 2; }
                JavaScriptTestDeclarations.Api api = framework
                    ? JavaScriptTestDeclarations.NameApi(imported) : JavaScriptTestDeclarations.Api.Unknown;
                if (tokens.Is(part - 1, "as") && tokens.Is(part - 2, "*"))
                    api = framework ? JavaScriptTestDeclarations.Api.Namespace : JavaScriptTestDeclarations.Api.Unknown;
                if (primary) api = framework && StringValue(tokens, end) is "ava" or "node:test"
                    ? JavaScriptTestDeclarations.Api.Case : JavaScriptTestDeclarations.Api.Unknown;
                if (typeOnly) api = JavaScriptTestDeclarations.Api.Unknown;
                bindings[local] = api;
            }
        }

        HashSet<string> shadowed = new(StringComparer.Ordinal);
        for (int index = 0; index < tokens.Tokens.Count; index++)
        {
            if (imports.Contains(index)) continue;
            if (tokens.Is(index, "const") || tokens.Is(index, "let") || tokens.Is(index, "var") ||
                tokens.Is(index, "function") || tokens.Is(index, "class"))
            {
                int next = index + 1;
                if (tokens.Is(next, "*")) next++;
                if (tokens.IsIdentifier(next)) shadowed.Add(Value(tokens, next));
                else if (tokens.Is(next, "{") && pairs.TryGetValue(next, out int end))
                    AddIdentifiers(tokens, next, end, shadowed);
            }
            if (tokens.IsIdentifier(index) && tokens.Is(index + 1, "=>")) shadowed.Add(Value(tokens, index));
            if (tokens.Is(index, "(") && pairs.TryGetValue(index, out int close) &&
                IsParameterList(tokens, index, close)) AddIdentifiers(tokens, index, close, shadowed);
            if (tokens.IsIdentifier(index) && (tokens.Is(index + 1, "=") ||
                tokens.Is(index + 1, "++") || tokens.Is(index + 1, "--"))) shadowed.Add(Value(tokens, index));
        }

        for (int index = 0; index < tokens.Tokens.Count; index++)
        {
            if (imports.Contains(index) || !tokens.IsIdentifier(index) ||
                tokens.Is(index - 1, ".") || tokens.Is(index - 1, "?.")) continue;
            string name = Value(tokens, index);
            if (shadowed.Contains(name)) continue;
            JavaScriptTestDeclarations.Api api = bindings.TryGetValue(name, out var bound)
                ? bound : testFile ? JavaScriptTestDeclarations.NameApi(name) : JavaScriptTestDeclarations.Api.Unknown;
            int end = index + 1;
            bool each = false;
            for (int depth = 0; depth < 12 && tokens.Is(end, ".") && tokens.IsIdentifier(end + 1); depth++)
            {
                string member = Value(tokens, end + 1);
                if (api == JavaScriptTestDeclarations.Api.Namespace) api = JavaScriptTestDeclarations.NameApi(member);
                else if (api is JavaScriptTestDeclarations.Api.Case or JavaScriptTestDeclarations.Api.Suite &&
                    JavaScriptTestDeclarations.IsModifier(member)) each = member == "each";
                else if (api == JavaScriptTestDeclarations.Api.Case && member == "describe")
                    api = JavaScriptTestDeclarations.Api.Suite;
                else if (api == JavaScriptTestDeclarations.Api.Mock && member is "fn" or "spyOn" or "mock" or "stub") { }
                else { api = JavaScriptTestDeclarations.Api.Unknown; break; }
                end += 2;
            }
            if (each)
            {
                if (tokens.Is(end, "(") && pairs.TryGetValue(end, out int close)) end = close + 1;
                else if (end < tokens.Tokens.Count && tokens.Tokens[end].Kind == JavaScriptTokenKind.Template) end++;
                else continue;
            }
            if (tokens.Is(end, "(")) JavaScriptTestDeclarations.Count(api, tokens.Tokens[index].Line, metrics);
        }
    }

    private static bool IsParameterList(JavaScriptTokenization tokens, int start, int close)
    {
        if (tokens.Is(close + 1, "=>") || tokens.Is(close + 1, "{") || tokens.Is(start - 1, "catch")) return true;
        if (!tokens.Is(close + 1, ":")) return false;
        // Typed function/arrow return annotations sit between the parameters and body.
        // A bounded conservative scan avoids resolving TypeScript type expressions.
        for (int index = close + 2; index < tokens.Tokens.Count && index < close + 256; index++)
        {
            if (tokens.Is(index, "{") || tokens.Is(index, "=>")) return true;
            if (tokens.Is(index, ";") || tokens.Is(index, "=")) break;
        }
        return false;
    }

    private static Dictionary<int, int> Pairs(JavaScriptTokenization tokens)
    {
        Dictionary<int, int> result = [];
        Stack<int> stack = new();
        for (int index = 0; index < tokens.Tokens.Count; index++)
        {
            if (tokens.Is(index, "(") || tokens.Is(index, "[") || tokens.Is(index, "{")) stack.Push(index);
            else if ((tokens.Is(index, ")") || tokens.Is(index, "]") || tokens.Is(index, "}")) && stack.Count > 0)
            {
                int opening = stack.Pop();
                if (tokens.Is(opening, "(") && tokens.Is(index, ")") ||
                    tokens.Is(opening, "[") && tokens.Is(index, "]") ||
                    tokens.Is(opening, "{") && tokens.Is(index, "}")) result[opening] = index;
            }
        }
        return result;
    }

    private static void AddIdentifiers(JavaScriptTokenization tokens, int start, int end, HashSet<string> names)
    {
        for (int index = start + 1; index < end; index++)
            if (tokens.IsIdentifier(index)) names.Add(Value(tokens, index));
    }

    private static string Value(JavaScriptTokenization tokens, int index) => tokens.Tokens[index].Span(tokens.Source).ToString();
    private static string StringValue(JavaScriptTokenization tokens, int index) => Value(tokens, index)[1..^1];
}
