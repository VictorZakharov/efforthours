namespace EffortHours.Analyzers.Go;

internal static class GoSyntaxAnalyzer
{
    public static GoSyntaxAnalysis Analyze(string source, string path, string modulePath)
    {
        GoTokenization tokenization = GoTokenizer.Tokenize(source);
        GoSourceMetrics metrics = new();
        IReadOnlyList<GoToken> tokens = tokenization.Tokens;
        ReadPackage(tokens, metrics);
        Dictionary<string, string> aliases = GoImportAnalysis.Read(tokens, metrics);
        metrics.InternalImports = metrics.ImportsSeen.Count(imported =>
            modulePath.Length > 0 &&
            (imported == modulePath || imported.StartsWith(modulePath + "/", StringComparison.Ordinal)));
        ReadDirectives(tokenization.Directives, metrics);
        metrics.PlatformFiles = GoPlatformFile.IsPlatformSpecific(path) ? 1 : 0;
        ReadDeclarations(tokens, path, metrics);
        ReadControlFlow(tokens, metrics);
        GoSemanticAnalyzer.Analyze(tokens, aliases, path, metrics);
        return new GoSyntaxAnalysis(tokenization.Confidence, metrics);
    }

    private static void ReadPackage(IReadOnlyList<GoToken> tokens, GoSourceMetrics metrics)
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Text == "package" && tokens[index + 1].Kind == GoTokenKind.Identifier)
            {
                metrics.PackageName = tokens[index + 1].Text;
                return;
            }
        }
    }

    private static void ReadDirectives(
        IReadOnlyList<GoDirective> directives,
        GoSourceMetrics metrics)
    {
        foreach (GoDirective directive in directives)
        {
            if (directive.Text.StartsWith("//go:build", StringComparison.Ordinal) ||
                directive.Text.StartsWith("// +build", StringComparison.Ordinal))
                metrics.BuildConstraints++;
            else if (directive.Text.StartsWith("//go:embed", StringComparison.Ordinal))
                metrics.EmbedDirectives++;
            else if (directive.Text.StartsWith("//go:generate", StringComparison.Ordinal))
                metrics.CodeGenerationDirectives++;
        }
    }

    private static void ReadDeclarations(
        IReadOnlyList<GoToken> tokens,
        string path,
        GoSourceMetrics metrics)
    {
        bool testFile = path.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "func")
            {
                ReadFunction(tokens, ref index, metrics, testFile);
            }
            else if (tokens[index].Text == "type")
            {
                ReadType(tokens, ref index, metrics);
            }
            else if (tokens[index].Text is "var" or "const")
            {
                ReadExportedValue(tokens, index, metrics);
            }
        }

        if (testFile &&
            metrics.TableDrivenCases == 0 &&
            tokens.Any(token => token.Text == "range") && HasQualifiedName(tokens, "t.Run"))
            metrics.TableDrivenCases = 1;
    }

    private static void ReadFunction(
        IReadOnlyList<GoToken> tokens,
        ref int index,
        GoSourceMetrics metrics,
        bool testFile)
    {
        int cursor = index + 1;
        bool method = cursor < tokens.Count && tokens[cursor].Text == "(";
        if (method)
        {
            int close = GoTokenUtilities.FindMatching(tokens, cursor, "(", ")");
            if (close < 0) return;
            cursor = close + 1;
        }

        if (cursor >= tokens.Count || tokens[cursor].Kind != GoTokenKind.Identifier) return;
        string name = tokens[cursor].Text;
        if (method) metrics.Methods++;
        else metrics.Functions++;
        if (GoTokenUtilities.IsExported(name)) metrics.PublicSymbols++;
        if (cursor + 1 < tokens.Count && tokens[cursor + 1].Text == "[")
            metrics.GenericDeclarations++;
        if (!method && metrics.PackageName == "main" && name == "main") metrics.EntryPoints++;
        if (testFile && name.StartsWith("Test", StringComparison.Ordinal)) metrics.TestCases++;
        if (testFile && name.StartsWith("Benchmark", StringComparison.Ordinal)) metrics.Benchmarks++;
        if (testFile && name.StartsWith("Example", StringComparison.Ordinal)) metrics.Examples++;
        if (testFile && name.StartsWith("Fuzz", StringComparison.Ordinal)) metrics.FuzzTests++;
        index = cursor;
    }

    private static void ReadType(
        IReadOnlyList<GoToken> tokens,
        ref int index,
        GoSourceMetrics metrics)
    {
        int cursor = index + 1;
        if (cursor >= tokens.Count || tokens[cursor].Kind != GoTokenKind.Identifier) return;
        string name = tokens[cursor].Text;
        metrics.Types++;
        if (GoTokenUtilities.IsExported(name)) metrics.PublicSymbols++;
        cursor++;
        if (cursor < tokens.Count && tokens[cursor].Text == "[")
        {
            metrics.GenericDeclarations++;
            int close = GoTokenUtilities.FindMatching(tokens, cursor, "[", "]");
            if (close > 0) cursor = close + 1;
        }

        if (cursor < tokens.Count && tokens[cursor].Text == "interface") metrics.Interfaces++;
        index = cursor;
    }

    private static void ReadExportedValue(
        IReadOnlyList<GoToken> tokens,
        int index,
        GoSourceMetrics metrics)
    {
        int cursor = index + 1;
        if (cursor < tokens.Count && tokens[cursor].Kind == GoTokenKind.Identifier &&
            GoTokenUtilities.IsExported(tokens[cursor].Text)) metrics.PublicSymbols++;
    }

    private static void ReadControlFlow(IReadOnlyList<GoToken> tokens, GoSourceMetrics metrics)
    {
        foreach (GoToken token in tokens)
        {
            if (token.Text is "if" or "for" or "switch" or "select" or "case" or "&&" or "||")
                metrics.BranchPoints++;
            if (token.Text == "go")
            {
                metrics.Goroutines++;
                metrics.AsyncUnits++;
            }
            if (token.Text is "chan" or "<-") metrics.ChannelUsages++;
            if (token.Text is "panic" or "recover") metrics.ErrorPaths++;
        }

        for (int index = 0; index + 2 < tokens.Count; index++)
        {
            if (tokens[index].Text == "err" && tokens[index + 1].Text == "!=" &&
                tokens[index + 2].Text == "nil") metrics.ErrorPaths++;
        }
    }

    private static bool HasQualifiedName(IReadOnlyList<GoToken> tokens, string name)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            if (GoTokenUtilities.QualifiedName(tokens, index) == name) return true;
        }

        return false;
    }
}
