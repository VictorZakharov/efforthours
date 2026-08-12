namespace EffortHours.Analysis;

internal static class KotlinFileClassification
{
    public static bool IsGradleScript(string lowerName) =>
        lowerName.EndsWith(".gradle.kts", StringComparison.Ordinal);

    public static bool IsTestSource(string fileName) =>
        fileName.EndsWith("Test.kt", StringComparison.Ordinal) ||
        fileName.EndsWith("Tests.kt", StringComparison.Ordinal) ||
        fileName.EndsWith("TestCase.kt", StringComparison.Ordinal) ||
        fileName.EndsWith("IT.kt", StringComparison.Ordinal) ||
        fileName.EndsWith("Test.kts", StringComparison.Ordinal) ||
        fileName.EndsWith("Tests.kts", StringComparison.Ordinal) ||
        fileName.EndsWith("TestCase.kts", StringComparison.Ordinal) ||
        fileName.EndsWith("IT.kts", StringComparison.Ordinal);
}
