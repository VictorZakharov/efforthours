namespace EffortHours.Analysis;

internal static class GoFileClassification
{
    public static bool IsProjectManifest(string lowerName) => lowerName == "go.mod";

    public static bool IsProjectArtifact(string lowerName) =>
        IsProjectManifest(lowerName) || lowerName is "go.work" or "go.sum";

    public static bool IsTestSource(string lowerName) =>
        lowerName.EndsWith("_test.go", StringComparison.Ordinal);
}
