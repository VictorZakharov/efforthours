using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed partial class CandidatePreflightTests
{
    private const string ManualQaReviewPolicyDigest =
        "sha256:bc4d9a29aac4fd80a917219c77a02dd0c8d1b71ecfb91d4f2b1ad287ecb231e0";
    private const string ManualQaReviewManifestDigest =
        "sha256:556fb26e096aece4c01bcaeb26118ae807d5f0d36102321a81087ce009816f9a";

    [Fact]
    public void ManualQaReviewCheckpointFreezesExactCandidateBlindPackets()
    {
        string root = RepositoryRoot();
        string checkpoint = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "2.0.0");
        string corpusJson = File.ReadAllText(Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "0.3.0.development-corpus.json"));
        string policyJson = File.ReadAllText(Path.Combine(checkpoint, "manual-qa-review-policy.json"));
        string manifestJson = File.ReadAllText(Path.Combine(checkpoint, "manual-qa-review-manifest.json"));
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        ManualQaReviewPolicy policy = ContractJson.Deserialize<ManualQaReviewPolicy>(policyJson);
        ManualQaReviewManifest committedManifest =
            ContractJson.Deserialize<ManualQaReviewManifest>(manifestJson);

        ManualQaReviewFreezeRunner.ValidatePolicyDigest(
            policyJson,
            ManualQaReviewPolicyDigest);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaReviewPolicy,
            policyJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaReviewManifest,
            manifestJson).IsValid);
        Assert.Equal(
            EligibleCodingEffortVersions.Categories,
            ManualQaCandidateTransformer.EligibleCategories);

        ManualQaReviewPacketSet reproduced = ManualQaReviewAuthoring.Scaffold(
            corpus,
            policy,
            ManualQaReviewPolicyDigest);
        Assert.Equal(15, reproduced.Manifest.RecordCount);
        Assert.Equal(955, reproduced.Manifest.TargetCount);
        Assert.Equal(
            ManualQaReviewManifestDigest,
            CalibrationDigest.Compute(reproduced.Manifest));
        Assert.Equal(
            ContractJson.SerializeDocument(reproduced.Manifest),
            manifestJson);

        Dictionary<string, CalibrationRecord> records = corpus.Records.ToDictionary(
            record => record.Id,
            StringComparer.Ordinal);
        Dictionary<string, ManualQaReviewPacket> packets = reproduced.Packets.ToDictionary(
            packet => packet.SourceRecordId,
            StringComparer.Ordinal);
        foreach (ManualQaReviewManifestPacket entry in committedManifest.Packets)
        {
            ManualQaReviewPacket packet = packets[entry.SourceRecordId];
            string packetPath = Path.Combine(
                checkpoint,
                "manual-qa-review-packets",
                entry.FileName);
            string packetJson = File.ReadAllText(packetPath);
            CalibrationRecord source = records[entry.SourceRecordId];
            string[] expectedTargetIds =
            [
                .. source.Targets.Where(target =>
                        EligibleCodingEffortVersions.Categories.Contains(target.Category))
                    .Select(target => target.Id)
                    .Order(StringComparer.Ordinal),
            ];

            Assert.Equal(ContractJson.SerializeDocument(packet), packetJson);
            Assert.Equal(entry.PacketDigest, CalibrationDigest.Compute(packet));
            Assert.Equal(
                expectedTargetIds,
                packet.Targets.Select(target => target.SourceTargetId).Order(StringComparer.Ordinal));
            Assert.True(ContractSchemaValidator.Validate(
                SchemaNames.CalibrationManualQaReviewPacket,
                packetJson).IsValid);
            Assert.DoesNotContain("\"hours\"", packetJson);
            Assert.DoesNotContain("\"rationale\"", packetJson);
            Assert.DoesNotContain("\"sourceWorkItemIds\"", packetJson);
            Assert.DoesNotContain("manual-qa-coding-ratio", packetJson);
            Assert.DoesNotContain("seed-rules", packetJson);
            Assert.DoesNotContain("30%", packetJson);
            Assert.DoesNotContain("40%", packetJson);
            Assert.DoesNotContain("50%", packetJson);
        }

        Assert.Equal(
            reproduced.Packets.SelectMany(packet => packet.Targets).Count(),
            reproduced.Packets.SelectMany(packet => packet.Targets.Select(target =>
                    (packet.SourceRecordId, target.SourceTargetId, target.SourceLineageDigest)))
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task ManualQaReviewFreezeIsDeterministicAndRefusesChangedArtifacts()
    {
        string root = RepositoryRoot();
        string checkpoint = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "2.0.0");
        using ManualQaReviewTemporaryDirectory temporary = new();
        ManualQaReviewFreezeOptions options = new()
        {
            CorpusPath = Path.Combine(
                root,
                "calibration",
                "corpora",
                "public-readiness",
                "0.3.0.development-corpus.json"),
            PolicyPath = Path.Combine(checkpoint, "manual-qa-review-policy.json"),
            ExpectedPolicyDigest = ManualQaReviewPolicyDigest,
            PacketDirectory = Path.Combine(temporary.Path, "packets"),
            ManifestPath = Path.Combine(temporary.Path, "manifest.json"),
        };

        await ManualQaReviewFreezeRunner.RunAsync(options, CancellationToken.None);
        string firstManifest = File.ReadAllText(options.ManifestPath);
        await ManualQaReviewFreezeRunner.RunAsync(options, CancellationToken.None);
        Assert.Equal(firstManifest, File.ReadAllText(options.ManifestPath));

        string unexpected = Path.Combine(
            options.PacketDirectory,
            "unexpected.manual-qa-review.json");
        File.WriteAllText(unexpected, "{}");
        InvalidDataException unexpectedError = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ManualQaReviewFreezeRunner.RunAsync(options, CancellationToken.None));
        Assert.Contains("unexpected artifact", unexpectedError.Message, StringComparison.Ordinal);
        File.Delete(unexpected);

        string packet = Directory.GetFiles(options.PacketDirectory, "*.json").Order().First();
        File.AppendAllText(packet, " ");
        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ManualQaReviewFreezeRunner.RunAsync(options, CancellationToken.None));
        Assert.Contains("Refusing to overwrite", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualQaReviewFreezeOptionsRequireEveryPinnedBoundary()
    {
        bool valid = ManualQaReviewFreezeOptions.TryParse(
        [
            "--corpus", "corpus.json",
            "--policy", "policy.json",
            "--expected-policy-digest", ManualQaReviewPolicyDigest,
            "--packets", "packets",
            "--manifest", "manifest.json",
        ], out ManualQaReviewFreezeOptions? options, out string? error);
        bool missing = ManualQaReviewFreezeOptions.TryParse(
        [
            "--corpus", "corpus.json",
            "--policy", "policy.json",
            "--expected-policy-digest", ManualQaReviewPolicyDigest,
            "--packets", "packets",
        ], out _, out string? missingError);

        Assert.True(valid, error);
        Assert.NotNull(options);
        Assert.False(missing);
        Assert.Contains("--manifest", missingError, StringComparison.Ordinal);
    }

    private sealed class ManualQaReviewTemporaryDirectory : IDisposable
    {
        public ManualQaReviewTemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"efforthours-manual-qa-review-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
