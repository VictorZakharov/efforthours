namespace EffortHours.Analyzers.Php;

internal static class PhpSemanticAnalyzer
{
    public static void Analyze(
        IReadOnlyList<PhpToken> tokens,
        PhpImportContext imports,
        PhpPackageModel package,
        string path,
        PhpSourceMetrics metrics)
    {
        HashSet<string> packageTechnologies = [.. package.Dependencies
            .Select(dependency => PhpTechnologyCatalog.FromDependency(dependency.Name))
            .Where(technology => technology is not null)
            .Select(technology => technology!)];
        metrics.Technologies.UnionWith(imports.Technologies);
        metrics.Technologies.UnionWith(packageTechnologies);

        bool testFile = IsTestPath(path);
        foreach (string attribute in metrics.AttributesSeen)
            ApplyAttribute(imports.ResolveType(attribute), attribute, testFile, metrics);
        foreach (string baseType in metrics.BaseTypesSeen)
            ApplyBaseType(imports.ResolveType(baseType), baseType, testFile, metrics);

        Dictionary<string, string> instances = ReadInstances(tokens, imports);
        foreach (string call in Calls(tokens))
        {
            metrics.CallsSeen.Add(call);
            string resolved = imports.ResolveCall(call, instances);
            ApplyCall(resolved.Length > 0 ? resolved : call, packageTechnologies, testFile, metrics);
        }

        ApplyPathSemantics(path, packageTechnologies, testFile, metrics);
        if (testFile && metrics.TestCases == 0) metrics.TestCases = 1;
    }

    private static void ApplyAttribute(
        string resolved,
        string raw,
        bool testFile,
        PhpSourceMetrics metrics)
    {
        string canonical = (resolved.Length > 0 ? resolved : raw).TrimStart('\\');
        string lower = canonical.ToLowerInvariant();
        string simple = SimpleName(lower);
        string? technology = PhpTechnologyCatalog.FromQualifiedName(canonical);
        if (technology is not null) metrics.Technologies.Add(technology);

        if (lower.StartsWith("symfony\\component\\routing\\", StringComparison.Ordinal) && simple == "route")
            metrics.ApiEndpoints++;
        if (lower.StartsWith("doctrine\\orm\\mapping\\", StringComparison.Ordinal) &&
            simple is "entity" or "embeddable" or "mappedsuperclass" or "table")
            metrics.DataModels++;
        if (lower.StartsWith("symfony\\component\\security\\", StringComparison.Ordinal) ||
            lower.StartsWith("sensio\\bundle\\frameworkextrabundle\\configuration\\isgranted", StringComparison.Ordinal))
            metrics.SecurityUsages++;
        if (lower.StartsWith("symfony\\component\\validator\\constraints\\", StringComparison.Ordinal))
            metrics.ValidationRules++;
        if (testFile && lower.StartsWith("phpunit\\framework\\attributes\\", StringComparison.Ordinal))
        {
            if (simple is "test" or "testwith") metrics.TestCases++;
            if (simple is "dataprovider" or "testwith") metrics.ParameterizedCases++;
        }
    }

    private static void ApplyBaseType(
        string resolved,
        string raw,
        bool testFile,
        PhpSourceMetrics metrics)
    {
        string canonical = (resolved.Length > 0 ? resolved : raw).TrimStart('\\');
        string lower = canonical.ToLowerInvariant();
        string? technology = PhpTechnologyCatalog.FromQualifiedName(canonical);
        if (technology is not null) metrics.Technologies.Add(technology);
        if (lower is "illuminate\\database\\eloquent\\model" ||
            lower.StartsWith("doctrine\\orm\\", StringComparison.Ordinal)) metrics.DataModels++;
        if (lower is "illuminate\\contracts\\queue\\shouldqueue" ||
            lower.StartsWith("symfony\\component\\messenger\\", StringComparison.Ordinal))
        {
            metrics.BackgroundUsages++;
            metrics.MessagingHandlers++;
        }
        if (lower.StartsWith("symfony\\component\\console\\command\\command", StringComparison.Ordinal) ||
            lower.StartsWith("illuminate\\console\\command", StringComparison.Ordinal)) metrics.CliCommands++;
        if (lower is "fiber" || lower.StartsWith("amp\\", StringComparison.Ordinal)) metrics.AsyncUnits++;
        if (testFile && lower.StartsWith("phpunit\\framework\\testcase", StringComparison.Ordinal))
            metrics.TestCases = Math.Max(1, metrics.TestCases);
    }

    private static void ApplyCall(
        string call,
        HashSet<string> packageTechnologies,
        bool testFile,
        PhpSourceMetrics metrics)
    {
        string lower = call.TrimStart('\\').ToLowerInvariant();
        string member = Member(lower);
        string owner = Owner(lower);
        string? technology = PhpTechnologyCatalog.FromQualifiedName(owner);
        if (technology is not null) metrics.Technologies.Add(technology);

        if (owner == "illuminate\\support\\facades\\route" &&
            member is "get" or "post" or "put" or "patch" or "delete" or "options" or "match" or "any" or "resource")
            metrics.ApiEndpoints++;

        bool persistenceOwner = owner.StartsWith("doctrine\\", StringComparison.Ordinal) ||
            owner.StartsWith("illuminate\\database\\", StringComparison.Ordinal) ||
            owner is "illuminate\\support\\facades\\db" or "illuminate\\support\\facades\\schema";
        if (persistenceOwner && member is "find" or "findall" or "persist" or "flush" or "save" or
            "delete" or "remove" or "query" or "select" or "insert" or "update" or "create" or "table")
            metrics.DataCalls++;
        if (owner == "illuminate\\support\\facades\\schema" && member is "create" or "table" or "drop" or "rename")
            metrics.Migrations++;

        if (technology is "guzzle" or "aws" or "google-cloud" or "azure" or "redis" or
            "rabbitmq" or "enqueue" ||
            owner.StartsWith("symfony\\contracts\\httpclient\\", StringComparison.Ordinal))
            metrics.IntegrationCalls++;

        if (technology == "jwt" || owner.StartsWith("symfony\\component\\security\\", StringComparison.Ordinal) ||
            owner.StartsWith("illuminate\\support\\facades\\auth", StringComparison.Ordinal) ||
            owner.StartsWith("illuminate\\support\\facades\\gate", StringComparison.Ordinal) ||
            owner.StartsWith("illuminate\\support\\facades\\hash", StringComparison.Ordinal) ||
            lower is "password_hash" or "password_verify") metrics.SecurityUsages++;

        bool queue = owner.StartsWith("illuminate\\queue\\", StringComparison.Ordinal) ||
            owner.StartsWith("illuminate\\support\\facades\\queue", StringComparison.Ordinal) ||
            owner.StartsWith("symfony\\component\\messenger\\", StringComparison.Ordinal) ||
            technology is "rabbitmq" or "enqueue";
        if (queue && member is "dispatch" or "push" or "later" or "send" or "publish")
        {
            metrics.BackgroundUsages++;
            metrics.MessagingHandlers++;
            metrics.IntegrationCalls++;
        }

        if (owner.StartsWith("symfony\\component\\validator\\", StringComparison.Ordinal) && member == "validate" ||
            owner.StartsWith("illuminate\\validation\\", StringComparison.Ordinal) && member is "validate" or "make" ||
            owner == "illuminate\\support\\facades\\validator" && member == "make")
            metrics.ValidationRules++;
        if (owner.StartsWith("symfony\\component\\console\\", StringComparison.Ordinal) &&
            member is "add" or "run" or "execute" ||
            owner.StartsWith("illuminate\\console\\", StringComparison.Ordinal) && member is "handle" or "call")
            metrics.CliCommands++;
        if (technology is "amphp" or "reactphp" || owner == "fiber") metrics.AsyncUnits++;

        bool pest = packageTechnologies.Contains("pest");
        if (testFile && pest && lower is "test" or "it") metrics.TestCases++;
        if (testFile && (member.StartsWith("assert", StringComparison.Ordinal) || pest && lower == "expect"))
            metrics.Assertions++;
        if (testFile && (technology == "mockery" || member is "createMock" or "getmockbuilder" or "mock"))
            metrics.MockUsages++;
        if (testFile && member is "with" or "dataprovider") metrics.ParameterizedCases++;
    }

    private static void ApplyPathSemantics(
        string path,
        HashSet<string> technologies,
        bool testFile,
        PhpSourceMetrics metrics)
    {
        string lower = path.ToLowerInvariant();
        if (!testFile && technologies.Contains("laravel") &&
            (lower.StartsWith("routes/", StringComparison.Ordinal) || lower.Contains("/routes/", StringComparison.Ordinal)))
            metrics.ApiTypes = Math.Max(1, metrics.ApiTypes);
        if (!testFile && technologies.Contains("laravel") &&
            (lower.StartsWith("database/migrations/", StringComparison.Ordinal) ||
             lower.Contains("/database/migrations/", StringComparison.Ordinal)))
            metrics.Migrations = Math.Max(1, metrics.Migrations);
        if (!testFile && (lower.StartsWith("app/jobs/", StringComparison.Ordinal) ||
            lower.Contains("/app/jobs/", StringComparison.Ordinal)) && technologies.Contains("laravel"))
            metrics.BackgroundUsages = Math.Max(1, metrics.BackgroundUsages);
        if (!testFile) return;
        if (lower.Contains("e2e", StringComparison.Ordinal) || lower.Contains("end-to-end", StringComparison.Ordinal))
            metrics.EndToEndTests = 1;
        else if (lower.Contains("integration", StringComparison.Ordinal)) metrics.IntegrationTests = 1;
        else if (lower.Contains("feature", StringComparison.Ordinal)) metrics.FeatureTests = 1;
    }

    private static Dictionary<string, string> ReadInstances(
        IReadOnlyList<PhpToken> tokens,
        PhpImportContext imports)
    {
        Dictionary<string, string> instances = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index + 3 < tokens.Count; index++)
        {
            if (tokens[index].Kind != PhpTokenKind.Variable || tokens[index + 1].Text != "=" ||
                !tokens[index + 2].Text.Equals("new", StringComparison.OrdinalIgnoreCase) ||
                tokens[index + 3].Kind != PhpTokenKind.Identifier) continue;
            string resolved = imports.ResolveType(tokens[index + 3].Text);
            if (resolved.Length > 0) instances[tokens[index].Text] = resolved;
        }
        return instances;
    }

    private static IEnumerable<string> Calls(IReadOnlyList<PhpToken> tokens)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == PhpTokenKind.Identifier && index + 3 < tokens.Count &&
                tokens[index + 1].Text == "::" && tokens[index + 2].Kind == PhpTokenKind.Identifier &&
                tokens[index + 3].Text == "(")
            {
                yield return tokens[index].Text + "::" + tokens[index + 2].Text;
                index += 2;
            }
            else if (tokens[index].Kind is PhpTokenKind.Identifier or PhpTokenKind.Variable &&
                index + 3 < tokens.Count && tokens[index + 1].Text is "->" or "?->" &&
                tokens[index + 2].Kind == PhpTokenKind.Identifier && tokens[index + 3].Text == "(")
            {
                yield return tokens[index].Text + "->" + tokens[index + 2].Text;
                index += 2;
            }
            else if (tokens[index].Kind == PhpTokenKind.Identifier && index + 1 < tokens.Count &&
                tokens[index + 1].Text == "(" && !IsDeclarationKeyword(tokens[index].Text))
                yield return tokens[index].Text;
        }
    }

    private static bool IsDeclarationKeyword(string value) => value.ToLowerInvariant() is
        "if" or "elseif" or "for" or "foreach" or "while" or "switch" or "catch" or
        "function" or "isset" or "empty" or "array" or "list" or "echo" or "print";

    private static string Owner(string value)
    {
        int separator = value.IndexOf("::", StringComparison.Ordinal);
        if (separator < 0) separator = value.IndexOf("->", StringComparison.Ordinal);
        return separator < 0 ? value : value[..separator];
    }

    private static string Member(string value)
    {
        int separator = value.LastIndexOf("::", StringComparison.Ordinal);
        int arrow = value.LastIndexOf("->", StringComparison.Ordinal);
        separator = Math.Max(separator, arrow);
        return separator < 0 ? value : value[(separator + 2)..];
    }

    private static string SimpleName(string value)
    {
        int separator = value.LastIndexOf('\\');
        return separator < 0 ? value : value[(separator + 1)..];
    }

    private static bool IsTestPath(string path)
    {
        string lower = path.ToLowerInvariant();
        string name = Path.GetFileName(lower);
        return lower.StartsWith("tests/", StringComparison.Ordinal) ||
            lower.Contains("/tests/", StringComparison.Ordinal) ||
            lower.Contains("/test/", StringComparison.Ordinal) ||
            name.EndsWith("test.php", StringComparison.Ordinal) || name is "pest.php" or "testcase.php";
    }
}
