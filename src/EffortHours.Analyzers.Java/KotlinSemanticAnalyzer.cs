namespace EffortHours.Analyzers.Java;

internal static class KotlinSemanticAnalyzer
{
    public static void Analyze(
        IReadOnlyList<KotlinToken> tokens,
        KotlinImportContext imports,
        string path,
        KotlinSourceMetrics metrics)
    {
        bool testFile = IsTestPath(path);
        foreach (string annotation in metrics.AnnotationsSeen)
            ApplyAnnotation(annotation, imports, testFile, metrics);
        foreach (string baseType in metrics.BaseTypesSeen)
            ApplyBaseType(baseType, imports, metrics);

        Dictionary<string, string> instances = ReadInstances(tokens, imports);
        foreach (string call in Calls(tokens))
        {
            string resolved = imports.ResolveCall(call, instances);
            if (resolved.Length > 0) ApplyCall(resolved, testFile, metrics);
        }

        CountQualifiedTypes(tokens, imports, metrics);
        if (testFile && metrics.TestCases == 0 &&
            imports.Technologies.Any(technology => technology is "kotlin-test" or "junit" or "kotest"))
            metrics.TestCases = 1;
        if (testFile && imports.Technologies.Contains("testcontainers")) metrics.IntegrationTests = 1;
        ClassifyTestPath(path, metrics);
    }

    private static void ApplyAnnotation(
        string annotation,
        KotlinImportContext imports,
        bool testFile,
        KotlinSourceMetrics metrics)
    {
        string simple = SimpleName(annotation);
        string? technology = ResolveTechnology(annotation, imports);
        if (technology is not null) metrics.Technologies.Add(technology);
        if (technology == "spring-web")
        {
            if (simple is "RestController" or "Controller") metrics.ApiTypes++;
            if (simple is "RequestMapping" or "GetMapping" or "PostMapping" or
                "PutMapping" or "DeleteMapping" or "PatchMapping") metrics.ApiEndpoints++;
        }
        else if (technology == "jakarta-rest")
        {
            if (simple is "Path" or "GET" or "POST" or "PUT" or "DELETE" or "PATCH")
                metrics.ApiEndpoints++;
            if (simple == "ApplicationPath") metrics.ApiTypes++;
        }
        else if (technology is "jakarta-persistence" or "spring-data" or "android-room")
        {
            if (simple is "Entity" or "Table" or "Embeddable" or "MappedSuperclass" or
                "Dao" or "Database") metrics.DataModels++;
            if (simple is "Query" or "Insert" or "Update" or "Delete" or "Transaction")
                metrics.DataCalls++;
        }
        else if (technology is "spring-security" or "jakarta-security" or "android-security")
        {
            if (simple is "PreAuthorize" or "PostAuthorize" or "Secured" or "RolesAllowed" or
                "PermitAll" or "DenyAll" or "EnableWebSecurity" or "RequiresApi")
                metrics.SecurityUsages++;
        }
        else if (technology == "android-compose" && simple == "Composable")
            metrics.UiSurfaces++;
        else if (technology is "spring-scheduling" or "quartz" or "spring-batch" or "android-work")
        {
            if (simple is "Scheduled" or "Async" or "EnableScheduling" or "StepScope" or
                "JobScope" or "HiltWorker") metrics.BackgroundUsages++;
        }
        else if (technology == "jakarta-validation" && simple is
            "Valid" or "Validated" or "NotNull" or "NotBlank" or "NotEmpty" or
            "Size" or "Min" or "Max" or "Pattern" or "Email" or "Positive" or "Negative")
            metrics.ValidationRules++;

        if (!testFile) return;
        if (technology is "junit" or "kotlin-test" or "kotest" && simple is
            "Test" or "TestFactory" or "RepeatedTest" or "ParameterizedTest")
            metrics.TestCases++;
        if (technology == "junit" && simple is
            "ParameterizedTest" or "MethodSource" or "ValueSource" or "CsvSource")
            metrics.ParameterizedCases++;
        if (technology is "mockk" or "mockito" && simple is "Mock" or "Spy" or "InjectMocks")
            metrics.MockUsages++;
    }

    private static void ApplyBaseType(
        string baseType,
        KotlinImportContext imports,
        KotlinSourceMetrics metrics)
    {
        string resolved = imports.ResolveType(baseType);
        string? technology = KotlinImportAnalysis.Technology(resolved);
        if (technology is not null) metrics.Technologies.Add(technology);
        string simple = SimpleName(baseType);
        if (technology == "spring-data" && simple is
            "Repository" or "CrudRepository" or "JpaRepository" or "PagingAndSortingRepository")
        {
            metrics.DataModels++;
            metrics.DataCalls++;
        }
        if (technology == "android-room" && simple == "RoomDatabase") metrics.DataModels++;
        if (technology is "android-activity" or "android-fragment" or "android-lifecycle" or
            "android-component" or "android-work")
        {
            if (simple is "Activity" or "ComponentActivity" or "AppCompatActivity" or "Fragment" or
                "ViewModel" or "Service" or "BroadcastReceiver" or "Worker" or "CoroutineWorker")
                metrics.AndroidComponents++;
            if (simple is "Worker" or "CoroutineWorker" or "Service") metrics.BackgroundUsages++;
        }
        if (technology is "flyway" or "liquibase") metrics.Migrations++;
    }

    private static void ApplyCall(string resolved, bool testFile, KotlinSourceMetrics metrics)
    {
        string member = resolved[(resolved.LastIndexOf('.') + 1)..].ToLowerInvariant();
        string? technology = KotlinImportAnalysis.Technology(resolved);
        if (technology is not null) metrics.Technologies.Add(technology);
        if (technology == "ktor-server" && member is
            "routing" or "route" or "get" or "post" or "put" or "delete" or "patch")
            metrics.ApiEndpoints++;
        if ((technology is "jdbc" or "spring-data" or "jakarta-persistence" or "exposed" or
                "android-room") && member is
            "query" or "update" or "execute" or "persist" or "merge" or "find" or "save" or
            "delete" or "select" or "insert" or "insertandgetid" or "transaction")
            metrics.DataCalls++;
        if (technology is "flyway" or "liquibase")
        {
            metrics.DataCalls++;
            metrics.Migrations = Math.Max(1, metrics.Migrations);
        }
        if (technology is "ktor-client" or "java-http" or "okhttp" or "retrofit" or "grpc" or
            "aws" or "google-cloud" or "azure" or "kafka" or "spring-amqp" or "jms")
            metrics.IntegrationCalls++;
        if (technology is "spring-security" or "jakarta-security" or "jwt" or "android-security")
            metrics.SecurityUsages++;
        if (technology is "spring-scheduling" or "quartz" or "spring-batch" or "android-work")
            metrics.BackgroundUsages++;
        if (technology == "jakarta-validation" && member == "validate") metrics.ValidationRules++;
        if (technology is "clikt" or "picocli" && member is
            "main" or "parse" or "execute" or "subcommands") metrics.CliCommands++;
        if (technology is "kotlin-coroutines" or "kotlin-flow")
        {
            if (member is "launch" or "async" or "withcontext" or "coroutinescope" or
                "supervisorscope" or "produce")
            {
                metrics.CoroutineUsages++;
                metrics.AsyncUnits++;
            }
            if (member is "flow" or "flowof" or "stateflow" or "sharedflow" or "callbackflow" or
                "map" or "filter" or "collect" or "combine") metrics.FlowUsages++;
            if (member is "launch" or "async" or "produce") metrics.BackgroundUsages++;
        }
        if (technology == "android-compose" && member is
            "setcontent" or "text" or "button" or "textfield" or "lazycolumn" or "navhost")
            metrics.UiSurfaces++;

        if (!testFile) return;
        if (technology is "junit" or "kotlin-test" or "kotest" &&
            (member.StartsWith("assert", StringComparison.Ordinal) || member is "fail" or "shouldbe"))
            metrics.Assertions++;
        if (technology is "mockk" or "mockito" && member is
            "mockk" or "spyk" or "every" or "verify" or "mock" or "spy" or "when")
            metrics.MockUsages++;
    }

    private static Dictionary<string, string> ReadInstances(
        IReadOnlyList<KotlinToken> tokens,
        KotlinImportContext imports)
    {
        Dictionary<string, string> instances = new(StringComparer.Ordinal);
        for (int index = 0; index + 3 < tokens.Count; index++)
        {
            if (tokens[index].Kind == KotlinTokenKind.Identifier && tokens[index + 1].Text == ":")
            {
                string declared = KotlinTokenUtilities.QualifiedName(tokens, index + 2);
                string resolved = imports.ResolveType(declared);
                if (resolved.Length > 0) instances[tokens[index].Text] = resolved;
            }
            if (tokens[index].Text is not ("val" or "var") ||
                tokens[index + 1].Kind != KotlinTokenKind.Identifier) continue;
            string variable = tokens[index + 1].Text;
            if (tokens[index + 2].Text == ":")
            {
                string declared = KotlinTokenUtilities.QualifiedName(tokens, index + 3);
                string resolved = imports.ResolveType(declared);
                if (resolved.Length > 0) instances[variable] = resolved;
            }
            int equals = FindOnLine(tokens, index + 2, "=");
            if (equals >= 0 && equals + 1 < tokens.Count)
            {
                string created = KotlinTokenUtilities.QualifiedName(tokens, equals + 1);
                string resolved = imports.ResolveType(created);
                if (resolved.Length > 0) instances[variable] = resolved;
            }
        }

        return instances;
    }

    private static IEnumerable<string> Calls(IReadOnlyList<KotlinToken> tokens)
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Kind != KotlinTokenKind.Identifier && tokens[index].Text is not ("get" or "set") ||
                index > 0 && tokens[index - 1].Text == ".") continue;
            bool keywordCall = tokens[index].Kind != KotlinTokenKind.Identifier;
            string name = keywordCall
                ? tokens[index].Text
                : KotlinTokenUtilities.QualifiedName(tokens, index);
            int length = keywordCall ? 1 : KotlinTokenUtilities.QualifiedNameLength(tokens, index);
            int callIndex = index + length;
            if (callIndex < tokens.Count && tokens[callIndex].Text == "<")
            {
                int close = FindGenericClose(tokens, callIndex);
                if (close > callIndex) callIndex = close + 1;
            }
            if (callIndex < tokens.Count && tokens[callIndex].Text is "(" or "{") yield return name;
            index += Math.Max(0, length - 1);
        }
    }

    private static int FindGenericClose(IReadOnlyList<KotlinToken> tokens, int start)
    {
        int depth = 0;
        for (int index = start; index < tokens.Count; index++)
        {
            depth += tokens[index].Text switch
            {
                "<" => 1,
                ">" => -1,
                ">>" => -2,
                ">>>" => -3,
                _ => 0,
            };
            if (depth <= 0) return index;
        }
        return -1;
    }

    private static void CountQualifiedTypes(
        IReadOnlyList<KotlinToken> tokens,
        KotlinImportContext imports,
        KotlinSourceMetrics metrics)
    {
        foreach (KotlinToken token in tokens.Where(token => token.Kind == KotlinTokenKind.Identifier))
        {
            string resolved = imports.ResolveType(token.Text);
            string? technology = KotlinImportAnalysis.Technology(resolved);
            if (technology is null) continue;
            metrics.Technologies.Add(technology);
            if (technology == "kotlin-flow") metrics.FlowUsages++;
            if (technology == "kotlin-coroutines" && token.Text is "CoroutineScope" or "Job" or "Deferred")
                metrics.CoroutineUsages++;
        }
    }

    private static string? ResolveTechnology(string name, KotlinImportContext imports)
    {
        string resolved = imports.ResolveType(name);
        if (resolved.Length > 0) return KotlinImportAnalysis.Technology(resolved);
        return name.Contains('.') ? KotlinImportAnalysis.Technology(name) : null;
    }

    private static int FindOnLine(IReadOnlyList<KotlinToken> tokens, int start, string value)
    {
        int line = tokens[start].Line;
        for (int index = start; index < tokens.Count && tokens[index].Line == line; index++)
            if (tokens[index].Text == value) return index;
        return -1;
    }

    private static string SimpleName(string value)
    {
        int separator = value.LastIndexOf('.');
        return separator < 0 ? value : value[(separator + 1)..];
    }

    private static bool IsTestPath(string path)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        return normalized.Contains("/src/test/", StringComparison.Ordinal) ||
            normalized.Contains("/src/androidtest/", StringComparison.Ordinal) ||
            normalized.Contains("/test/", StringComparison.Ordinal) ||
            normalized.StartsWith("test/", StringComparison.Ordinal) ||
            KotlinFileNameIsTest(Path.GetFileName(path));
    }

    private static bool KotlinFileNameIsTest(string name) =>
        name.EndsWith("Test.kt", StringComparison.Ordinal) ||
        name.EndsWith("Tests.kt", StringComparison.Ordinal) ||
        name.EndsWith("TestCase.kt", StringComparison.Ordinal) ||
        name.EndsWith("IT.kt", StringComparison.Ordinal) ||
        name.EndsWith("Test.kts", StringComparison.Ordinal) ||
        name.EndsWith("Tests.kts", StringComparison.Ordinal) ||
        name.EndsWith("TestCase.kts", StringComparison.Ordinal) ||
        name.EndsWith("IT.kts", StringComparison.Ordinal);

    private static void ClassifyTestPath(string path, KotlinSourceMetrics metrics)
    {
        if (!IsTestPath(path)) return;
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal) ||
            lower.Contains("endtoend", StringComparison.Ordinal) ||
            lower.Contains("end-to-end", StringComparison.Ordinal)) metrics.EndToEndTests = 1;
        else if (lower.Contains("component", StringComparison.Ordinal) ||
            lower.Contains("androidtest", StringComparison.Ordinal)) metrics.ComponentTests = 1;
        else if (lower.Contains("integration", StringComparison.Ordinal) ||
            Path.GetFileName(path).EndsWith("IT.kt", StringComparison.Ordinal)) metrics.IntegrationTests = 1;
    }
}
