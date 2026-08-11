namespace EffortHours.Analyzers.Java;

internal static class JavaSemanticAnalyzer
{
    public static void Analyze(
        IReadOnlyList<JavaToken> tokens,
        JavaImportContext imports,
        string path,
        JavaSourceMetrics metrics)
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

        if (testFile && metrics.TestCases == 0 &&
            (imports.Technologies.Contains("junit") || imports.Technologies.Contains("testng")))
            metrics.TestCases = 1;
        if (testFile && imports.Technologies.Contains("testcontainers")) metrics.IntegrationTests = 1;
        ClassifyTestPath(path, metrics);
    }

    private static void ApplyAnnotation(
        string annotation,
        JavaImportContext imports,
        bool testFile,
        JavaSourceMetrics metrics)
    {
        string simple = SimpleName(annotation);
        string? technology = AnnotationTechnology(annotation, imports);
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
        else if (technology is "jakarta-persistence" or "spring-data" or "hibernate")
        {
            if (simple is "Entity" or "Table" or "Embeddable" or "MappedSuperclass" or "Repository")
                metrics.DataModels++;
            if (simple is "Query" or "Modifying") metrics.DataCalls++;
        }
        else if (technology is "spring-security" or "jakarta-security")
        {
            if (simple is "PreAuthorize" or "PostAuthorize" or "Secured" or "RolesAllowed" or
                "PermitAll" or "DenyAll" or "EnableWebSecurity") metrics.SecurityUsages++;
        }
        else if (technology is "spring-kafka" or "spring-amqp" or "jms")
        {
            if (simple is "KafkaListener" or "RabbitListener" or "JmsListener")
            {
                metrics.MessagingHandlers++;
                metrics.IntegrationCalls++;
                metrics.BackgroundUsages++;
            }
        }
        else if (technology is "spring-scheduling" or "quartz" or "spring-batch")
        {
            if (simple is "Scheduled" or "Async" or "EnableScheduling" or "StepScope" or "JobScope")
            {
                metrics.BackgroundUsages++;
                if (simple == "Async") metrics.AsyncUnits++;
            }
        }
        else if (technology is "jakarta-validation")
        {
            if (simple is "Valid" or "Validated" or "NotNull" or "NotBlank" or "NotEmpty" or
                "Size" or "Min" or "Max" or "Pattern" or "Email" or "Positive" or "Negative")
                metrics.ValidationRules++;
        }
        else if (technology is "picocli" or "jcommander")
        {
            if (simple is "Command" or "Option" or "Parameters" or "Parameter") metrics.CliCommands++;
        }

        if (!testFile) return;
        if ((technology is "junit" or "testng") &&
            (simple is "Test" or "TestFactory" or "RepeatedTest" or "ParameterizedTest"))
            metrics.TestCases++;
        if ((technology is "junit" or "testng") &&
            (simple is "ParameterizedTest" or "MethodSource" or "ValueSource" or "CsvSource" or "DataProvider"))
            metrics.ParameterizedCases++;
        if (technology == "mockito" && (simple is "Mock" or "Spy" or "InjectMocks")) metrics.MockUsages++;
        if (technology == "spring-test")
        {
            if (simple == "SpringBootTest") metrics.IntegrationTests++;
            if (simple is "WebMvcTest" or "DataJpaTest" or "JsonTest" or "RestClientTest")
                metrics.ComponentTests++;
        }
    }

    private static void ApplyBaseType(
        string baseType,
        JavaImportContext imports,
        JavaSourceMetrics metrics)
    {
        string resolved = imports.ResolveType(baseType);
        string? technology = JavaImportAnalysis.Technology(resolved);
        if (technology is not null) metrics.Technologies.Add(technology);
        string simple = SimpleName(baseType);
        if (technology == "spring-data" &&
            simple is "Repository" or "CrudRepository" or "JpaRepository" or "PagingAndSortingRepository")
        {
            metrics.DataModels++;
            metrics.DataCalls++;
        }
        if (technology is "flyway" or "liquibase")
            metrics.Migrations++;
        if (technology is "quartz" or "spring-batch")
            metrics.BackgroundUsages++;
        if (resolved.StartsWith("java.lang.Thread", StringComparison.Ordinal) ||
            resolved.StartsWith("java.lang.Runnable", StringComparison.Ordinal) ||
            resolved.StartsWith("java.util.concurrent.Callable", StringComparison.Ordinal))
        {
            metrics.ConcurrencyUsages++;
            metrics.AsyncUnits++;
        }
    }

    private static void ApplyCall(string resolved, bool testFile, JavaSourceMetrics metrics)
    {
        string lower = resolved.ToLowerInvariant();
        string member = lower[(lower.LastIndexOf('.') + 1)..];
        string? technology = JavaImportAnalysis.Technology(resolved);
        if (technology is not null) metrics.Technologies.Add(technology);
        if (technology == "spring-web" && (member is "route" or "get" or "post" or "put" or "delete"))
            metrics.ApiEndpoints++;
        if ((technology is "jdbc" or "spring-data" or "jakarta-persistence" or "hibernate" or "mybatis" or "jooq") &&
            (member is "query" or "queryforobject" or "update" or "execute" or "persist" or "merge" or
                "find" or "save" or "delete" or "select" or "insertinto" or "createquery"))
            metrics.DataCalls++;
        if (technology is "flyway" or "liquibase")
        {
            metrics.DataCalls++;
            metrics.Migrations = Math.Max(1, metrics.Migrations);
        }
        if (technology is "java-http" or "okhttp" or "retrofit" or "openfeign" or "grpc" or
            "aws" or "google-cloud" or "azure" or "kafka" or "rabbitmq" or "spring-kafka" or
            "spring-amqp" or "jms") metrics.IntegrationCalls++;
        if (technology is "spring-security" or "jakarta-security" or "jwt" ||
            resolved.StartsWith("java.security.", StringComparison.Ordinal)) metrics.SecurityUsages++;
        if (technology is "spring-scheduling" or "quartz" or "spring-batch") metrics.BackgroundUsages++;
        if (technology == "jakarta-validation" && member == "validate") metrics.ValidationRules++;
        if ((technology is "picocli" or "jcommander") &&
            (member is "execute" or "parseargs" or "addsubcommand"))
            metrics.CliCommands++;
        if (technology == "java-concurrency" &&
            (member is "submit" or "execute" or "schedule" or
                "runasync" or "supplyasync" or "thenapply" or "thencompose"))
        {
            metrics.ConcurrencyUsages++;
            metrics.AsyncUnits++;
            if (member is "submit" or "execute" or "schedule") metrics.BackgroundUsages++;
        }

        if (!testFile) return;
        if ((technology is "junit" or "testng" or "assertj" or "hamcrest") &&
            (member.StartsWith("assert", StringComparison.Ordinal) || member is "fail" or "that"))
            metrics.Assertions++;
        if (technology == "mockito" && (member is "mock" or "spy" or "when" or "verify" or "donothing"))
            metrics.MockUsages++;
    }

    private static Dictionary<string, string> ReadInstances(
        IReadOnlyList<JavaToken> tokens,
        JavaImportContext imports)
    {
        Dictionary<string, string> instances = new(StringComparer.Ordinal);
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Kind != JavaTokenKind.Identifier) continue;
            string type = JavaTokenUtilities.QualifiedName(tokens, index);
            int typeLength = JavaTokenUtilities.QualifiedNameLength(tokens, index);
            int variableIndex = index + typeLength;
            if (variableIndex < tokens.Count && tokens[variableIndex].Kind == JavaTokenKind.Identifier)
            {
                string resolvedType = imports.ResolveType(type);
                if (resolvedType.Length > 0) instances[tokens[variableIndex].Text] = resolvedType;
            }

            if (index + 2 < tokens.Count && tokens[index + 1].Text == "=" && tokens[index + 2].Text == "new")
            {
                string created = JavaTokenUtilities.QualifiedName(tokens, index + 3);
                string resolved = imports.ResolveType(created);
                if (resolved.Length > 0) instances[tokens[index].Text] = resolved;
            }
        }

        return instances;
    }

    private static IEnumerable<string> Calls(IReadOnlyList<JavaToken> tokens)
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Kind != JavaTokenKind.Identifier ||
                index > 0 && tokens[index - 1].Text == ".") continue;
            string name = JavaTokenUtilities.QualifiedName(tokens, index);
            int length = JavaTokenUtilities.QualifiedNameLength(tokens, index);
            if (index + length < tokens.Count && tokens[index + length].Text == "(") yield return name;
            index += Math.Max(0, length - 1);
        }
    }

    private static string? AnnotationTechnology(string annotation, JavaImportContext imports)
    {
        string resolved = imports.ResolveType(annotation);
        if (resolved.Length > 0) return JavaImportAnalysis.Technology(resolved);
        if (annotation.Contains('.')) return JavaImportAnalysis.Technology(annotation);
        string[] technologies = [.. imports.WildcardPackages
            .Select(package => JavaImportAnalysis.Technology(package + "." + annotation))
            .Where(technology => technology is not null)
            .Select(technology => technology!)
            .Distinct(StringComparer.Ordinal)];
        return technologies.Length == 1 ? technologies[0] : null;
    }

    private static string SimpleName(string value)
    {
        int separator = value.LastIndexOf('.');
        return separator < 0 ? value : value[(separator + 1)..];
    }

    private static bool IsTestPath(string path)
    {
        string lower = path.ToLowerInvariant();
        string name = Path.GetFileName(path);
        return lower.Contains("/src/test/", StringComparison.Ordinal) ||
            lower.Contains("/test/", StringComparison.Ordinal) ||
            lower.StartsWith("test/", StringComparison.Ordinal) ||
            name.EndsWith("Test.java", StringComparison.Ordinal) ||
            name.EndsWith("Tests.java", StringComparison.Ordinal) ||
            name.EndsWith("TestCase.java", StringComparison.Ordinal) ||
            name.EndsWith("IT.java", StringComparison.Ordinal);
    }

    private static void ClassifyTestPath(string path, JavaSourceMetrics metrics)
    {
        if (!IsTestPath(path)) return;
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal) ||
            lower.Contains("endtoend", StringComparison.Ordinal) ||
            lower.Contains("end-to-end", StringComparison.Ordinal)) metrics.EndToEndTests = 1;
        else if (lower.Contains("component", StringComparison.Ordinal)) metrics.ComponentTests = 1;
        else if (lower.Contains("integration", StringComparison.Ordinal) ||
            Path.GetFileName(path).EndsWith("IT.java", StringComparison.Ordinal)) metrics.IntegrationTests = 1;
    }
}
