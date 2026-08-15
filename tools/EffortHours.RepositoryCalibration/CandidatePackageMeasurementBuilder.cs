namespace EffortHours.RepositoryCalibration;

internal static class CandidatePackageMeasurementBuilder
{
    public const decimal MaximumIncreaseMib = 25m;

    public static CandidatePackageMeasurement Build(
        string seedInstallPath,
        string candidateInstallPath)
    {
        if (!Directory.Exists(seedInstallPath) || !Directory.Exists(candidateInstallPath))
        {
            throw new DirectoryNotFoundException(
                "Both seed and staged-candidate installed tool layouts are required.");
        }

        FileInfo[] seedFiles = [.. new DirectoryInfo(seedInstallPath).EnumerateFiles(
            "*",
            SearchOption.AllDirectories)];
        FileInfo[] candidateFiles = [.. new DirectoryInfo(candidateInstallPath).EnumerateFiles(
            "*",
            SearchOption.AllDirectories)];
        bool hasModel = candidateFiles.Any(file =>
            file.Name == "1.0.0.logical-capability-model.json");
        bool hasRuntime = candidateFiles.Any(file =>
            file.Name == "EffortHours.RepositoryCalibration.dll");
        bool seedHasCandidate = seedFiles.Any(file =>
            file.Name is "1.0.0.logical-capability-model.json" or
                "EffortHours.RepositoryCalibration.dll");
        if (!hasModel || !hasRuntime || seedHasCandidate)
        {
            throw new InvalidDataException(
                "Installed-layout measurement did not isolate the candidate model and runtime overlay.");
        }

        long seedBytes = seedFiles.Sum(file => file.Length);
        long candidateBytes = candidateFiles.Sum(file => file.Length);
        long increase = candidateBytes - seedBytes;
        decimal increaseMib = decimal.Round(
            increase / 1024m / 1024m,
            4,
            MidpointRounding.AwayFromZero);
        return new CandidatePackageMeasurement
        {
            SeedInstalledBytes = seedBytes,
            CandidateInstalledBytes = candidateBytes,
            IncreaseBytes = increase,
            IncreaseMib = increaseMib,
            MaximumIncreaseMib = MaximumIncreaseMib,
            Passed = increase >= 0 && increaseMib <= MaximumIncreaseMib,
        };
    }
}
