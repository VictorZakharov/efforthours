namespace EffortHours.Analyzers.GDScript;

internal static class GDScriptSyntaxAnalyzer
{
    public static GDScriptSyntaxAnalysis Analyze(string source, bool isTest, CancellationToken cancellationToken)
    {
        GDScriptTokenization parsed = GDScriptTokenizer.Tokenize(source, cancellationToken);
        Dictionary<string, int> metrics = new(StringComparer.Ordinal)
        {
            ["types"] = 1, // Each script is an implicit class, with or without class_name.
        };
        IReadOnlyList<GDScriptToken> tokens = parsed.Tokens;
        for (int index = 0; index < tokens.Count; index++)
        {
            GDScriptToken token = tokens[index];
            if (token.Kind is not (GDScriptTokenKind.Keyword or GDScriptTokenKind.Identifier or GDScriptTokenKind.Operator))
                continue;
            string next = index + 1 < tokens.Count ? tokens[index + 1].Text : string.Empty;
            string following = index + 2 < tokens.Count ? tokens[index + 2].Text : string.Empty;
            if (token.Text == "func")
            {
                if (index + 1 < tokens.Count && tokens[index + 1].Kind == GDScriptTokenKind.Identifier && following == "(")
                {
                    Add("methods");
                    if (!next.StartsWith('_')) Add("public-symbols");
                    if (isTest && next.StartsWith("test_", StringComparison.Ordinal)) Add("test-cases");
                    if (next is "_ready" or "_process" or "_physics_process" or "_input" or "_unhandled_input" or "_init")
                        Add("lifecycle-methods");
                }
                else if (next == "(") Add("functions"); // Anonymous function.
            }
            if (token.Text is "class" or "enum" && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == GDScriptTokenKind.Identifier)
            {
                Add("types");
                if (!next.StartsWith('_')) Add("public-symbols");
            }
            if (token.Text == "class_name" && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == GDScriptTokenKind.Identifier) Add("public-symbols");
            if (token.Text == "signal" && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == GDScriptTokenKind.Identifier)
            {
                Add("signals");
                if (!next.StartsWith('_')) Add("public-symbols");
            }
            if (token.Text is "await" or "yield") Add("async-units");
            if (token.Text is "if" or "elif" or "for" or "while" or "match" or "and" or "or" or "&&" or "||")
                Add("branch-points");
            if (token.Text == "@" && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == GDScriptTokenKind.Identifier)
            {
                Add("annotations");
                if (next == "export" || next.StartsWith("export_", StringComparison.Ordinal)) Add("export-annotations");
                if (next == "rpc") Add("rpc-annotations");
                if (next == "tool") Add("editor-tool-annotations");
            }
            if (isTest && (token.Text == "assert" || token.Text.StartsWith("assert_", StringComparison.Ordinal)) && next == "(")
                Add("assertions");
        }
        return new GDScriptSyntaxAnalysis(metrics, parsed.Confidence);

        void Add(string name) => metrics[name] = metrics.GetValueOrDefault(name) + 1;
    }
}

internal sealed record GDScriptSyntaxAnalysis(IReadOnlyDictionary<string, int> Metrics, string Confidence);
