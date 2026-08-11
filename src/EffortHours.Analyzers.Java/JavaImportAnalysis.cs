namespace EffortHours.Analyzers.Java;

internal sealed class JavaImportContext
{
    public Dictionary<string, string> ExplicitTypes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> StaticMembers { get; } = new(StringComparer.Ordinal);

    public HashSet<string> StaticWildcardOwners { get; } = new(StringComparer.Ordinal);

    public HashSet<string> WildcardPackages { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ImportsSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);

    public string ResolveType(string name)
    {
        if (name.Length == 0) return string.Empty;
        if (name.Contains('.') && char.IsLower(name[0])) return name;
        int separator = name.IndexOf('.');
        string root = separator < 0 ? name : name[..separator];
        string suffix = separator < 0 ? string.Empty : name[separator..];
        if (ExplicitTypes.TryGetValue(root, out string? imported)) return imported + suffix;
        string[] wildcardMatches = [.. WildcardPackages
            .Select(package => package + "." + name)
            .Where(candidate => JavaImportAnalysis.Technology(candidate) is not null)
            .Distinct(StringComparer.Ordinal)];
        return wildcardMatches.Length == 1 ? wildcardMatches[0] : string.Empty;
    }

    public bool MatchesType(string name, params string[] canonicalTypes)
    {
        string resolved = ResolveType(name);
        if (canonicalTypes.Contains(resolved, StringComparer.Ordinal)) return true;
        if (name.Contains('.')) return canonicalTypes.Contains(name, StringComparer.Ordinal);
        return canonicalTypes.Any(canonical =>
            canonical.EndsWith('.' + name, StringComparison.Ordinal) &&
            WildcardPackages.Contains(canonical[..canonical.LastIndexOf('.')], StringComparer.Ordinal));
    }

    public string ResolveCall(string name, IReadOnlyDictionary<string, string> instances)
    {
        if (StaticMembers.TryGetValue(name, out string? staticMember)) return staticMember;
        int separator = name.IndexOf('.');
        string root = separator < 0 ? name : name[..separator];
        string suffix = separator < 0 ? string.Empty : name[separator..];
        if (instances.TryGetValue(root, out string? instance)) return instance + suffix;
        if (ExplicitTypes.TryGetValue(root, out string? imported)) return imported + suffix;
        if (separator >= 0 && char.IsLower(name[0])) return name;
        return separator < 0 && StaticWildcardOwners.Count == 1
            ? StaticWildcardOwners.Single() + "." + name
            : string.Empty;
    }
}

internal static class JavaImportAnalysis
{
    public static JavaImportContext Read(IReadOnlyList<JavaToken> tokens)
    {
        JavaImportContext context = new();
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text != "import") continue;
            bool isStatic = index + 1 < tokens.Count && tokens[index + 1].Text == "static";
            int start = index + (isStatic ? 2 : 1);
            string name = JavaTokenUtilities.QualifiedName(tokens, start);
            if (name.Length == 0) continue;
            Add(name, isStatic, context);
        }

        return context;
    }

    private static void Add(string importName, bool isStatic, JavaImportContext context)
    {
        context.ImportsSeen.Add(importName);
        string technologyPath = importName.EndsWith(".*", StringComparison.Ordinal)
            ? importName[..^2]
            : importName;
        string? technology = Technology(technologyPath);
        if (technology is not null) context.Technologies.Add(technology);

        if (importName.EndsWith(".*", StringComparison.Ordinal))
        {
            string owner = importName[..^2];
            if (isStatic) context.StaticWildcardOwners.Add(owner);
            else context.WildcardPackages.Add(owner);
            return;
        }

        int separator = importName.LastIndexOf('.');
        string simple = separator < 0 ? importName : importName[(separator + 1)..];
        if (isStatic) context.StaticMembers[simple] = importName;
        else context.ExplicitTypes[simple] = importName;
    }

    public static string? Technology(string name)
    {
        string lower = name.ToLowerInvariant();
        if (Within(lower, "org.springframework.web")) return "spring-web";
        if (Within(lower, "jakarta.ws.rs") || Within(lower, "javax.ws.rs")) return "jakarta-rest";
        if (Within(lower, "org.springframework.data")) return "spring-data";
        if (Within(lower, "jakarta.persistence") || Within(lower, "javax.persistence")) return "jakarta-persistence";
        if (Within(lower, "org.springframework.jdbc") || Within(lower, "java.sql")) return "jdbc";
        if (Within(lower, "org.hibernate")) return "hibernate";
        if (Within(lower, "org.mybatis")) return "mybatis";
        if (Within(lower, "org.jooq")) return "jooq";
        if (Within(lower, "org.flywaydb")) return "flyway";
        if (Within(lower, "liquibase")) return "liquibase";
        if (Within(lower, "org.springframework.security")) return "spring-security";
        if (Within(lower, "jakarta.annotation.security") || Within(lower, "javax.annotation.security")) return "jakarta-security";
        if (Within(lower, "io.jsonwebtoken") || Within(lower, "com.auth0.jwt")) return "jwt";
        if (Within(lower, "org.springframework.kafka")) return "spring-kafka";
        if (Within(lower, "org.springframework.amqp")) return "spring-amqp";
        if (Within(lower, "jakarta.jms") || Within(lower, "javax.jms")) return "jms";
        if (Within(lower, "org.apache.kafka")) return "kafka";
        if (Within(lower, "com.rabbitmq")) return "rabbitmq";
        if (Within(lower, "io.grpc")) return "grpc";
        if (Within(lower, "software.amazon.awssdk") || Within(lower, "com.amazonaws")) return "aws";
        if (Within(lower, "com.google.cloud")) return "google-cloud";
        if (Within(lower, "com.azure")) return "azure";
        if (Within(lower, "java.net.http")) return "java-http";
        if (Within(lower, "okhttp3")) return "okhttp";
        if (Within(lower, "retrofit2")) return "retrofit";
        if (Within(lower, "feign") || Within(lower, "org.springframework.cloud.openfeign")) return "openfeign";
        if (Within(lower, "org.springframework.scheduling")) return "spring-scheduling";
        if (Within(lower, "org.quartz")) return "quartz";
        if (Within(lower, "org.springframework.batch")) return "spring-batch";
        if (Within(lower, "picocli")) return "picocli";
        if (Within(lower, "com.beust.jcommander")) return "jcommander";
        if (Within(lower, "jakarta.validation") || Within(lower, "javax.validation")) return "jakarta-validation";
        if (Within(lower, "org.junit")) return "junit";
        if (Within(lower, "org.testng")) return "testng";
        if (Within(lower, "org.assertj")) return "assertj";
        if (Within(lower, "org.mockito")) return "mockito";
        if (Within(lower, "org.hamcrest")) return "hamcrest";
        if (Within(lower, "org.testcontainers")) return "testcontainers";
        if (Within(lower, "org.springframework.test")) return "spring-test";
        if (Within(lower, "java.util.concurrent")) return "java-concurrency";
        return null;
    }

    private static bool Within(string value, string root) =>
        value == root || value.StartsWith(root + ".", StringComparison.Ordinal);
}
