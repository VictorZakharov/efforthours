using Acornima.Ast;

namespace EffortHours.Analyzers.JavaScript;

// Bind declarations before counting calls, so hoisting and later shadow declarations
// cannot turn production methods or parameters into framework test APIs.
internal static class JavaScriptTestDeclarations
{
    internal enum Api { Unknown, Case, Suite, Assertion, Mock, Namespace }

    public static void Analyze(Node root, bool testFile, JavaScriptSourceMetrics metrics)
    {
        Scope global = new(null, testFile, function: true);
        List<(Node Node, Scope Scope)> nodes = [];
        Stack<(Node Node, Scope Scope)> pending = new();
        pending.Push((root, global));
        while (pending.TryPop(out var item))
        {
            Node node = item.Node;
            Scope scope = item.Scope;
            if (node is FunctionDeclaration declaration && declaration.Id is not null)
            {
                scope.Declare(declaration.Id.Name, Api.Unknown);
            }
            if (node is IFunction function)
            {
                scope = new Scope(scope, testFile, function: true);
                if (function.Id is not null) scope.Declare(function.Id.Name, Api.Unknown);
                foreach (Node parameter in function.Params) DeclarePattern(parameter, scope);
            }
            else if (node.Type is NodeType.BlockStatement or NodeType.CatchClause or
                NodeType.ForStatement or NodeType.ForInStatement or NodeType.ForOfStatement)
            {
                scope = new Scope(scope, testFile, function: false);
            }
            if (node is ClassExpression expression && expression.Id is not null)
            {
                scope = new Scope(scope, testFile, function: false);
                scope.Declare(expression.Id.Name, Api.Unknown);
            }
            nodes.Add((node, scope));
            if (node is CatchClause handler && handler.Param is not null) DeclarePattern(handler.Param, scope);
            if (node is ClassDeclaration type && type.Id is not null) scope.Declare(type.Id.Name, Api.Unknown);
            if (node is VariableDeclaration variables)
            {
                Scope target = variables.Kind.ToString() == "Var" ? scope.FunctionScope : scope;
                foreach (VariableDeclarator variable in variables.Declarations) DeclarePattern(variable.Id, target);
            }
            if (node is ImportDeclaration import)
            {
                bool framework = IsFramework(import.Source.Value);
                foreach (ImportDeclarationSpecifier specifier in import.Specifiers)
                {
                    switch (specifier)
                    {
                        case ImportSpecifier named:
                            scope.Declare(named.Local.Name, framework ? NameApi(Name(named.Imported)) : Api.Unknown);
                            break;
                        case ImportNamespaceSpecifier space:
                            scope.Declare(space.Local.Name, framework ? Api.Namespace : Api.Unknown);
                            break;
                        case ImportDefaultSpecifier primary:
                            scope.Declare(primary.Local.Name,
                                import.Source.Value is "ava" or "node:test" ? Api.Case : Api.Unknown);
                            break;
                    }
                }
            }
            foreach (Node child in node.ChildNodes) pending.Push((child, scope));
        }

        foreach (var (node, scope) in nodes)
        {
            if (node is not VariableDeclaration variables || scope.IsDeclared("require")) continue;
            Scope target = variables.Kind.ToString() == "Var" ? scope.FunctionScope : scope;
            foreach (VariableDeclarator variable in variables.Declarations)
            {
                if (variable.Init is not CallExpression { Callee: Identifier { Name: "require" } } require ||
                    require.Arguments.Count != 1 || require.Arguments[0] is not StringLiteral module || !IsFramework(module.Value)) continue;
                if (variable.Id is Identifier name)
                    target.Declare(name.Name, module.Value is "node:test" or "ava" ? Api.Case : Api.Namespace);
                else if (variable.Id is ObjectPattern pattern)
                    foreach (Node property in pattern.Properties)
                        if (property is Property { Computed: false, Value: Identifier local } named)
                            target.Declare(local.Name, NameApi(Name(named.Key)));
            }
        }

        // Reassignments make the binding ambiguous for the whole lexical scope.
        foreach (var (node, scope) in nodes)
        {
            if (node is AssignmentExpression assignment && assignment.Left is Identifier or ObjectPattern or ArrayPattern)
                VisitPattern(assignment.Left, scope.Invalidate);
            if (node is UpdateExpression { Argument: Identifier updated }) scope.Invalidate(updated.Name);
        }
        foreach (var (node, scope) in nodes)
        {
            if (node is not CallExpression call || IsEach(call.Callee)) continue;
            Api api = Resolve(call.Callee, scope);
            Count(api, call.Location.Start.Line, metrics);
        }
    }

    private static Api Resolve(Expression expression, Scope scope, int depth = 0)
    {
        if (depth > 12) return Api.Unknown;
        if (expression is Identifier identifier) return scope.Lookup(identifier.Name);
        if (expression is MemberExpression { Computed: false, Property: Identifier member } access)
        {
            Api receiver = Resolve(access.Object, scope, depth + 1);
            if (receiver == Api.Namespace) return NameApi(member.Name);
            if (receiver == Api.Case && member.Name == "describe") return Api.Suite;
            if (receiver is Api.Case or Api.Suite && IsModifier(member.Name)) return receiver;
            if (receiver == Api.Mock && member.Name is "fn" or "mock" or "spyOn" or "stub") return Api.Mock;
        }
        if (expression is CallExpression builder && IsEach(builder.Callee))
            return Resolve(builder.Callee, scope, depth + 1);
        if (expression is TaggedTemplateExpression template && IsEach(template.Tag))
            return Resolve(template.Tag, scope, depth + 1);
        return Api.Unknown;
    }

    private static bool IsEach(Expression expression) => expression is
        MemberExpression { Computed: false, Property: Identifier { Name: "each" } };

    internal static bool IsModifier(string name) => name is
        "only" or "skip" or "todo" or "concurrent" or "sequential" or "fails" or "each";

    internal static bool IsFramework(string? name) => name is
        "vitest" or "@jest/globals" or "mocha" or "jasmine" or "@playwright/test" or "node:test" or "ava";

    internal static Api NameApi(string? name) => name switch
    {
        "test" or "it" or "specify" => Api.Case,
        "describe" or "suite" or "context" => Api.Suite,
        "expect" or "assert" or "assertThat" => Api.Assertion,
        "jest" or "vi" or "mock" or "spyOn" or "stub" => Api.Mock,
        _ => Api.Unknown,
    };

    internal static void Count(Api api, int line, JavaScriptSourceMetrics metrics)
    {
        switch (api)
        {
            case Api.Case: metrics.TestCases++; break;
            case Api.Suite: metrics.TestSuites++; break;
            case Api.Assertion: metrics.Assertions++; break;
            case Api.Mock: metrics.MockUsages++; break;
            default: return;
        }
        metrics.TestLine = Math.Min(metrics.TestLine ?? line, line);
    }

    private static string? Name(Expression expression) => expression switch
    {
        Identifier identifier => identifier.Name,
        StringLiteral literal => literal.Value,
        _ => null,
    };

    private static void DeclarePattern(Node pattern, Scope scope) =>
        VisitPattern(pattern, name => scope.Declare(name, Api.Unknown));

    private static void VisitPattern(Node pattern, Action<string> visit)
    {
        Stack<Node> pending = new();
        pending.Push(pattern);
        while (pending.TryPop(out Node? node))
        {
            if (node is Identifier identifier) visit(identifier.Name);
            else if (node is AssignmentPattern assignment) pending.Push(assignment.Left);
            else if (node is Property property) pending.Push(property.Value);
            else foreach (Node child in node.ChildNodes) pending.Push(child);
        }
    }

    private sealed class Scope(Scope? parent, bool testFile, bool function)
    {
        private readonly Dictionary<string, Api> _bindings = new(StringComparer.Ordinal);
        public bool IsDeclared(string name) => _bindings.ContainsKey(name) || parent?.IsDeclared(name) == true;
        public Scope FunctionScope => function || parent is null ? this : parent.FunctionScope;
        public void Declare(string name, Api api) => _bindings[name] = api;
        public Api Lookup(string name) => _bindings.TryGetValue(name, out Api api)
            ? api : parent?.Lookup(name) ?? (testFile ? NameApi(name) : Api.Unknown);
        public void Invalidate(string name)
        {
            if (_bindings.ContainsKey(name) || parent is null) _bindings[name] = Api.Unknown;
            else parent.Invalidate(name);
        }
    }
}
