namespace EffortHours.Analysis;

internal static class PhpFileClassification
{
    public static bool IsProjectArtifact(string lowerName) =>
        IsProjectManifest(lowerName) || lowerName is
            "composer.lock" or "phpunit.xml" or "phpunit.xml.dist" or
            "phpstan.neon" or "phpstan.neon.dist" or "psalm.xml" or "psalm.xml.dist";

    public static bool IsProjectManifest(string lowerName) => lowerName == "composer.json";

    public static bool IsTestSource(string fileName)
    {
        string lowerName = fileName.ToLowerInvariant();
        return lowerName.EndsWith("test.php", StringComparison.Ordinal) ||
            lowerName.EndsWith("tests.php", StringComparison.Ordinal) ||
            lowerName is "pest.php" or "testcase.php";
    }

    public static bool IsGeneratedPath(string lowerPath)
    {
        string normalized = lowerPath.Replace('\\', '/');
        return normalized.StartsWith("bootstrap/cache/", StringComparison.Ordinal) ||
            normalized.Contains("/bootstrap/cache/", StringComparison.Ordinal) ||
            normalized.StartsWith("var/cache/", StringComparison.Ordinal) ||
            normalized.Contains("/var/cache/", StringComparison.Ordinal) ||
            normalized.StartsWith("storage/framework/views/", StringComparison.Ordinal) ||
            normalized.Contains("/storage/framework/views/", StringComparison.Ordinal) ||
            normalized.StartsWith("storage/framework/cache/", StringComparison.Ordinal) ||
            normalized.Contains("/storage/framework/cache/", StringComparison.Ordinal);
    }
}
