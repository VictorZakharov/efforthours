namespace EffortHours.Analyzers.Rust;

internal static class RustSyntaxAnalyzer
{
    private static readonly HashSet<string> BuiltInAttributes = new(StringComparer.Ordinal)
    {
        "allow", "bench", "cfg", "cfg_attr", "cold", "deprecated", "derive", "doc",
        "export_name", "inline", "link", "link_name", "link_section", "must_use",
        "no_mangle", "non_exhaustive", "panic_handler", "path", "repr", "should_panic",
        "test", "track_caller", "used",
    };

    public static RustSyntaxAnalysis Analyze(
        string source,
        string path,
        CargoPackageModel package)
    {
        RustTokenization tokenization = RustTokenizer.Tokenize(source);
        RustSourceMetrics metrics = new();
        RustImportContext imports = RustImportAnalysis.Read(tokenization.Tokens, package, metrics);
        ReadAttributes(tokenization.Tokens, metrics);
        ReadDeclarations(tokenization.Tokens, path, metrics);
        ApplyTargetRole(package, path, metrics);
        ReadControlFlow(tokenization.Tokens, metrics);
        ReadDocumentation(tokenization.Tokens, metrics);
        RustSemanticAnalyzer.Analyze(tokenization.Tokens, imports, package, path, metrics);
        return new RustSyntaxAnalysis(tokenization.Confidence, imports.Crates, metrics);
    }

    private static void ReadAttributes(IReadOnlyList<RustToken> tokens, RustSourceMetrics metrics)
    {
        for (int index = 0; index + 2 < tokens.Count; index++)
        {
            if (tokens[index].Text != "#" || tokens[index + 1].Text != "[") continue;
            int end = FindMatching(tokens, index + 1, "[", "]");
            if (end < 0) continue;
            string[] names = [.. tokens.Skip(index + 2).Take(end - index - 2)
                .Where(token => token.Kind is RustTokenKind.Identifier or RustTokenKind.Keyword)
                .Select(token => token.Text)];
            if (names.Length == 0) continue;
            if (names.Contains("feature", StringComparer.Ordinal)) metrics.FeatureGates++;
            if (names[0] == "test" || names.Last() == "test") metrics.TestCases++;
            if (names[0] == "bench") metrics.Benchmarks++;
            if (names[0] == "derive")
                metrics.AttributeMacros += Math.Max(1, names.Length - 1);
            else if (!BuiltInAttributes.Contains(names[0]))
                metrics.AttributeMacros++;
            if (names[0] is "no_mangle" or "export_name" or "link" or "link_name")
                metrics.FfiBoundaries++;
            index = end;
        }
    }

    private static void ReadDeclarations(
        IReadOnlyList<RustToken> tokens,
        string path,
        RustSourceMetrics metrics)
    {
        int braceDepth = 0;
        bool awaitingImpl = false;
        Stack<int> implDepths = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            RustToken token = tokens[index];
            if (token.Text == "{")
            {
                braceDepth++;
                if (awaitingImpl) { implDepths.Push(braceDepth); awaitingImpl = false; }
            }
            else if (token.Text == "}")
            {
                if (implDepths.Count > 0 && implDepths.Peek() == braceDepth) implDepths.Pop();
                braceDepth = Math.Max(0, braceDepth - 1);
            }
            else if (token.Text == "impl")
            {
                metrics.Impls++;
                awaitingImpl = true;
                if (HasGenericNear(tokens, index)) metrics.GenericDeclarations++;
            }
            else if (token.Text == "mod")
            {
                metrics.Modules++;
                if (NextToken(tokens, index + 1, ";") >= 0) metrics.ExternalModules++;
            }
            else if (token.Text is "struct" or "enum" or "trait" or "union" or "type")
            {
                ReadType(tokens, index, token.Text, metrics);
            }
            else if (token.Text == "fn")
            {
                ReadFunction(tokens, index, implDepths.Count > 0, path, metrics);
            }
            else if (token.Text is "const" or "static" && IsPublic(tokens, index))
            {
                metrics.PublicSymbols++;
            }
        }

        metrics.BuildScript = Path.GetFileName(path).Equals("build.rs", StringComparison.OrdinalIgnoreCase);
        if (metrics.BuildScript) metrics.EntryPoints++;
        string relative = RelativeToPackage(path);
        if (relative == "src/main.rs" || relative.StartsWith("src/bin/", StringComparison.Ordinal) ||
            relative.StartsWith("examples/", StringComparison.Ordinal)) metrics.EntryPoints++;
        if (relative.StartsWith("tests/", StringComparison.Ordinal))
        {
            metrics.IntegrationTests = Math.Max(1, metrics.IntegrationTests);
            metrics.TestCases = Math.Max(1, metrics.TestCases);
        }
        if (relative.StartsWith("benches/", StringComparison.Ordinal)) metrics.Benchmarks = Math.Max(1, metrics.Benchmarks);
        if (relative.StartsWith("examples/", StringComparison.Ordinal)) metrics.Examples = Math.Max(1, metrics.Examples);
    }

    private static void ReadType(
        IReadOnlyList<RustToken> tokens,
        int index,
        string kind,
        RustSourceMetrics metrics)
    {
        metrics.Types++;
        if (kind == "struct") metrics.Structs++;
        else if (kind == "enum") metrics.Enums++;
        else if (kind == "trait") metrics.Traits++;
        else if (kind == "union") metrics.Unions++;
        if (IsPublic(tokens, index)) metrics.PublicSymbols++;
        int name = NextIdentifier(tokens, index + 1);
        if (name >= 0 && name + 1 < tokens.Count && tokens[name + 1].Text == "<")
            metrics.GenericDeclarations++;
    }

    private static void ReadFunction(
        IReadOnlyList<RustToken> tokens,
        int index,
        bool method,
        string path,
        RustSourceMetrics metrics)
    {
        if (method) metrics.Methods++;
        else metrics.Functions++;
        if (IsPublic(tokens, index)) metrics.PublicSymbols++;
        int name = NextIdentifier(tokens, index + 1);
        if (name >= 0)
        {
            if (tokens[name].Text == "main") metrics.EntryPoints++;
            if (name + 1 < tokens.Count && tokens[name + 1].Text == "<") metrics.GenericDeclarations++;
            if (IsTestPath(path) && tokens[name].Text.StartsWith("test_", StringComparison.Ordinal))
                metrics.TestCases++;
            if (IsBenchPath(path) && tokens[name].Text.StartsWith("bench", StringComparison.Ordinal))
                metrics.Benchmarks++;
        }
    }

    private static void ReadControlFlow(IReadOnlyList<RustToken> tokens, RustSourceMetrics metrics)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            string value = tokens[index].Text;
            if (value is "if" or "else" or "match" or "while" or "for" or "loop" or "=>" or "&&" or "||")
                metrics.BranchPoints++;
            if (value == "async") metrics.AsyncUnits++;
            if (value == "await") metrics.AwaitPoints++;
            if (value == "unsafe") metrics.UnsafeBlocks++;
            if (tokens[index].Kind == RustTokenKind.Lifetime) metrics.LifetimeUsages++;
            if (value == "?" || value is "Err" or "Result" || value is "panic" or "bail" && IsMacro(tokens, index))
                metrics.ErrorPaths++;
            if (IsMacro(tokens, index))
            {
                metrics.MacroInvocations++;
                if (value is "macro_rules" or "macro") metrics.MacroDefinitions++;
                if (value is "include" or "include_bytes" or "include_str" ||
                    value is "env" or "option_env" && ContainsNearby(tokens, index, "OUT_DIR"))
                    metrics.GeneratedBindingSignals++;
                if (value.StartsWith("assert", StringComparison.Ordinal) || value is "matches" or "debug_assert")
                    metrics.Assertions++;
            }
            if (value == "extern" && index + 1 < tokens.Count &&
                (tokens[index + 1].Kind == RustTokenKind.String || tokens[index + 1].Text == "fn"))
                metrics.FfiBoundaries++;
        }
    }

    private static void ApplyTargetRole(
        CargoPackageModel package,
        string path,
        RustSourceMetrics metrics)
    {
        string relative = package.Directory == "." || !path.StartsWith(package.Directory + "/", StringComparison.Ordinal)
            ? path
            : path[(package.Directory.Length + 1)..];
        foreach (CargoTarget target in package.Targets.Where(target => target.Path == relative))
        {
            if (target.Kind == "test")
            {
                metrics.IsTestTarget = true;
                metrics.IntegrationTests = Math.Max(1, metrics.IntegrationTests);
                metrics.TestCases = Math.Max(1, metrics.TestCases);
            }
            else if (target.Kind == "bench")
            {
                metrics.IsTestTarget = true;
                metrics.Benchmarks = Math.Max(1, metrics.Benchmarks);
            }
            else if (target.Kind == "example")
            {
                metrics.Examples = Math.Max(1, metrics.Examples);
                metrics.EntryPoints = Math.Max(1, metrics.EntryPoints);
            }
            else if (target.Kind is "bin" or "build") metrics.EntryPoints = Math.Max(1, metrics.EntryPoints);
        }
    }

    private static void ReadDocumentation(IReadOnlyList<RustToken> tokens, RustSourceMetrics metrics)
    {
        foreach (RustToken token in tokens.Where(token => token.Kind == RustTokenKind.Documentation))
        {
            metrics.DocumentationComments++;
            string lower = token.Text.ToLowerInvariant();
            if (lower.Contains("```", StringComparison.Ordinal) &&
                !lower.Contains("```text", StringComparison.Ordinal) &&
                !lower.Contains("```ignore", StringComparison.Ordinal))
                metrics.DocumentationTests++;
        }
    }

    private static bool IsMacro(IReadOnlyList<RustToken> tokens, int index) =>
        index + 1 < tokens.Count && tokens[index + 1].Text == "!";

    private static bool ContainsNearby(IReadOnlyList<RustToken> tokens, int index, string value) =>
        tokens.Skip(index).Take(12).Any(token => token.Text.Contains(value, StringComparison.Ordinal));

    private static bool HasGenericNear(IReadOnlyList<RustToken> tokens, int index) =>
        tokens.Skip(index + 1).TakeWhile(token => token.Text != "{").Any(token => token.Text == "<");

    private static bool PreviousContains(IReadOnlyList<RustToken> tokens, int index, string value)
    {
        for (int cursor = index - 1; cursor >= 0 && index - cursor <= 8; cursor--)
        {
            if (tokens[cursor].Text is ";" or "{" or "}") break;
            if (tokens[cursor].Text == value) return true;
        }
        return false;
    }

    private static bool IsPublic(IReadOnlyList<RustToken> tokens, int index) =>
        PreviousContains(tokens, index, "pub");

    private static int NextIdentifier(IReadOnlyList<RustToken> tokens, int start)
    {
        for (int index = start; index < tokens.Count && index - start < 8; index++)
            if (tokens[index].Kind == RustTokenKind.Identifier) return index;
        return -1;
    }

    private static int NextToken(IReadOnlyList<RustToken> tokens, int start, string value)
    {
        for (int index = start; index < tokens.Count && index - start < 12; index++)
        {
            if (tokens[index].Text == value) return index;
            if (tokens[index].Text == "{") return -1;
        }
        return -1;
    }

    private static int FindMatching(IReadOnlyList<RustToken> tokens, int start, string open, string close)
    {
        int depth = 0;
        for (int index = start; index < tokens.Count; index++)
        {
            if (tokens[index].Text == open) depth++;
            else if (tokens[index].Text == close && --depth == 0) return index;
        }
        return -1;
    }

    private static bool IsTestPath(string path) =>
        path.Split('/').Contains("tests", StringComparer.OrdinalIgnoreCase);

    private static bool IsBenchPath(string path) =>
        path.Split('/').Contains("benches", StringComparer.OrdinalIgnoreCase);

    private static string RelativeToPackage(string path)
    {
        string normalized = path.Replace('\\', '/');
        int source = normalized.LastIndexOf("/src/", StringComparison.Ordinal);
        if (source >= 0) return normalized[(source + 1)..];
        foreach (string root in new[] { "tests/", "benches/", "examples/" })
        {
            int index = normalized.LastIndexOf("/" + root, StringComparison.Ordinal);
            if (index >= 0) return normalized[(index + 1)..];
            if (normalized.StartsWith(root, StringComparison.Ordinal)) return normalized;
        }
        return normalized;
    }
}
