using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class ManualQaReviewFreezeRunner
{
    public static async Task RunAsync(
        ManualQaReviewFreezeOptions options,
        CancellationToken cancellationToken)
    {
        string corpusJson = await File.ReadAllTextAsync(options.CorpusPath, cancellationToken)
            .ConfigureAwait(false);
        string policyJson = await File.ReadAllTextAsync(options.PolicyPath, cancellationToken)
            .ConfigureAwait(false);
        ValidatePolicyDigest(policyJson, options.ExpectedPolicyDigest);
        ValidateSchema(SchemaNames.CalibrationCorpus, corpusJson, "source corpus");
        ValidateSchema(
            SchemaNames.CalibrationManualQaReviewPolicy,
            policyJson,
            "manual-QA review policy");

        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        ManualQaReviewPolicy policy = ContractJson.Deserialize<ManualQaReviewPolicy>(policyJson);
        ManualQaReviewPacketSet result = ManualQaReviewAuthoring.Scaffold(
            corpus,
            policy,
            options.ExpectedPolicyDigest);

        Directory.CreateDirectory(options.PacketDirectory);
        Dictionary<string, ManualQaReviewPacket> packetsByRecord = result.Packets.ToDictionary(
            packet => packet.SourceRecordId,
            StringComparer.Ordinal);
        RejectUnexpectedPacketArtifacts(options.PacketDirectory, result.Manifest.Packets);
        foreach (ManualQaReviewManifestPacket entry in result.Manifest.Packets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ManualQaReviewPacket packet = packetsByRecord[entry.SourceRecordId];
            string document = ContractJson.SerializeDocument(packet);
            ValidateSchema(
                SchemaNames.CalibrationManualQaReviewPacket,
                document,
                $"manual-QA review packet '{entry.SourceRecordId}'");
            string path = Path.Combine(options.PacketDirectory, entry.FileName);
            await WriteImmutableAsync(path, document, cancellationToken).ConfigureAwait(false);
        }

        string manifestDocument = ContractJson.SerializeDocument(result.Manifest);
        ValidateSchema(
            SchemaNames.CalibrationManualQaReviewManifest,
            manifestDocument,
            "manual-QA review manifest");
        Directory.CreateDirectory(Path.GetDirectoryName(options.ManifestPath)!);
        await WriteImmutableAsync(
                options.ManifestPath,
                manifestDocument,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void RejectUnexpectedPacketArtifacts(
        string packetDirectory,
        IReadOnlyList<ManualQaReviewManifestPacket> expectedPackets)
    {
        HashSet<string> expectedFileNames = expectedPackets
            .Select(packet => packet.FileName)
            .ToHashSet(StringComparer.Ordinal);
        string? unexpected = Directory
            .EnumerateFiles(packetDirectory, "*.manual-qa-review.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .FirstOrDefault(fileName => !expectedFileNames.Contains(fileName));
        if (unexpected is not null)
        {
            throw new InvalidDataException(
                $"Refusing to use a manual-QA packet directory containing unexpected artifact '{unexpected}'.");
        }
    }

    internal static void ValidatePolicyDigest(string policyJson, string expectedDigest)
    {
        string actual = JsonArtifactDigest.Compute(policyJson);
        if (!string.Equals(actual, expectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Manual-QA review policy digest differs: expected {expectedDigest}; actual {actual}.");
        }
    }

    private static void ValidateSchema(string schemaName, string json, string description)
    {
        SchemaValidationResult result = ContractSchemaValidator.Validate(schemaName, json);
        if (!result.IsValid)
        {
            throw new InvalidDataException(
                $"The {description} is schema-invalid: {string.Join("; ", result.Errors)}");
        }
    }

    private static async Task WriteImmutableAsync(
        string path,
        string document,
        CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            string existing = await File.ReadAllTextAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(existing, document, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Refusing to overwrite different frozen manual-QA review artifact '{path}'.");
            }

            return;
        }

        await File.WriteAllTextAsync(path, document, cancellationToken).ConfigureAwait(false);
    }
}
