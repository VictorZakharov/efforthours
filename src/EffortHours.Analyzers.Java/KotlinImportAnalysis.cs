namespace EffortHours.Analyzers.Java;

internal sealed class KotlinImportContext
{
    public Dictionary<string, string> Symbols { get; } = new(StringComparer.Ordinal);

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
        if (Symbols.TryGetValue(root, out string? imported)) return imported + suffix;
        string[] wildcardMatches = [.. WildcardPackages
            .Select(package => package + "." + name)
            .Where(candidate => KotlinImportAnalysis.Technology(candidate) is not null)
            .Distinct(StringComparer.Ordinal)];
        return wildcardMatches.Length == 1 ? wildcardMatches[0] : string.Empty;
    }

    public string ResolveCall(string name, IReadOnlyDictionary<string, string> instances)
    {
        if (Symbols.TryGetValue(name, out string? direct)) return direct;
        int separator = name.IndexOf('.');
        string root = separator < 0 ? name : name[..separator];
        string suffix = separator < 0 ? string.Empty : name[separator..];
        if (instances.TryGetValue(root, out string? instance)) return instance + suffix;
        if (Symbols.TryGetValue(root, out string? imported)) return imported + suffix;
        if (separator < 0)
        {
            string[] wildcardMatches = [.. WildcardPackages
                .Select(package => package + "." + name)
                .Where(candidate => KotlinImportAnalysis.Technology(candidate) is not null)
                .Distinct(StringComparer.Ordinal)];
            if (wildcardMatches.Length == 1) return wildcardMatches[0];
        }
        if (separator >= 0 && char.IsLower(name[0])) return name;
        return string.Empty;
    }
}

internal static class KotlinImportAnalysis
{
    public static KotlinImportContext Read(IReadOnlyList<KotlinToken> tokens)
    {
        KotlinImportContext context = new();
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Text != "import") continue;
            int start = index + 1;
            string name = KotlinTokenUtilities.QualifiedName(tokens, start);
            if (name.Length == 0) continue;
            int end = start + KotlinTokenUtilities.QualifiedNameLength(tokens, start);
            string simple = name[(name.LastIndexOf('.') + 1)..];
            if (end + 1 < tokens.Count && tokens[end].Text == "as" &&
                tokens[end + 1].Kind == KotlinTokenKind.Identifier)
                simple = tokens[end + 1].Text;
            Add(name, simple, context);
        }

        return context;
    }

    private static void Add(string importName, string simple, KotlinImportContext context)
    {
        context.ImportsSeen.Add(importName);
        string technologyPath = importName.EndsWith(".*", StringComparison.Ordinal)
            ? importName[..^2]
            : importName;
        string? technology = Technology(technologyPath);
        if (technology is not null) context.Technologies.Add(technology);
        if (importName.EndsWith(".*", StringComparison.Ordinal))
            context.WildcardPackages.Add(importName[..^2]);
        else
            context.Symbols[simple] = importName;
    }

    public static string? Technology(string name)
    {
        string lower = name.ToLowerInvariant();
        if (Within(lower, "io.ktor.server")) return "ktor-server";
        if (Within(lower, "io.ktor.client")) return "ktor-client";
        if (Within(lower, "org.springframework.web")) return "spring-web";
        if (Within(lower, "jakarta.ws.rs") || Within(lower, "javax.ws.rs")) return "jakarta-rest";
        if (Within(lower, "org.springframework.data")) return "spring-data";
        if (Within(lower, "jakarta.persistence") || Within(lower, "javax.persistence")) return "jakarta-persistence";
        if (Within(lower, "org.springframework.jdbc") || Within(lower, "java.sql")) return "jdbc";
        if (Within(lower, "org.jetbrains.exposed")) return "exposed";
        if (Within(lower, "androidx.room")) return "android-room";
        if (Within(lower, "org.flywaydb")) return "flyway";
        if (Within(lower, "liquibase")) return "liquibase";
        if (Within(lower, "androidx.compose")) return "android-compose";
        if (Within(lower, "androidx.activity") || Within(lower, "android.app")) return "android-activity";
        if (Within(lower, "androidx.fragment")) return "android-fragment";
        if (Within(lower, "androidx.lifecycle")) return "android-lifecycle";
        if (Within(lower, "android.content")) return "android-component";
        if (Within(lower, "androidx.work")) return "android-work";
        if (Within(lower, "kotlinx.coroutines.flow")) return "kotlin-flow";
        if (Within(lower, "kotlinx.coroutines")) return "kotlin-coroutines";
        if (Within(lower, "org.springframework.security")) return "spring-security";
        if (Within(lower, "jakarta.annotation.security") || Within(lower, "javax.annotation.security")) return "jakarta-security";
        if (Within(lower, "io.jsonwebtoken") || Within(lower, "com.auth0.jwt")) return "jwt";
        if (Within(lower, "androidx.security")) return "android-security";
        if (Within(lower, "java.net.http")) return "java-http";
        if (Within(lower, "okhttp3")) return "okhttp";
        if (Within(lower, "retrofit2")) return "retrofit";
        if (Within(lower, "io.grpc")) return "grpc";
        if (Within(lower, "software.amazon.awssdk") || Within(lower, "com.amazonaws")) return "aws";
        if (Within(lower, "com.google.cloud")) return "google-cloud";
        if (Within(lower, "com.azure")) return "azure";
        if (Within(lower, "org.apache.kafka") || Within(lower, "org.springframework.kafka")) return "kafka";
        if (Within(lower, "org.springframework.amqp")) return "spring-amqp";
        if (Within(lower, "jakarta.jms") || Within(lower, "javax.jms")) return "jms";
        if (Within(lower, "org.springframework.scheduling")) return "spring-scheduling";
        if (Within(lower, "org.quartz")) return "quartz";
        if (Within(lower, "org.springframework.batch")) return "spring-batch";
        if (Within(lower, "jakarta.validation") || Within(lower, "javax.validation")) return "jakarta-validation";
        if (Within(lower, "com.github.ajalt.clikt")) return "clikt";
        if (Within(lower, "picocli")) return "picocli";
        if (Within(lower, "kotlin.test")) return "kotlin-test";
        if (Within(lower, "org.junit")) return "junit";
        if (Within(lower, "io.kotest")) return "kotest";
        if (Within(lower, "io.mockk")) return "mockk";
        if (Within(lower, "org.mockito")) return "mockito";
        if (Within(lower, "org.testcontainers")) return "testcontainers";
        if (Within(lower, "androidx.test") || Within(lower, "androidx.compose.ui.test")) return "android-test";
        return null;
    }

    private static bool Within(string value, string root) =>
        value == root || value.StartsWith(root + ".", StringComparison.Ordinal);
}
