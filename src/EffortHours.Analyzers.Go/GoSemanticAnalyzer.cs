namespace EffortHours.Analyzers.Go;

internal static class GoSemanticAnalyzer
{
    public static void Analyze(
        IReadOnlyList<GoToken> tokens,
        IReadOnlyDictionary<string, string> aliases,
        string path,
        GoSourceMetrics metrics)
    {
        Dictionary<string, string> instances = ReadInstances(tokens, aliases);
        ReadTypedInstances(tokens, aliases, instances);
        foreach (string call in Calls(tokens))
        {
            string resolved = Resolve(call, aliases, instances);
            if (resolved.Length == 0) continue;
            string? technology = Technology(resolved, aliases);
            ApplyCall(resolved.ToLowerInvariant(), technology, path, metrics);
        }

        if (metrics.Technologies.Contains("cobra") && HasTokenSequence(tokens, "cobra", ".", "Command"))
            metrics.CliCommands++;
        if (metrics.Technologies.Contains("validator") && HasTokenSequence(tokens, "validate", ".", "Struct"))
            metrics.ValidationRules++;
        if (metrics.Technologies.Contains("gomock")) metrics.MockUsages++;
    }

    private static Dictionary<string, string> ReadInstances(
        IReadOnlyList<GoToken> tokens,
        IReadOnlyDictionary<string, string> aliases)
    {
        Dictionary<string, string> instances = new(StringComparer.Ordinal);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != GoTokenKind.Identifier) continue;
            string target = tokens[index].Text;
            int assignment = index + 1;
            while (assignment < tokens.Count && tokens[assignment].Line == tokens[index].Line &&
                tokens[assignment].Text is not (":=" or "=")) assignment++;
            if (assignment >= tokens.Count || tokens[assignment].Line != tokens[index].Line) continue;
            int value = assignment + 1;
            while (value < tokens.Count && tokens[value].Text is "&" or "*") value++;
            string name = GoTokenUtilities.QualifiedName(tokens, value);
            string resolved = Resolve(name, aliases, instances);
            if (resolved.Length > 0) instances[target] = resolved;
        }

        return instances;
    }

    private static void ReadTypedInstances(
        IReadOnlyList<GoToken> tokens,
        IReadOnlyDictionary<string, string> aliases,
        Dictionary<string, string> instances)
    {
        for (int index = 0; index + 3 < tokens.Count; index++)
        {
            if (tokens[index].Kind != GoTokenKind.Identifier) continue;
            int type = index + 1;
            if (tokens[type].Text == "*") type++;
            string name = GoTokenUtilities.QualifiedName(tokens, type);
            string resolved = Resolve(name, aliases, instances);
            if (resolved.Length > 0) instances[tokens[index].Text] = resolved;
        }
    }

    private static IEnumerable<string> Calls(IReadOnlyList<GoToken> tokens)
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Kind != GoTokenKind.Identifier) continue;
            string name = GoTokenUtilities.QualifiedName(tokens, index);
            int length = GoTokenUtilities.QualifiedNameLength(tokens, index);
            if (index + length < tokens.Count && tokens[index + length].Text == "(") yield return name;
            index += Math.Max(0, length - 1);
        }
    }

    private static string Resolve(
        string name,
        IReadOnlyDictionary<string, string> aliases,
        Dictionary<string, string> instances)
    {
        if (name.Length == 0) return string.Empty;
        int separator = name.IndexOf('.');
        string root = separator < 0 ? name : name[..separator];
        string suffix = separator < 0 ? string.Empty : name[separator..];
        if (instances.TryGetValue(root, out string? instance)) return instance + suffix;
        return aliases.TryGetValue(root, out string? import) ? import + suffix : string.Empty;
    }

    private static string? Technology(
        string resolved,
        IReadOnlyDictionary<string, string> aliases)
    {
        string? import = aliases.Values
            .Where(value => resolved == value || resolved.StartsWith(value + ".", StringComparison.Ordinal))
            .OrderByDescending(value => value.Length)
            .FirstOrDefault();
        return import is null ? null : GoImportAnalysis.Technology(import);
    }

    private static void ApplyCall(
        string name,
        string? technology,
        string path,
        GoSourceMetrics metrics)
    {
        string member = name[(name.LastIndexOf('.') + 1)..];
        if (technology == "net/http" && member is "handle" or "handlefunc") metrics.ApiEndpoints++;
        if (technology == "net/http" && member is "newservemux" or "server") metrics.ApiTypes++;
        if (technology is "gin" or "echo" or "chi" or "gorilla/mux" &&
            member is "get" or "post" or "put" or "delete" or "patch" or "options" or "head" or "handle" or "handlefunc")
            metrics.ApiEndpoints++;
        if (technology is "flag" or "cobra" or "urfave/cli" &&
            member is "string" or "stringp" or "bool" or "int" or "var" or "addcommand" or "command")
            metrics.CliCommands++;
        if (technology is "database/sql" or "gorm" or "sqlx" or "ent" &&
            member is "open" or "query" or "querycontext" or "queryrow" or "exec" or "execcontext" or "begin" or "create" or "save" or "delete" or "find" or "scan")
            metrics.DataCalls++;
        if (technology is "gorm" or "ent" && member is "automigrate" or "schema")
        {
            metrics.DataModels++;
            metrics.Migrations++;
        }
        if (technology == "migration")
        {
            metrics.DataCalls++;
            metrics.Migrations = 1;
        }
        if (technology is "grpc" or "aws" or "google-cloud" or "azure" or "kafka" or "nats")
            metrics.IntegrationCalls++;
        if (technology == "net/http" && member is "get" or "post" or "newrequest" or "newrequestwithcontext" or "do")
            metrics.IntegrationCalls++;
        if (technology is "crypto" or "x/crypto" or "jwt" or "oauth2") metrics.SecurityUsages++;
        if (technology is "cron" or "asynq" or "temporal") metrics.BackgroundUsages++;
        if (technology == "validator") metrics.ValidationRules++;
        if (technology == "sync") metrics.SynchronizationUsages++;
        if (technology == "testify" || technology == "go-cmp" ||
            name.StartsWith("testing.t.", StringComparison.Ordinal) &&
            member is "error" or "errorf" or "fatal" or "fatalf" or "fail" or "failnow")
            metrics.Assertions++;
        if (technology is "testify" or "gomock" && member.Contains("mock", StringComparison.Ordinal))
            metrics.MockUsages++;
        if (path.EndsWith("_test.go", StringComparison.OrdinalIgnoreCase) &&
            name.StartsWith("testing.t.run", StringComparison.Ordinal)) metrics.TableDrivenCases++;
        if (name.StartsWith("errors.new", StringComparison.Ordinal) ||
            name.StartsWith("fmt.errorf", StringComparison.Ordinal)) metrics.ErrorPaths++;
    }

    private static bool HasTokenSequence(IReadOnlyList<GoToken> tokens, params string[] values)
    {
        for (int index = 0; index + values.Length <= tokens.Count; index++)
        {
            bool match = true;
            for (int offset = 0; offset < values.Length; offset++)
                match &= tokens[index + offset].Text == values[offset];
            if (match) return true;
        }

        return false;
    }
}
