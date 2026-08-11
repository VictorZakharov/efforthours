namespace EffortHours.Analysis;

internal static class JavaFileClassification
{
    public static bool IsProjectManifest(string lowerName) =>
        lowerName is "pom.xml" or "build.gradle" or "build.gradle.kts";

    public static bool IsProjectArtifact(string lowerName) =>
        IsProjectManifest(lowerName) ||
        lowerName is "settings.gradle" or "settings.gradle.kts" or
            "gradle.properties" or "gradle.lockfile" or
            "gradlew" or "gradlew.bat" or "mvnw" or "mvnw.cmd";

    public static bool IsTestSource(string fileName) =>
        fileName.EndsWith("Test.java", StringComparison.Ordinal) ||
        fileName.EndsWith("Tests.java", StringComparison.Ordinal) ||
        fileName.EndsWith("TestCase.java", StringComparison.Ordinal) ||
        fileName.EndsWith("IT.java", StringComparison.Ordinal);
}
