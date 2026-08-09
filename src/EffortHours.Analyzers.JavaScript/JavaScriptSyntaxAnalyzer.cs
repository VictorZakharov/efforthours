using Acornima;
using Acornima.Ast;
using Acornima.Jsx;

namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptSyntaxAnalyzer
{
    public static JavaScriptSyntaxResult Analyze(
        string source,
        string path,
        JavaScriptTokenization tokens,
        bool useTypeScriptLexer,
        bool hasJsx)
    {
        JavaScriptSourceMetrics metrics = AnalyzeTokens(path, tokens, hasJsx);
        if (useTypeScriptLexer)
        {
            metrics.LexerBackedFiles = 1;
            return new JavaScriptSyntaxResult(metrics, "typescript-token-stream", null);
        }

        if (TryParse(source, path, hasJsx, out Node? root, out int? errorLine, out string parserKind) ||
            (!hasJsx && TryParse(source, path, true, out root, out errorLine, out parserKind)))
        {
            ApplyAstMetrics(root!, metrics);
            metrics.ParserBackedFiles = 1;
            return new JavaScriptSyntaxResult(metrics, parserKind, null);
        }

        metrics.LexerBackedFiles = 1;
        return new JavaScriptSyntaxResult(metrics, "token-stream-fallback", errorLine);
    }

    private static JavaScriptSourceMetrics AnalyzeTokens(
        string path,
        JavaScriptTokenization tokenization,
        bool hasJsx)
    {
        JavaScriptSourceMetrics metrics = new() { Files = 1 };
        IReadOnlyList<JavaScriptToken> tokens = tokenization.Tokens;
        AddPathSignals(path, metrics);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokenization.Is(index, "import"))
            {
                if (tokenization.Is(index + 1, "("))
                {
                    metrics.DynamicImports++;
                }
                else
                {
                    metrics.Imports++;
                }

                AddImportedTechnology(tokenization, index, metrics);
            }
            else if (tokenization.Is(index, "export"))
            {
                metrics.Exports++;
                AddNextRouteHandler(path, tokenization, index, metrics);
            }
            else if (tokenization.Is(index, "function"))
            {
                metrics.Functions++;
                if (tokenization.Is(index - 1, "async"))
                {
                    metrics.AsyncFunctions++;
                }
            }
            else if (tokenization.Is(index, "=>"))
            {
                metrics.Functions++;
                if (HasNearbyToken(tokenization, index, "async", 4))
                {
                    metrics.AsyncFunctions++;
                }
            }
            else if (tokenization.Is(index, "class"))
            {
                metrics.Classes++;
            }
            else if (tokenization.Is(index, "interface"))
            {
                metrics.Interfaces++;
            }
            else if (tokenization.Is(index, "type") && tokenization.IsIdentifier(index + 1) &&
                !tokenization.Is(index - 1, "import"))
            {
                metrics.TypeAliases++;
            }
            else if (tokenization.Is(index, "enum"))
            {
                metrics.Enums++;
            }
            else if (IsBranchToken(tokenization, index))
            {
                metrics.BranchPoints++;
            }
            else if (tokenization.Is(index, "@") && tokenization.IsIdentifier(index + 1))
            {
                metrics.Decorators++;
                AnalyzeDecorator(tokenization, index + 1, metrics);
            }

            if (tokenization.Is(index, "(") && tokenization.IsIdentifier(index - 1))
            {
                bool isMethod = LooksLikeMethodDeclaration(tokenization, index);
                if (isMethod)
                {
                    metrics.Methods++;
                    if (tokenization.Is(index - 2, "async"))
                    {
                        metrics.AsyncFunctions++;
                    }
                }

                if (!isMethod)
                {
                    AnalyzeCall(tokenization, index - 1, metrics);
                }
            }

            if (hasJsx && tokenization.Is(index, "<") && tokenization.IsIdentifier(index + 1) &&
                !tokenization.Is(index - 1, "/"))
            {
                metrics.JsxElements++;
                metrics.UiLine ??= tokens[index].Line;
                if (tokenization.Is(index + 1, "form"))
                {
                    metrics.FormUsages++;
                }
            }
        }

        if (tokenization.HasHashBang)
        {
            metrics.EntryPointLine = 1;
        }

        if (metrics.JsxElements > 0)
        {
            metrics.UiComponents = Math.Max(metrics.UiComponents, 1);
        }

        return metrics;
    }

    private static void AnalyzeCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics)
    {
        if (tokenization.Is(nameIndex - 1, "function") || tokenization.Is(nameIndex - 1, "class"))
        {
            return;
        }

        metrics.Calls++;
        int line = tokenization.Tokens[nameIndex].Line;
        if (IsAny(tokenization, nameIndex, "require"))
        {
            metrics.Imports++;
            AddRequiredTechnology(tokenization, nameIndex + 2, metrics);
        }

        if (IsAny(tokenization, nameIndex, "test", "it"))
        {
            metrics.TestCases++;
            metrics.TestLine ??= line;
        }
        else if (IsAny(tokenization, nameIndex, "describe", "suite"))
        {
            metrics.TestSuites++;
            metrics.TestLine ??= line;
        }
        else if (IsAny(tokenization, nameIndex, "expect", "assert", "assertThat"))
        {
            metrics.Assertions++;
            metrics.TestLine ??= line;
        }
        else if (IsAny(tokenization, nameIndex, "mock", "spyOn", "stub", "vi", "jest"))
        {
            metrics.MockUsages++;
            metrics.TestLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "render", "renderHook", "mount", "shallow") &&
            metrics.TechnologyFamilies.Contains("test-component"))
        {
            metrics.ComponentTestUsages++;
            metrics.TestLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "request") &&
            metrics.Technologies.Contains("supertest"))
        {
            metrics.IntegrationTestUsages++;
            metrics.TestLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "goto", "visit", "locator") &&
            metrics.TechnologyFamilies.Contains("test-e2e"))
        {
            metrics.EndToEndTestUsages++;
            metrics.TestLine ??= line;
        }

        if (IsHttpMethod(tokenization, nameIndex) && HasRouteReceiver(tokenization, nameIndex))
        {
            metrics.ApiEndpoints++;
            metrics.ApiLine ??= line;
            metrics.HttpMethods.Add(tokenization.Tokens[nameIndex].Span(tokenization.Source).ToString().ToLowerInvariant());
        }
        else if (IsAny(tokenization, nameIndex, "route", "register") &&
            HasRouteReceiver(tokenization, nameIndex))
        {
            metrics.ApiEndpoints++;
            metrics.ApiLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "useState", "useReducer", "useContext", "createStore", "configureStore", "createSlice", "signal", "ref", "reactive"))
        {
            metrics.StateUsages++;
            metrics.UiLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "useEffect", "useLayoutEffect", "watch", "watchEffect", "onMount"))
        {
            metrics.EffectUsages++;
            metrics.UiLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "useForm", "createForm"))
        {
            metrics.FormUsages++;
            metrics.UiLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "createElement", "createRoot", "createApp", "bootstrapApplication", "mount"))
        {
            metrics.UiComponents = Math.Max(metrics.UiComponents, 1);
            metrics.UiLine ??= line;
        }

        if (IsDataCall(tokenization, nameIndex, metrics))
        {
            metrics.DataCalls++;
            metrics.DataLine ??= line;
        }

        if (IsIntegrationCall(tokenization, nameIndex, metrics))
        {
            metrics.IntegrationCalls++;
            metrics.IntegrationLine ??= line;
        }

        if (IsSecurityCall(tokenization, nameIndex, metrics))
        {
            metrics.SecurityUsages++;
            metrics.SecurityLine ??= line;
        }

        if (IsValidationCall(tokenization, nameIndex, metrics))
        {
            metrics.ValidationUsages++;
            metrics.ValidationLine ??= line;
        }

        if (IsBackgroundCall(tokenization, nameIndex, metrics))
        {
            metrics.BackgroundUsages++;
            metrics.BackgroundLine ??= line;
        }
    }

    private static void AnalyzeDecorator(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics)
    {
        int line = tokenization.Tokens[nameIndex].Line;
        if (IsAny(tokenization, nameIndex, "Controller", "Resolver"))
        {
            metrics.ApiControllers++;
            metrics.ApiLine ??= line;
        }

        if (IsHttpMethod(tokenization, nameIndex))
        {
            metrics.ApiEndpoints++;
            metrics.ApiLine ??= line;
            metrics.HttpMethods.Add(tokenization.Tokens[nameIndex].Span(tokenization.Source).ToString().ToLowerInvariant());
        }

        if (IsAny(tokenization, nameIndex, "Query", "Mutation", "Subscription", "ResolveField"))
        {
            metrics.GraphQlOperations++;
            metrics.ApiLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "Component", "Directive", "Pipe"))
        {
            metrics.UiComponents++;
            metrics.UiLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "Entity", "Column", "Schema", "Prop"))
        {
            metrics.DataCalls++;
            metrics.DataLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "UseGuards", "Roles", "Public", "Permissions"))
        {
            metrics.SecurityUsages++;
            metrics.SecurityLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "IsString", "IsNumber", "IsEmail", "ValidateNested", "Min", "Max"))
        {
            metrics.ValidationUsages++;
            metrics.ValidationLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "Processor", "Process", "Cron", "Interval", "Timeout"))
        {
            metrics.BackgroundUsages++;
            metrics.BackgroundLine ??= line;
        }
    }

    private static bool TryParse(
        string source,
        string path,
        bool jsx,
        out Node? root,
        out int? errorLine,
        out string parserKind)
    {
        ParseErrorException? lastError = null;
        foreach (bool module in PreferredParseOrder(path))
        {
            try
            {
                ParserOptions options = jsx
                    ? new JsxParserOptions
                    {
                        EcmaVersion = EcmaVersion.Latest,
                        AllowHashBang = true,
                        OnRegExp = SkipRegularExpressionParsing,
                    }
                    : new ParserOptions
                    {
                        EcmaVersion = EcmaVersion.Latest,
                        AllowHashBang = true,
                        OnRegExp = SkipRegularExpressionParsing,
                    };
                if (jsx)
                {
                    JsxParser parser = new((JsxParserOptions)options);
                    root = module
                        ? parser.ParseModule(source, path)
                        : parser.ParseScript(source, path);
                }
                else
                {
                    Parser parser = new(options);
                    root = module
                        ? parser.ParseModule(source, path)
                        : parser.ParseScript(source, path);
                }

                errorLine = null;
                parserKind = jsx ? "acornima-jsx-ast" : "acornima-ast";
                return true;
            }
            catch (ParseErrorException exception)
            {
                lastError = exception;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                lastError = null;
            }
        }

        root = null;
        errorLine = lastError?.LineNumber is > 0 ? lastError.LineNumber : null;
        parserKind = string.Empty;
        return false;
    }

    private static void ApplyAstMetrics(Node root, JavaScriptSourceMetrics metrics)
    {
        int imports = 0;
        int dynamicImports = 0;
        int exports = 0;
        int functions = 0;
        int asyncFunctions = 0;
        int classes = 0;
        int branches = 0;
        int calls = 0;
        int decorators = 0;
        int jsxElements = 0;
        Stack<Node> pending = new();
        pending.Push(root);
        while (pending.TryPop(out Node? node))
        {
            switch (node.Type)
            {
                case NodeType.ImportDeclaration:
                    imports++;
                    break;
                case NodeType.ImportExpression:
                    dynamicImports++;
                    break;
                case NodeType.ExportAllDeclaration:
                case NodeType.ExportDefaultDeclaration:
                case NodeType.ExportNamedDeclaration:
                    exports++;
                    break;
                case NodeType.FunctionDeclaration:
                    functions++;
                    asyncFunctions += ((FunctionDeclaration)node).Async ? 1 : 0;
                    break;
                case NodeType.FunctionExpression:
                    functions++;
                    asyncFunctions += ((FunctionExpression)node).Async ? 1 : 0;
                    break;
                case NodeType.ArrowFunctionExpression:
                    functions++;
                    asyncFunctions += ((ArrowFunctionExpression)node).Async ? 1 : 0;
                    break;
                case NodeType.ClassDeclaration:
                case NodeType.ClassExpression:
                    classes++;
                    break;
                case NodeType.IfStatement:
                case NodeType.ForStatement:
                case NodeType.ForInStatement:
                case NodeType.ForOfStatement:
                case NodeType.WhileStatement:
                case NodeType.DoWhileStatement:
                case NodeType.CatchClause:
                case NodeType.ConditionalExpression:
                case NodeType.SwitchCase:
                    branches++;
                    break;
                case NodeType.CallExpression:
                    calls++;
                    break;
                case NodeType.Decorator:
                    decorators++;
                    break;
            }

            if (node.GetType().Name == "JsxOpeningElement")
            {
                jsxElements++;
            }

            foreach (Node child in node.ChildNodes)
            {
                pending.Push(child);
            }
        }

        metrics.Imports = imports;
        metrics.DynamicImports = dynamicImports;
        metrics.Exports = exports;
        metrics.Functions = functions;
        metrics.AsyncFunctions = asyncFunctions;
        metrics.Classes = classes;
        metrics.BranchPoints = branches;
        metrics.Calls = calls;
        metrics.Decorators = decorators;
        metrics.JsxElements = jsxElements;
        if (jsxElements > 0)
        {
            metrics.UiComponents = Math.Max(metrics.UiComponents, 1);
        }
    }

    private static void AddImportedTechnology(
        JavaScriptTokenization tokenization,
        int importIndex,
        JavaScriptSourceMetrics metrics)
    {
        for (int index = importIndex + 1;
             index < tokenization.Tokens.Count && index <= importIndex + 24;
             index++)
        {
            if (tokenization.Is(index, ";"))
            {
                return;
            }

            if (tokenization.Tokens[index].Kind == JavaScriptTokenKind.String)
            {
                AddTechnology(tokenization.ReadModuleSpecifier(index), metrics);
                return;
            }
        }
    }

    private static void AddRequiredTechnology(
        JavaScriptTokenization tokenization,
        int stringIndex,
        JavaScriptSourceMetrics metrics) =>
        AddTechnology(tokenization.ReadModuleSpecifier(stringIndex), metrics);

    private static void AddTechnology(string? packageName, JavaScriptSourceMetrics metrics)
    {
        if (packageName is null)
        {
            return;
        }

        TechnologyClassification? classification = JavaScriptTechnologyCatalog.Classify(packageName);
        if (classification is not null)
        {
            metrics.Technologies.Add(classification.Name);
            metrics.TechnologyFamilies.Add(classification.Family);
            metrics.ObservedTechnologies.Add(classification.Name);
            metrics.ObservedTechnologyFamilies.Add(classification.Family);
        }
    }

    private static RegExpParseResult SkipRegularExpressionParsing(
        in RegExpParsingContext context)
    {
        _ = context;
        return RegExpParseResult.ForSuccess(null, null);
    }

    private static void AddPathSignals(string path, JavaScriptSourceMetrics metrics)
    {
        string lowerPath = path.ToLowerInvariant();
        string fileName = Path.GetFileNameWithoutExtension(lowerPath);
        if (lowerPath.Contains("/migrations/", StringComparison.Ordinal) ||
            lowerPath.Contains("/migration/", StringComparison.Ordinal))
        {
            metrics.Migrations = 1;
            metrics.DataLine = 1;
        }

        if (lowerPath.Contains("/pages/", StringComparison.Ordinal) ||
            lowerPath.Contains("/routes/", StringComparison.Ordinal) ||
            fileName is "page" or "layout")
        {
            metrics.UiPages = 1;
            metrics.UiLine = 1;
        }
    }

    private static void AddNextRouteHandler(
        string path,
        JavaScriptTokenization tokenization,
        int exportIndex,
        JavaScriptSourceMetrics metrics)
    {
        if (!Path.GetFileNameWithoutExtension(path).Equals("route", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        for (int index = exportIndex + 1;
             index < tokenization.Tokens.Count && index <= exportIndex + 6;
             index++)
        {
            if (IsUpperHttpMethod(tokenization, index))
            {
                metrics.ApiEndpoints++;
                metrics.ApiLine ??= tokenization.Tokens[index].Line;
                metrics.HttpMethods.Add(tokenization.Tokens[index].Span(tokenization.Source).ToString().ToLowerInvariant());
                return;
            }
        }
    }

    private static bool IsDataCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics)
    {
        if (IsAny(tokenization, nameIndex, "findMany", "findUnique", "findFirst", "insert", "select", "transaction", "aggregate", "upsert"))
        {
            return metrics.TechnologyFamilies.Contains("data") ||
                ChainContains(tokenization, nameIndex, "db", "prisma", "repository", "model", "query");
        }

        return IsAny(tokenization, nameIndex, "connect", "createConnection") &&
            metrics.TechnologyFamilies.Contains("data");
    }

    private static bool IsIntegrationCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics)
    {
        if (IsAny(tokenization, nameIndex, "fetch"))
        {
            return true;
        }

        return metrics.TechnologyFamilies.Contains("integration") &&
            (IsAny(tokenization, nameIndex, "send", "request", "publish", "consume", "upload", "download", "create") ||
             ChainContains(tokenization, nameIndex, "axios", "stripe", "twilio", "firebase", "kafka", "socket"));
    }

    private static bool IsSecurityCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics) =>
        IsAny(tokenization, nameIndex, "authenticate", "authorize", "signIn", "signOut", "hashPassword", "verifyToken", "useGuards") ||
        (metrics.TechnologyFamilies.Contains("security") &&
         IsAny(tokenization, nameIndex, "sign", "verify", "hash", "compare", "use"));

    private static bool IsValidationCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics) =>
        IsAny(tokenization, nameIndex, "safeParse", "validateOrReject", "validateSync") ||
        (metrics.TechnologyFamilies.Contains("validation") &&
         IsAny(tokenization, nameIndex, "parse", "validate", "check"));

    private static bool IsBackgroundCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics) =>
        IsAny(tokenization, nameIndex, "schedule", "cron", "processJob", "enqueue") ||
        (metrics.TechnologyFamilies.Contains("background") &&
         IsAny(tokenization, nameIndex, "add", "process", "repeat", "run", "start"));

    private static bool HasRouteReceiver(JavaScriptTokenization tokenization, int nameIndex)
    {
        if (!tokenization.Is(nameIndex - 1, ".") && !tokenization.Is(nameIndex - 1, "?."))
        {
            return false;
        }

        return ChainContains(
            tokenization,
            nameIndex,
            "app", "router", "server", "fastify", "hono", "route", "api");
    }

    private static bool ChainContains(
        JavaScriptTokenization tokenization,
        int nameIndex,
        params ReadOnlySpan<string> candidates)
    {
        int inspected = 0;
        for (int index = nameIndex - 2; index >= 0 && inspected < 6; index -= 2, inspected++)
        {
            if (!tokenization.IsIdentifier(index))
            {
                return false;
            }

            foreach (string candidate in candidates)
            {
                if (tokenization.Is(index, candidate))
                {
                    return true;
                }
            }

            if (index == 0 ||
                (!tokenization.Is(index - 1, ".") && !tokenization.Is(index - 1, "?.")))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsBranchToken(JavaScriptTokenization tokenization, int index) =>
        IsAny(tokenization, index, "if", "for", "while", "catch", "switch", "case") ||
        tokenization.Is(index, "?");

    private static bool IsHttpMethod(JavaScriptTokenization tokenization, int index) =>
        IsAny(tokenization, index, "get", "post", "put", "patch", "delete", "options", "head", "all",
            "Get", "Post", "Put", "Patch", "Delete", "Options", "Head", "All");

    private static bool IsUpperHttpMethod(JavaScriptTokenization tokenization, int index) =>
        IsAny(tokenization, index, "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD");

    private static bool HasNearbyToken(
        JavaScriptTokenization tokenization,
        int beforeIndex,
        string value,
        int maximumDistance)
    {
        for (int index = beforeIndex - 1;
             index >= 0 && index >= beforeIndex - maximumDistance;
             index--)
        {
            if (tokenization.Is(index, value))
            {
                return true;
            }

            if (tokenization.Is(index, ";") || tokenization.Is(index, "{"))
            {
                return false;
            }
        }

        return false;
    }

    private static bool LooksLikeMethodDeclaration(
        JavaScriptTokenization tokenization,
        int openingParenthesis)
    {
        int nameIndex = openingParenthesis - 1;
        if (IsAny(
            tokenization,
            nameIndex,
            "if", "for", "while", "switch", "catch", "with", "function") ||
            tokenization.Is(nameIndex - 1, "function") ||
            tokenization.Is(nameIndex - 1, ".") ||
            tokenization.Is(nameIndex - 1, "?."))
        {
            return false;
        }

        int depth = 1;
        int closingParenthesis = -1;
        int limit = Math.Min(tokenization.Tokens.Count, openingParenthesis + 512);
        for (int index = openingParenthesis + 1; index < limit; index++)
        {
            if (tokenization.Is(index, "("))
            {
                depth++;
            }
            else if (tokenization.Is(index, ")") && --depth == 0)
            {
                closingParenthesis = index;
                break;
            }
        }

        if (closingParenthesis < 0)
        {
            return false;
        }

        for (int index = closingParenthesis + 1;
             index < tokenization.Tokens.Count && index <= closingParenthesis + 64;
             index++)
        {
            if (tokenization.Is(index, "{"))
            {
                return true;
            }

            if (tokenization.Is(index, "=>") || tokenization.Is(index, ";") ||
                tokenization.Is(index, ".") || tokenization.Is(index, "?.") ||
                tokenization.Is(index, ",") || tokenization.Is(index, ")"))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsAny(
        JavaScriptTokenization tokenization,
        int index,
        params ReadOnlySpan<string> values)
    {
        foreach (string value in values)
        {
            if (tokenization.Is(index, value))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<bool> PreferredParseOrder(string path)
    {
        if (Path.GetExtension(path).Equals(".cjs", StringComparison.OrdinalIgnoreCase))
        {
            yield return false;
            yield return true;
        }
        else
        {
            yield return true;
            yield return false;
        }
    }
}

internal sealed record JavaScriptSyntaxResult(
    JavaScriptSourceMetrics Metrics,
    string Method,
    int? ParseErrorLine);
