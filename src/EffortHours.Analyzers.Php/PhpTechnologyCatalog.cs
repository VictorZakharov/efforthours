namespace EffortHours.Analyzers.Php;

internal static class PhpTechnologyCatalog
{
    public static string? FromDependency(string dependency)
    {
        string lower = dependency.ToLowerInvariant();
        if (lower is "laravel/framework" or "illuminate/support") return "laravel";
        if (lower.StartsWith("illuminate/", StringComparison.Ordinal)) return "laravel";
        if (lower.StartsWith("symfony/", StringComparison.Ordinal)) return "symfony";
        if (lower.StartsWith("doctrine/", StringComparison.Ordinal)) return "doctrine";
        if (lower is "guzzlehttp/guzzle" or "guzzlehttp/psr7") return "guzzle";
        if (lower.StartsWith("aws/", StringComparison.Ordinal)) return "aws";
        if (lower.StartsWith("google/cloud-", StringComparison.Ordinal)) return "google-cloud";
        if (lower.StartsWith("microsoft/azure", StringComparison.Ordinal)) return "azure";
        if (lower is "firebase/php-jwt" or "lcobucci/jwt") return "jwt";
        if (lower is "predis/predis") return "redis";
        if (lower is "php-amqplib/php-amqplib") return "rabbitmq";
        if (lower.StartsWith("enqueue/", StringComparison.Ordinal)) return "enqueue";
        if (lower is "react/promise" or "react/event-loop") return "reactphp";
        if (lower.StartsWith("amphp/", StringComparison.Ordinal)) return "amphp";
        if (lower is "phpunit/phpunit") return "phpunit";
        if (lower is "pestphp/pest") return "pest";
        if (lower is "mockery/mockery") return "mockery";
        return null;
    }

    public static string? FromQualifiedName(string name)
    {
        string lower = name.TrimStart('\\').ToLowerInvariant();
        if (Within(lower, "illuminate")) return "laravel";
        if (Within(lower, "symfony")) return "symfony";
        if (Within(lower, "doctrine")) return "doctrine";
        if (Within(lower, "guzzlehttp")) return "guzzle";
        if (Within(lower, "aws")) return "aws";
        if (Within(lower, "google\\cloud")) return "google-cloud";
        if (Within(lower, "microsoft\\azure")) return "azure";
        if (Within(lower, "firebase\\jwt") || Within(lower, "lcobucci\\jwt")) return "jwt";
        if (Within(lower, "predis")) return "redis";
        if (Within(lower, "phpamqplib")) return "rabbitmq";
        if (Within(lower, "enqueue")) return "enqueue";
        if (Within(lower, "react\\promise") || Within(lower, "react\\eventloop")) return "reactphp";
        if (Within(lower, "amp")) return "amphp";
        if (Within(lower, "phpunit")) return "phpunit";
        if (Within(lower, "pest")) return "pest";
        if (Within(lower, "mockery")) return "mockery";
        return null;
    }

    private static bool Within(string value, string root) =>
        value == root || value.StartsWith(root + "\\", StringComparison.Ordinal);
}
