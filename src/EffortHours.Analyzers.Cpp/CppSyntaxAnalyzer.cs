namespace EffortHours.Analyzers.Cpp;

internal static class CppSyntaxAnalyzer
{
    private static readonly HashSet<string> ControlNames = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "sizeof", "alignof", "decltype",
        "static_cast", "dynamic_cast", "reinterpret_cast", "const_cast", "typeid", "requires",
    };

    public static CppSyntaxAnalysis Analyze(
        string source,
        string path,
        IEnumerable<string>? declaredDependencies = null,
        CancellationToken cancellationToken = default)
    {
        CppTokenization full = CppTokenizer.Tokenize(source, cancellationToken);
        CppPreprocessorAnalysis preprocessor = CppPreprocessorAnalyzer.Analyze(full.Tokens);
        HashSet<string> dependencies = declaredDependencies?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        (CppSourceMetrics metrics, string confidence) =
            full.Truncated || preprocessor.MaximumDepth > CppPreprocessorAnalyzer.MaximumConditionalDepth
                ? (new CppSourceMetrics(), "low")
                : AnalyzeConditional(
                    CppConditionalNormalizer.Split(source, cancellationToken),
                    path,
                    dependencies,
                    0,
                    cancellationToken);
        metrics.Files = 1;
        metrics.TranslationUnits = CppPath.IsSource(path) ? 1 : 0;
        metrics.Headers = CppPath.IsHeader(path) ? 1 : 0;
        confidence = full.Confidence == "low" || preprocessor.Malformed ? "low" : confidence;
        return new CppSyntaxAnalysis(confidence, preprocessor, metrics);
    }

    private static (CppSourceMetrics Metrics, string Confidence) AnalyzeConditional(
        ConditionalSource source,
        string path,
        IReadOnlySet<string> dependencies,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CppTokenization tokens = CppTokenizer.Tokenize(source.Unconditional, cancellationToken);
        CppSourceMetrics result = AnalyzeTokens(tokens.Tokens, path, dependencies, cancellationToken);
        string confidence = tokens.Confidence;
        if (depth >= CppPreprocessorAnalyzer.MaximumConditionalDepth)
            return (result, "low");

        foreach (IReadOnlyList<string> group in source.Groups)
        {
            CppSourceMetrics? maximum = null;
            foreach (string branch in group)
            {
                ConditionalSource nested = CppConditionalNormalizer.Split(branch, cancellationToken);
                (CppSourceMetrics branchMetrics, string branchConfidence) =
                    AnalyzeConditional(nested, path, dependencies, depth + 1, cancellationToken);
                maximum = maximum is null ? branchMetrics : ComponentMaximum(maximum, branchMetrics);
                if (branchConfidence == "low") confidence = "low";
            }
            if (maximum is not null) Add(result, maximum);
        }
        return (result, confidence);
    }

    private static CppSourceMetrics AnalyzeTokens(
        IReadOnlyList<CppToken> tokens,
        string path,
        IReadOnlySet<string> dependencies,
        CancellationToken cancellationToken)
    {
        CppSourceMetrics metrics = new()
        {
            TranslationUnits = CppPath.IsSource(path) ? 1 : 0,
            Headers = CppPath.IsHeader(path) ? 1 : 0,
        };
        Dictionary<int, ScopeFrame> openings = [];
        Stack<ScopeFrame> scopes = [];
        for (int index = 0; index < tokens.Count; index++)
        {
            if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            CppToken token = tokens[index];
            if (token.Kind == CppTokenKind.Documentation) metrics.DocumentationComments++;
            if (token.Kind == CppTokenKind.Preprocessor || token.Kind == CppTokenKind.End) continue;

            if (token.Text == "{")
            {
                scopes.Push(openings.GetValueOrDefault(index) ?? new ScopeFrame("block", false));
                continue;
            }
            if (token.Text == "}")
            {
                if (scopes.Count > 0) scopes.Pop();
                continue;
            }
            if (token.Text is "public" or "protected" or "private" && NextText(tokens, index) == ":")
            {
                ScopeFrame? type = scopes.FirstOrDefault(scope => scope.Kind == "type");
                type?.Public = token.Text == "public";
                continue;
            }
            if (token.Text == "namespace")
            {
                metrics.Namespaces++;
                int opening = FindNext(tokens, index + 1, "{");
                if (opening >= 0)
                    openings[opening] = new ScopeFrame("namespace", true, NextIdentifier(tokens, index));
                continue;
            }
            if (token.Text is "class" or "struct" or "union" or "enum")
            {
                ReadType(tokens, index, token.Text, scopes, openings, metrics);
                continue;
            }
            if (token.Text == "typedef") metrics.Typedefs++;
            if (token.Text == "template") metrics.Templates++;
            if (token.Text == "concept") metrics.Concepts++;
            if (token.Text == "requires") metrics.Concepts++;
            if (token.Text == "module") metrics.Modules++;
            if (token.Text == "import") metrics.Imports++;
            if (token.Text is "co_await" or "co_return" or "co_yield") metrics.AsyncUnits++;
            if (token.Text is "if" or "else" or "switch" or "case" or "for" or "while" or "do" or "?")
                metrics.BranchPoints++;
            if (token.Text is "try" or "catch" or "throw") metrics.ErrorPaths++;
            if (token.Text == "[" && LooksLikeLambda(tokens, index)) metrics.Lambdas++;
            if (token.Text == "extern" && index + 1 < tokens.Count &&
                tokens[index + 1].Kind == CppTokenKind.String &&
                LiteralIsC(tokens[index + 1].Text)) metrics.FfiBoundaries++;

            if (CanBeFunctionName(token) && NextText(tokens, index) == "(" &&
                !scopes.Any(scope => scope.Kind == "function") &&
                TryReadFunction(tokens, index, scopes, out int bodyOpening, out bool publicSymbol))
            {
                bool method = scopes.Any(scope => scope.Kind == "type") || PreviousText(tokens, index) == "::";
                if (method) metrics.Methods++;
                else metrics.Functions++;
                if (publicSymbol)
                    metrics.PublicDeclarationKeys.Add(DeclarationKey(
                        scopes,
                        "function",
                        FunctionSignature(tokens, index)));
                if (token.Text == "main") metrics.EntryPoints++;
                if (bodyOpening >= 0) openings[bodyOpening] = new ScopeFrame("function", false);
            }
        }

        metrics.PublicSymbols = metrics.UnkeyedPublicSymbols + metrics.PublicDeclarationKeys.Count;
        CppSemanticAnalyzer.Analyze(tokens, path, dependencies, metrics);
        return metrics;
    }

    private static void ReadType(
        IReadOnlyList<CppToken> tokens,
        int index,
        string kind,
        IEnumerable<ScopeFrame> scopes,
        Dictionary<int, ScopeFrame> openings,
        CppSourceMetrics metrics)
    {
        if (PreviousText(tokens, index) == "enum" && kind is "class" or "struct") return;
        metrics.Types++;
        if (kind == "class") metrics.Classes++;
        else if (kind == "struct") metrics.Structs++;
        else if (kind == "union") metrics.Unions++;
        else metrics.Enums++;
        bool nestedPrivate = scopes.FirstOrDefault(scope => scope.Kind == "type") is { Public: false };
        string name = TypeName(tokens, index, kind);
        if (!nestedPrivate)
        {
            if (name.Length > 0)
                metrics.PublicDeclarationKeys.Add(DeclarationKey(scopes, kind, name));
            else metrics.UnkeyedPublicSymbols++;
        }
        int opening = FindBefore(tokens, index + 1, "{", ";");
        if (opening >= 0)
            openings[opening] = new ScopeFrame("type", kind != "class", name);
    }

    private static bool TryReadFunction(
        IReadOnlyList<CppToken> tokens,
        int nameIndex,
        IEnumerable<ScopeFrame> scopes,
        out int bodyOpening,
        out bool publicSymbol)
    {
        bodyOpening = -1;
        publicSymbol = false;
        string name = tokens[nameIndex].Text;
        if (ControlNames.Contains(name)) return false;
        string previous = PreviousText(tokens, nameIndex);
        if (previous is "." or "->") return false;
        if (HasAssignmentSinceBoundary(tokens, nameIndex)) return false;
        int open = nameIndex + 1;
        int close = FindMatching(tokens, open, "(", ")");
        if (close < 0) return false;
        int cursor = close + 1;
        int limit = Math.Min(tokens.Count, close + 32);
        while (cursor < limit &&
            (tokens[cursor].Text is
                "const" or "volatile" or "noexcept" or "override" or "final" or "&" or "&&" or
                "requires" or "->" or "=" or "default" or "delete" or "0" or ":" ||
             tokens[cursor].Kind is CppTokenKind.Identifier or CppTokenKind.Keyword))
        {
            if (tokens[cursor].Text is "{" or ";") break;
            cursor++;
        }
        while (cursor < limit && tokens[cursor].Text is not ("{" or ";")) cursor++;
        if (cursor >= limit) return false;
        bodyOpening = tokens[cursor].Text == "{" ? cursor : -1;
        ScopeFrame? type = scopes.FirstOrDefault(scope => scope.Kind == "type");
        bool isStatic = HasTokenSinceBoundary(tokens, nameIndex, "static");
        publicSymbol = !isStatic && (type?.Public ?? true);
        return true;
    }

    private static bool HasAssignmentSinceBoundary(IReadOnlyList<CppToken> tokens, int index)
    {
        for (int cursor = index - 1; cursor >= 0 && tokens[cursor].Text is not (";" or "{" or "}"); cursor--)
            if (tokens[cursor].Text == "=") return true;
        return false;
    }

    private static bool HasTokenSinceBoundary(IReadOnlyList<CppToken> tokens, int index, string value)
    {
        for (int cursor = index - 1; cursor >= 0 && tokens[cursor].Text is not (";" or "{" or "}"); cursor--)
            if (tokens[cursor].Text == value) return true;
        return false;
    }

    private static bool LooksLikeLambda(IReadOnlyList<CppToken> tokens, int index)
    {
        int close = FindMatching(tokens, index, "[", "]");
        if (close < 0 || close + 1 >= tokens.Count) return false;
        return tokens[close + 1].Text is "(" or "{" or "mutable" or "noexcept";
    }

    private static bool CanBeFunctionName(CppToken token) =>
        token.Kind == CppTokenKind.Identifier || token.Text == "operator";

    private static string TypeName(IReadOnlyList<CppToken> tokens, int index, string kind)
    {
        int candidate = kind == "enum" && NextText(tokens, index) is "class" or "struct"
            ? index + 2
            : index + 1;
        return candidate < tokens.Count && tokens[candidate].Kind == CppTokenKind.Identifier
            ? tokens[candidate].Text
            : string.Empty;
    }

    private static string FunctionSignature(IReadOnlyList<CppToken> tokens, int nameIndex)
    {
        int close = FindMatching(tokens, nameIndex + 1, "(", ")");
        if (close < 0) return tokens[nameIndex].Text;
        return string.Join(' ', tokens.Skip(nameIndex).Take(close - nameIndex + 1).Select(token => token.Text));
    }

    private static string DeclarationKey(IEnumerable<ScopeFrame> scopes, string kind, string signature)
    {
        string owner = string.Join("::", scopes.Reverse()
            .Where(scope => scope.Name.Length > 0)
            .Select(scope => scope.Name));
        return $"{owner}|{kind}|{signature}";
    }

    private static bool LiteralIsC(string value) =>
        value.TrimStart('u', 'U', 'L', '8').Equals("\"C\"", StringComparison.Ordinal);

    private static int FindMatching(IReadOnlyList<CppToken> tokens, int index, string open, string close)
    {
        if (index >= tokens.Count || tokens[index].Text != open) return -1;
        int depth = 0;
        for (int cursor = index; cursor < tokens.Count; cursor++)
        {
            if (tokens[cursor].Text == open) depth++;
            else if (tokens[cursor].Text == close && --depth == 0) return cursor;
        }
        return -1;
    }

    private static int FindNext(IReadOnlyList<CppToken> tokens, int index, string value)
    {
        for (int cursor = index; cursor < tokens.Count; cursor++)
        {
            if (tokens[cursor].Text == value) return cursor;
            if (tokens[cursor].Text == ";") return -1;
        }
        return -1;
    }

    private static int FindBefore(
        IReadOnlyList<CppToken> tokens,
        int index,
        string value,
        string boundary)
    {
        for (int cursor = index; cursor < tokens.Count; cursor++)
        {
            if (tokens[cursor].Text == value) return cursor;
            if (tokens[cursor].Text == boundary) return -1;
        }
        return -1;
    }

    private static string NextText(IReadOnlyList<CppToken> tokens, int index) =>
        index + 1 < tokens.Count ? tokens[index + 1].Text : string.Empty;

    private static string NextIdentifier(IReadOnlyList<CppToken> tokens, int index) =>
        index + 1 < tokens.Count && tokens[index + 1].Kind == CppTokenKind.Identifier
            ? tokens[index + 1].Text
            : string.Empty;

    private static string PreviousText(IReadOnlyList<CppToken> tokens, int index) =>
        index > 0 ? tokens[index - 1].Text : string.Empty;

    private static CppSourceMetrics ComponentMaximum(CppSourceMetrics left, CppSourceMetrics right)
    {
        CppSourceMetrics result = new() { Files = 0 };
        Merge(result, left, Math.Max);
        Merge(result, right, Math.Max);
        result.Technologies.UnionWith(left.Technologies);
        result.Technologies.UnionWith(right.Technologies);
        return result;
    }

    private static void Add(CppSourceMetrics target, CppSourceMetrics source) =>
        Merge(target, source, static (left, right) => left + right);

    private static void Merge(CppSourceMetrics target, CppSourceMetrics source, Func<int, int, int> combine)
    {
        target.Files = combine(target.Files, source.Files);
        target.TranslationUnits = combine(target.TranslationUnits, source.TranslationUnits);
        target.Headers = combine(target.Headers, source.Headers);
        target.Namespaces = combine(target.Namespaces, source.Namespaces);
        target.Functions = combine(target.Functions, source.Functions);
        target.Methods = combine(target.Methods, source.Methods);
        target.Types = combine(target.Types, source.Types);
        target.Classes = combine(target.Classes, source.Classes);
        target.Structs = combine(target.Structs, source.Structs);
        target.Unions = combine(target.Unions, source.Unions);
        target.Enums = combine(target.Enums, source.Enums);
        target.Typedefs = combine(target.Typedefs, source.Typedefs);
        target.UnkeyedPublicSymbols = combine(target.UnkeyedPublicSymbols, source.UnkeyedPublicSymbols);
        target.Templates = combine(target.Templates, source.Templates);
        target.Concepts = combine(target.Concepts, source.Concepts);
        target.Lambdas = combine(target.Lambdas, source.Lambdas);
        target.Modules = combine(target.Modules, source.Modules);
        target.Imports = combine(target.Imports, source.Imports);
        target.AsyncUnits = combine(target.AsyncUnits, source.AsyncUnits);
        target.BranchPoints = combine(target.BranchPoints, source.BranchPoints);
        target.ErrorPaths = combine(target.ErrorPaths, source.ErrorPaths);
        target.ConcurrencyUsages = combine(target.ConcurrencyUsages, source.ConcurrencyUsages);
        target.FfiBoundaries = combine(target.FfiBoundaries, source.FfiBoundaries);
        target.EntryPoints = combine(target.EntryPoints, source.EntryPoints);
        target.ApiSurfaces = combine(target.ApiSurfaces, source.ApiSurfaces);
        target.CliCommands = combine(target.CliCommands, source.CliCommands);
        target.DataCalls = combine(target.DataCalls, source.DataCalls);
        target.IntegrationCalls = combine(target.IntegrationCalls, source.IntegrationCalls);
        target.SecurityUsages = combine(target.SecurityUsages, source.SecurityUsages);
        target.ValidationRules = combine(target.ValidationRules, source.ValidationRules);
        target.UiSurfaces = combine(target.UiSurfaces, source.UiSurfaces);
        target.TestCases = combine(target.TestCases, source.TestCases);
        target.Assertions = combine(target.Assertions, source.Assertions);
        target.Benchmarks = combine(target.Benchmarks, source.Benchmarks);
        target.FuzzTargets = combine(target.FuzzTargets, source.FuzzTargets);
        target.DocumentationComments = combine(target.DocumentationComments, source.DocumentationComments);
        target.Technologies.UnionWith(source.Technologies);
        target.PublicDeclarationKeys.UnionWith(source.PublicDeclarationKeys);
        target.PublicSymbols = target.UnkeyedPublicSymbols + target.PublicDeclarationKeys.Count;
    }

    private sealed class ScopeFrame(string kind, bool isPublic, string name = "")
    {
        public string Kind { get; } = kind;
        public bool Public { get; set; } = isPublic;
        public string Name { get; } = name;
    }
}
