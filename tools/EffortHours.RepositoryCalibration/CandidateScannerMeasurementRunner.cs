using System.Globalization;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateScannerMeasurementRunner
{
    public const int Files = 1_000;
    public const int LinesPerFile = 100;

    public static async Task<CandidateScannerMeasurement> RunAsync(
        string scannerBenchmarkPath,
        CancellationToken cancellationToken)
    {
        MeasuredProcessResult result = await CandidateMeasurementProcess.RunAsync(
            "dotnet",
            [
                scannerBenchmarkPath,
                "--files", Files.ToString(CultureInfo.InvariantCulture),
                "--lines-per-file", LinesPerFile.ToString(CultureInfo.InvariantCulture),
                "--mixed",
            ],
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> values = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        bool unchanged = ReadBool(values, "target-metadata-unchanged");
        bool execution = values.GetValueOrDefault("target-execution") == "not-performed";
        bool installation = values.GetValueOrDefault("dependency-installation") == "not-performed";
        bool network = values.GetValueOrDefault("network-access") == "not-performed";
        bool passed = values.GetValueOrDefault("mode") == "mixed-static" &&
            unchanged && execution && installation && network;
        return new CandidateScannerMeasurement
        {
            Mode = Required(values, "mode"),
            RequestedLines = ReadInt(values, "requested-lines"),
            AnalyzedTextLines = ReadLong(values, "analyzed-text-lines"),
            ScanSeconds = ReadDecimal(values, "scan-seconds"),
            PeakWorkingSetMib = ReadDecimal(values, "scan-peak-working-set-mib"),
            SourceDigest = Required(values, "digest"),
            TargetMetadataDigest = Required(values, "target-metadata-digest"),
            TargetMetadataUnchanged = unchanged,
            TargetExecutionNotPerformed = execution,
            DependencyInstallationNotPerformed = installation,
            NetworkAccessNotPerformed = network,
            ThresholdStatus = "not-applicable-no-frozen-cross-platform-scanner-threshold",
            Passed = passed,
        };
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value)
            ? value
            : throw new InvalidDataException($"Scanner benchmark omitted '{key}'.");

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key) =>
        int.Parse(Required(values, key), NumberStyles.None, CultureInfo.InvariantCulture);

    private static long ReadLong(IReadOnlyDictionary<string, string> values, string key) =>
        long.Parse(Required(values, key), NumberStyles.None, CultureInfo.InvariantCulture);

    private static decimal ReadDecimal(IReadOnlyDictionary<string, string> values, string key) =>
        decimal.Parse(Required(values, key), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key) =>
        bool.Parse(Required(values, key));
}
