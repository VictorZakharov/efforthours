namespace EffortHours.Analyzers.Python;

internal static class PythonSyntaxAnalyzer
{
    private sealed record ClassContext(int Depth, string QualifiedBase);

    public static PythonSyntaxAnalysis Analyze(string source, string path)
    {
        PythonTokenization tokenization = PythonTokenizer.Tokenize(source);
        PythonSourceMetrics metrics = new();
        Dictionary<string, string> aliases = new(StringComparer.Ordinal);
        Dictionary<string, string> instances = new(StringComparer.Ordinal);
        List<ClassContext> classes = [];
        List<int> functions = [];
        List<(int Depth, string Name)> decorators = [];
        List<PythonToken> line = [];
        int depth = 0;

        foreach (PythonToken token in tokenization.Tokens)
        {
            if (token.Kind == PythonTokenKind.Indent)
            {
                depth++;
                continue;
            }

            if (token.Kind == PythonTokenKind.Dedent)
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (token.Kind is PythonTokenKind.NewLine or PythonTokenKind.End)
            {
                if (line.Count > 0)
                {
                    AnalyzeLine(
                        line,
                        depth,
                        path,
                        aliases,
                        instances,
                        classes,
                        functions,
                        decorators,
                        metrics);
                    line.Clear();
                }

                continue;
            }

            line.Add(token);
        }

        return new PythonSyntaxAnalysis(tokenization.Confidence, metrics);
    }

    private static void AnalyzeLine(
        List<PythonToken> line,
        int depth,
        string path,
        Dictionary<string, string> aliases,
        Dictionary<string, string> instances,
        List<ClassContext> classes,
        List<int> functions,
        List<(int Depth, string Name)> decorators,
        PythonSourceMetrics metrics)
    {
        classes.RemoveAll(item => item.Depth >= depth);
        functions.RemoveAll(item => item >= depth);
        decorators.RemoveAll(item => item.Depth > depth);

        if (line[0].Text == "import")
        {
            ReadImport(line, aliases, metrics);
        }
        else if (line[0].Text == "from")
        {
            ReadFromImport(line, aliases, metrics);
        }

        if (line[0].Text == "@")
        {
            string decorator = Resolve(QualifiedName(line, 1), aliases, instances);
            if (decorator.Length > 0)
            {
                decorators.Add((depth, decorator));
                metrics.Decorators++;
                ApplySemanticName(decorator, isDecorator: true, metrics);
            }
        }

        int declaration = line[0].Text == "async" && line.Count > 1 ? 1 : 0;
        bool isAsync = declaration == 1;
        if (line[declaration].Text == "def" && line.Count > declaration + 1)
        {
            string name = line[declaration + 1].Text;
            int nearestClass = classes
                .Where(context => context.Depth < depth)
                .Select(context => context.Depth)
                .DefaultIfEmpty(-1)
                .Max();
            int nearestFunction = functions
                .Where(context => context < depth)
                .DefaultIfEmpty(-1)
                .Max();
            bool method = nearestClass > nearestFunction;
            if (method) metrics.Methods++;
            else metrics.Functions++;
            if (isAsync) metrics.AsyncUnits++;
            if (!name.StartsWith('_') && (method || nearestFunction < 0)) metrics.PublicSymbols++;
            if (name.StartsWith("test_", StringComparison.Ordinal)) metrics.TestCases++;
            metrics.TypeAnnotations += line.Count(token => token.Text is ":" or "->");
            functions.Add(depth);
            decorators.RemoveAll(item => item.Depth == depth);
        }
        else if (line[0].Text == "class" && line.Count > 1)
        {
            string name = line[1].Text;
            string baseName = Resolve(FirstBaseName(line), aliases, instances);
            metrics.Classes++;
            if (!name.StartsWith('_')) metrics.PublicSymbols++;
            classes.Add(new ClassContext(depth, baseName));
            ApplyClassBase(baseName, metrics);
            decorators.RemoveAll(item => item.Depth == depth);
        }

        metrics.BranchPoints += line.Count(token => token.Text is
            "if" or "elif" or "for" or "while" or "try" or "except" or "match" or "case" or
            "and" or "or");
        metrics.Assertions += line.Count(token => token.Text == "assert");

        string assignmentTarget = AssignmentTarget(line);
        foreach (string call in line[0].Text == "@" ? [] : Calls(line))
        {
            string resolved = Resolve(call, aliases, instances);
            metrics.Calls++;
            ApplySemanticName(resolved, isDecorator: false, metrics);
            if (assignmentTarget.Length > 0)
            {
                instances[assignmentTarget] = resolved;
                assignmentTarget = string.Empty;
            }
        }

        if (IsMainGuard(line) || path.EndsWith("/__main__.py", StringComparison.Ordinal) ||
            path.Equals("__main__.py", StringComparison.Ordinal))
        {
            metrics.EntryPoints = Math.Max(1, metrics.EntryPoints);
        }

        if (line.Any(token => token.Text == "__all__"))
        {
            metrics.PublicSymbols += line.Count(token => token.Kind == PythonTokenKind.String);
        }
    }

    private static void ReadImport(
        List<PythonToken> line,
        Dictionary<string, string> aliases,
        PythonSourceMetrics metrics)
    {
        int index = 1;
        while (index < line.Count)
        {
            string module = QualifiedName(line, index);
            if (module.Length == 0) break;
            index += NameTokenLength(line, index);
            string alias = module.Split('.')[0];
            if (index + 1 < line.Count && line[index].Text == "as")
            {
                alias = line[index + 1].Text;
                index += 2;
            }

            aliases[alias] = module;
            AddImport(module, metrics);
            while (index < line.Count && line[index].Text != ",") index++;
            if (index < line.Count) index++;
        }
    }

    private static void ReadFromImport(
        List<PythonToken> line,
        Dictionary<string, string> aliases,
        PythonSourceMetrics metrics)
    {
        int importIndex = line.ToList().FindIndex(token => token.Text == "import");
        if (importIndex < 2) return;
        string module = string.Concat(line.Skip(1).Take(importIndex - 1).Select(token => token.Text));
        AddImport(module, metrics);
        int index = importIndex + 1;
        while (index < line.Count)
        {
            if (line[index].Kind != PythonTokenKind.Identifier)
            {
                index++;
                continue;
            }

            string imported = line[index].Text;
            string alias = imported;
            index++;
            if (index + 1 < line.Count && line[index].Text == "as")
            {
                alias = line[index + 1].Text;
                index += 2;
            }

            aliases[alias] = module.TrimStart('.') + "." + imported;
        }
    }

    private static void AddImport(string module, PythonSourceMetrics metrics)
    {
        if (module.Length == 0) return;
        if (metrics.ImportsSeen.Add(module)) metrics.Imports++;
        if (module.StartsWith('.')) metrics.InternalImports++;
        ApplyTechnology(module.TrimStart('.'), metrics);
    }

    private static IEnumerable<string> Calls(List<PythonToken> line)
    {
        for (int index = 0; index + 1 < line.Count; index++)
        {
            if (line[index].Kind != PythonTokenKind.Identifier) continue;
            string name = QualifiedName(line, index);
            int length = NameTokenLength(line, index);
            if (index + length < line.Count && line[index + length].Text == "(")
            {
                yield return name;
            }

            index += Math.Max(0, length - 1);
        }
    }

    private static string QualifiedName(List<PythonToken> line, int start)
    {
        if (start >= line.Count || line[start].Kind != PythonTokenKind.Identifier) return string.Empty;
        List<string> parts = [line[start].Text];
        int index = start + 1;
        while (index + 1 < line.Count && line[index].Text == "." &&
            line[index + 1].Kind == PythonTokenKind.Identifier)
        {
            parts.Add(line[index + 1].Text);
            index += 2;
        }

        return string.Join('.', parts);
    }

    private static int NameTokenLength(List<PythonToken> line, int start)
    {
        int index = start + 1;
        while (index + 1 < line.Count && line[index].Text == "." &&
            line[index + 1].Kind == PythonTokenKind.Identifier)
        {
            index += 2;
        }

        return index - start;
    }

    private static string Resolve(
        string name,
        Dictionary<string, string> aliases,
        Dictionary<string, string> instances)
    {
        if (name.Length == 0) return name;
        int separator = name.IndexOf('.');
        string root = separator < 0 ? name : name[..separator];
        string suffix = separator < 0 ? string.Empty : name[separator..];
        if (instances.TryGetValue(root, out string? instance)) return instance + suffix;
        return aliases.TryGetValue(root, out string? import) ? import + suffix : name;
    }

    private static string FirstBaseName(List<PythonToken> line)
    {
        int open = line.ToList().FindIndex(token => token.Text == "(");
        return open < 0 ? string.Empty : QualifiedName(line, open + 1);
    }

    private static string AssignmentTarget(List<PythonToken> line) =>
        line.Count > 2 && line[0].Kind == PythonTokenKind.Identifier && line[1].Text == "="
            ? line[0].Text
            : string.Empty;

    private static bool IsMainGuard(List<PythonToken> line) =>
        line.Count >= 4 && line[0].Text == "if" && line[1].Text == "__name__" &&
        line[2].Text == "==" && line[3].Kind == PythonTokenKind.String &&
        line[3].Text.Contains("__main__", StringComparison.Ordinal);

    private static void ApplyClassBase(string name, PythonSourceMetrics metrics)
    {
        string lower = name.ToLowerInvariant();
        if (metrics.Technologies.Contains("unittest") &&
            lower.Contains("unittest.testcase", StringComparison.Ordinal))
            metrics.Technologies.Add("unittest");
        if (metrics.Technologies.Contains("pydantic") &&
            lower.Contains("pydantic.basemodel", StringComparison.Ordinal))
        {
            metrics.ValidationTypes++;
            metrics.Technologies.Add("pydantic");
        }
        if (metrics.Technologies.Contains("sqlalchemy") &&
                lower.Contains("sqlalchemy", StringComparison.Ordinal) ||
            metrics.Technologies.Contains("django") &&
                lower.Contains("django.db.models.model", StringComparison.Ordinal))
        {
            metrics.DataModels++;
        }
        if (metrics.Technologies.Contains("fastapi") && lower.Contains("fastapi", StringComparison.Ordinal) ||
            metrics.Technologies.Contains("flask") && lower.Contains("flask", StringComparison.Ordinal) ||
            metrics.Technologies.Contains("django") && lower.Contains("django.views", StringComparison.Ordinal))
        {
            metrics.ApiTypes++;
        }
    }

    private static void ApplySemanticName(
        string name,
        bool isDecorator,
        PythonSourceMetrics metrics)
    {
        string lower = name.ToLowerInvariant();
        if (HasAny(metrics, "fastapi", "flask", "django") && IsApiEndpoint(lower, isDecorator))
            metrics.ApiEndpoints++;
        if (HasAny(metrics, "argparse", "click", "typer") && IsCliCommand(lower, isDecorator))
            metrics.CliCommands++;
        if (HasAny(metrics, "sqlalchemy", "django") &&
            (lower.StartsWith("sqlalchemy.", StringComparison.Ordinal) ||
             lower.StartsWith("django.db.", StringComparison.Ordinal))) metrics.DataCalls++;
        if (metrics.Technologies.Contains("alembic") &&
            lower.StartsWith("alembic.", StringComparison.Ordinal))
        {
            metrics.DataCalls++;
            metrics.Migrations = 1;
        }
        if (HasAny(metrics, "requests", "httpx", "boto3", "google", "azure", "openai") &&
            StartsWithAny(lower, "requests.", "httpx.", "boto3.", "botocore.",
                "google.cloud.", "azure.", "openai.")) metrics.IntegrationCalls++;
        if (HasAny(metrics, "jose", "passlib", "bcrypt", "fastapi", "django") &&
            StartsWithAny(lower, "jwt.", "pyjwt.", "jose.", "passlib.", "bcrypt.",
                "fastapi.security.", "django.contrib.auth.")) metrics.SecurityUsages++;
        if (HasAny(metrics, "celery", "rq") && StartsWithAny(lower, "celery.", "rq."))
            metrics.BackgroundUsages++;
        if (HasAny(metrics, "pydantic", "marshmallow") &&
            StartsWithAny(lower, "pydantic.", "marshmallow.")) metrics.ValidationRules++;
        if (metrics.Technologies.Contains("pytest") &&
            lower.Contains("pytest.mark.parametrize", StringComparison.Ordinal)) metrics.ParameterizedCases++;
        if (metrics.Technologies.Contains("pytest") &&
            lower.Contains("pytest.fixture", StringComparison.Ordinal)) metrics.Technologies.Add("pytest");
        if (metrics.Technologies.Contains("unittest") &&
            (lower.Contains("unittest.mock", StringComparison.Ordinal) ||
            lower.EndsWith(".mock", StringComparison.Ordinal) ||
            lower.EndsWith(".patch", StringComparison.Ordinal))) metrics.MockUsages++;
        if (HasAny(metrics, "numpy", "pandas", "polars", "scipy", "sklearn", "tensorflow", "torch") &&
            StartsWithAny(lower, "numpy.", "pandas.", "polars.", "scipy.", "sklearn.", "tensorflow.", "torch."))
            metrics.DataAnalysisCalls++;
        if (HasAny(metrics, "matplotlib", "seaborn", "plotly", "bokeh", "altair") &&
            StartsWithAny(lower, "matplotlib.", "seaborn.", "plotly.", "bokeh.", "altair."))
            metrics.VisualizationCalls++;
    }

    private static bool IsApiEndpoint(string name, bool decorator) =>
        decorator && (StartsWithAny(name, "fastapi.fastapi.", "fastapi.apirouter.",
            "flask.flask.", "flask.blueprint.") &&
            EndsWithAny(name, ".get", ".post", ".put", ".delete", ".patch", ".route",
                ".websocket", ".api_route")) ||
        StartsWithAny(name, "django.urls.path", "django.urls.re_path");

    private static bool IsCliCommand(string name, bool decorator) =>
        StartsWithAny(name, "argparse.argumentparser", "argparse.argumentparser.add_argument") ||
        decorator && StartsWithAny(name, "click.command", "click.group", "typer.typer.command",
            "typer.typer.callback");

    private static void ApplyTechnology(string name, PythonSourceMetrics metrics)
    {
        string lower = name.ToLowerInvariant();
        foreach (string technology in new[]
        {
            "fastapi", "flask", "django", "sqlalchemy", "alembic", "requests", "httpx",
            "boto3", "celery", "rq", "argparse", "click", "typer", "pytest", "unittest",
            "pydantic", "marshmallow", "passlib", "bcrypt", "jose", "google", "azure", "openai",
            "numpy", "pandas", "polars", "scipy", "sklearn", "tensorflow", "torch",
            "matplotlib", "seaborn", "plotly", "bokeh", "altair",
        })
        {
            if (lower == technology || lower.StartsWith(technology + ".", StringComparison.Ordinal))
                metrics.Technologies.Add(technology);
        }
    }

    private static bool StartsWithAny(string value, params string[] prefixes) =>
        prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));

    private static bool EndsWithAny(string value, params string[] suffixes) =>
        suffixes.Any(suffix => value.EndsWith(suffix, StringComparison.Ordinal));

    private static bool HasAny(PythonSourceMetrics metrics, params string[] technologies) =>
        technologies.Any(metrics.Technologies.Contains);
}
