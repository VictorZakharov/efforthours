namespace EffortHours.Analysis;

internal static class DockerFileClassification
{
    public static bool IsProjectArtifact(string lowerName) =>
        IsDockerfile(lowerName) || IsComposeFile(lowerName) || lowerName == ".dockerignore";

    public static bool IsDockerfile(string lowerName) =>
        lowerName == "dockerfile" ||
        lowerName.StartsWith("dockerfile.", StringComparison.Ordinal) ||
        lowerName.EndsWith(".dockerfile", StringComparison.Ordinal);

    public static bool IsComposeFile(string lowerName)
    {
        if (!lowerName.EndsWith(".yml", StringComparison.Ordinal) &&
            !lowerName.EndsWith(".yaml", StringComparison.Ordinal))
        {
            return false;
        }

        return lowerName.StartsWith("compose.", StringComparison.Ordinal) ||
            lowerName.StartsWith("docker-compose.", StringComparison.Ordinal);
    }
}
