namespace EffortHours.Analyzers.Php;

internal static class PhpSyntaxAnalyzer
{
    public static PhpSyntaxAnalysis Analyze(string source, string path, PhpPackageModel package)
    {
        PhpTokenizationResult tokenization = PhpTokenizer.Tokenize(source);
        IReadOnlyList<PhpToken> tokens = tokenization.Tokens;
        PhpImportContext imports = PhpImportAnalysis.Read(tokens);
        PhpSourceMetrics metrics = new()
        {
            Imports = imports.ImportsSeen.Count,
            DocumentationComments = tokenization.DocumentationComments,
            EntryPoints = IsEntryPath(path, package) ? 1 : 0,
        };
        AnalyzeStructure(tokens, metrics);
        PhpSemanticAnalyzer.Analyze(tokens, imports, package, path, metrics);
        string confidence = tokenization.Truncated || !tokenization.StructurallyBalanced
            ? "low"
            : metrics.DynamicIncludes + metrics.ReflectionUsages + metrics.MagicMethods > 0
                ? "medium"
                : "medium";
        return new PhpSyntaxAnalysis(confidence, imports, metrics, tokens);
    }

    private static void AnalyzeStructure(IReadOnlyList<PhpToken> tokens, PhpSourceMetrics metrics)
    {
        Stack<int> typeDepths = [];
        int braceDepth = 0;
        bool pendingType = false;
        for (int index = 0; index < tokens.Count; index++)
        {
            string text = tokens[index].Text;
            string lower = text.ToLowerInvariant();
            if (text == "{")
            {
                braceDepth++;
                if (pendingType)
                {
                    typeDepths.Push(braceDepth);
                    pendingType = false;
                }
                continue;
            }

            if (text == "}")
            {
                if (typeDepths.Count > 0 && typeDepths.Peek() == braceDepth) typeDepths.Pop();
                braceDepth = Math.Max(0, braceDepth - 1);
                continue;
            }

            if (lower == "namespace" && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == PhpTokenKind.Identifier)
                metrics.Namespace = tokens[index + 1].Text.TrimStart('\\');

            if (lower is "class" or "interface" or "trait" or "enum")
            {
                bool anonymous = lower == "class" && index > 0 &&
                    tokens[index - 1].Text.Equals("new", StringComparison.OrdinalIgnoreCase);
                if (!anonymous)
                {
                    metrics.Types++;
                    metrics.PublicSymbols++;
                    if (lower == "class") metrics.Classes++;
                    else if (lower == "interface") metrics.Interfaces++;
                    else if (lower == "trait") metrics.Traits++;
                    else metrics.Enums++;
                    pendingType = true;
                }
            }

            if (lower is "extends" or "implements")
            {
                int cursor = index + 1;
                while (cursor < tokens.Count && tokens[cursor].Text != "{")
                {
                    if (tokens[cursor].Kind == PhpTokenKind.Identifier)
                        metrics.BaseTypesSeen.Add(tokens[cursor].Text);
                    cursor++;
                }
            }

            if (lower == "function")
            {
                int nameIndex = index + 1;
                if (nameIndex < tokens.Count && tokens[nameIndex].Text == "&") nameIndex++;
                if (nameIndex < tokens.Count && tokens[nameIndex].Kind == PhpTokenKind.Identifier)
                {
                    string name = tokens[nameIndex].Text;
                    bool method = typeDepths.Count > 0;
                    if (method) metrics.Methods++;
                    else metrics.Functions++;
                    if (!method || !HasVisibility(tokens, index, "private", "protected")) metrics.PublicSymbols++;
                    if (name.StartsWith("__", StringComparison.Ordinal)) metrics.MagicMethods++;
                    if (name.StartsWith("test", StringComparison.OrdinalIgnoreCase)) metrics.TestCases++;
                }
            }

            if (text == "#[")
            {
                metrics.Attributes++;
                int attribute = NextIdentifier(tokens, index + 1);
                if (attribute >= 0) metrics.AttributesSeen.Add(tokens[attribute].Text);
            }

            if (lower is "if" or "elseif" or "for" or "foreach" or "while" or "case" or
                "catch" or "match" || text is "&&" or "||" or "??" or "?")
                metrics.BranchPoints++;
            if (lower is "throw" or "catch") metrics.ExceptionPaths++;
            if (lower == "yield") metrics.AsyncUnits++;
            if (lower is "include" or "include_once" or "require" or "require_once")
            {
                int next = index + 1;
                if (next < tokens.Count && tokens[next].Text == "(") next++;
                if (next >= tokens.Count || tokens[next].Kind != PhpTokenKind.String) metrics.DynamicIncludes++;
            }
            if (lower.StartsWith("reflection", StringComparison.Ordinal) ||
                lower is "call_user_func" or "call_user_func_array") metrics.ReflectionUsages++;
            if (tokens[index].Kind == PhpTokenKind.Variable && index + 1 < tokens.Count &&
                tokens[index + 1].Text == "(") metrics.ReflectionUsages++;
        }
    }

    private static bool HasVisibility(
        IReadOnlyList<PhpToken> tokens,
        int functionIndex,
        params string[] candidates)
    {
        for (int index = functionIndex - 1; index >= 0; index--)
        {
            string text = tokens[index].Text;
            if (text is ";" or "{" or "}") break;
            if (candidates.Contains(text, StringComparer.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int NextIdentifier(IReadOnlyList<PhpToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count && tokens[index].Text != "]"; index++)
            if (tokens[index].Kind == PhpTokenKind.Identifier) return index;
        return -1;
    }

    private static bool IsEntryPath(string path, PhpPackageModel package)
    {
        string lower = path.ToLowerInvariant();
        string name = Path.GetFileName(lower);
        return name is "index.php" or "console.php" or "worker.php" ||
            lower.StartsWith("bin/", StringComparison.Ordinal) ||
            lower.Contains("/bin/", StringComparison.Ordinal) ||
            package.BinEntries > 0 && package.AutoloadRoots.Any(root => PhpPath.IsWithin(path, root));
    }
}
