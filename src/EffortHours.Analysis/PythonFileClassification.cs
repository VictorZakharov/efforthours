namespace EffortHours.Analysis;

internal static class PythonFileClassification
{
    public static bool IsProjectManifest(string lowerName) =>
        lowerName is "pyproject.toml" or "setup.cfg" or "setup.py";

    public static bool IsProjectArtifact(string lowerName) =>
        IsProjectManifest(lowerName) ||
        lowerName is "pipfile" or "pipfile.lock" or "poetry.lock" or "pdm.lock" or
            "uv.lock" or "tox.ini" or "pytest.ini" or ".coveragerc" ||
        IsRequirementsFile(lowerName);

    public static bool IsRequirementsFile(string lowerName) =>
        lowerName.StartsWith("requirements", StringComparison.Ordinal) &&
        (lowerName.EndsWith(".txt", StringComparison.Ordinal) ||
         lowerName.EndsWith(".in", StringComparison.Ordinal));
}
