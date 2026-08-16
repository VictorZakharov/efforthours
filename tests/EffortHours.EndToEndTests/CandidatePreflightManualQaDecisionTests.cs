using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed partial class CandidatePreflightTests
{
    private const string ManualQaDecisionPolicyDigest =
        "sha256:92c0b73259773cb3e4bec6e570043ea41ff8a83d4448ce2e2ea292be05455f60";
    private const string ManualQaDecisionTemplateDigest =
        "sha256:d71c51ef4f9b7f71d295a6b3fa5cc42f56e35c2a148d89c7db1954fd63e56ff8";

    [Fact]
    public void ManualQaDecisionCheckpointFreezesTheExactPreAnswerCompilerBoundary()
    {
        string root = RepositoryRoot();
        string reviewCheckpoint = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "2.0.0");
        string decisionCheckpoint = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "2.1.0");
        string corpusJson = File.ReadAllText(Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "0.3.0.development-corpus.json"));
        string reviewPolicyJson = File.ReadAllText(Path.Combine(
            reviewCheckpoint,
            "manual-qa-review-policy.json"));
        string manifestJson = File.ReadAllText(Path.Combine(
            reviewCheckpoint,
            "manual-qa-review-manifest.json"));
        string policyJson = File.ReadAllText(Path.Combine(
            decisionCheckpoint,
            "manual-qa-decision-compiler-policy.json"));
        string templateJson = File.ReadAllText(Path.Combine(
            decisionCheckpoint,
            "manual-qa-decision-plan.template.json"));

        ManualQaDecisionRunner.ValidateDigest(
            policyJson,
            ManualQaDecisionPolicyDigest,
            "manual-QA decision policy");
        ManualQaDecisionRunner.ValidateDigest(
            templateJson,
            ManualQaDecisionTemplateDigest,
            "manual-QA decision template");
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaDecisionPolicy,
            policyJson).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaDecisionPlan,
            templateJson).IsValid);

        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(corpusJson);
        ManualQaReviewPolicy reviewPolicy = ContractJson.Deserialize<ManualQaReviewPolicy>(
            reviewPolicyJson);
        ManualQaReviewManifest manifest = ContractJson.Deserialize<ManualQaReviewManifest>(
            manifestJson);
        ManualQaDecisionCompilerPolicy policy =
            ContractJson.Deserialize<ManualQaDecisionCompilerPolicy>(policyJson);
        ManualQaDecisionPlan template = ContractJson.Deserialize<ManualQaDecisionPlan>(templateJson);
        ManualQaReviewPacket[] packets =
        [
            .. manifest.Packets.Select(entry => ContractJson.Deserialize<ManualQaReviewPacket>(
                File.ReadAllText(Path.Combine(
                    reviewCheckpoint,
                    "manual-qa-review-packets",
                    entry.FileName)))),
        ];
        ManualQaDecisionPlan reproduced = ManualQaDecisionAuthoring.CreateTemplate(
            corpus,
            reviewPolicy,
            ManualQaReviewPolicyDigest,
            manifest,
            packets,
            policy,
            ManualQaDecisionPolicyDigest);

        Assert.Equal(templateJson, ContractJson.SerializeDocument(reproduced));
        Assert.Equal(15, template.RecordCount);
        Assert.Equal(955, template.DecisionCount);
        Assert.Equal(320, policy.ExpectedRemovedTargetCount);
        Assert.Equal(1_710, policy.ExpectedPreservedTargetCount);
        Assert.Equal(2_665, policy.ExpectedOutputTargetCount);
        Assert.Equal(ManualQaDecisionPlanStatus.Unreviewed, template.Status);
        Assert.All(template.Records, record => Assert.Null(record.Review));
        Assert.All(template.Records.SelectMany(record => record.Decisions), decision =>
        {
            Assert.Null(decision.Disposition);
            Assert.Null(decision.Hours);
            Assert.Null(decision.Rationale);
            Assert.Empty(decision.EvidenceIds);
            Assert.Empty(decision.UncertaintyReasons);
            Assert.Null(decision.OverlapAllocation);
            Assert.Null(decision.DuplicateOfSourceTargetId);
            Assert.Null(decision.SizeException);
        });

        Dictionary<string, ManualQaReviewManifestPacket> entries = manifest.Packets.ToDictionary(
            entry => entry.SourceRecordId,
            StringComparer.Ordinal);
        Dictionary<string, ManualQaReviewPacket> packetsByRecord = packets.ToDictionary(
            packet => packet.SourceRecordId,
            StringComparer.Ordinal);
        foreach (ManualQaDecisionPlanRecord record in template.Records)
        {
            ManualQaReviewManifestPacket entry = entries[record.SourceRecordId];
            Assert.Equal(entry.PacketDigest, record.PacketDigest);
            Assert.Equal(entry.LineageDigest, record.LineageDigest);
            Dictionary<string, ManualQaReviewTarget> targets = packetsByRecord[record.SourceRecordId]
                .Targets.ToDictionary(target => target.SourceTargetId, StringComparer.Ordinal);
            Assert.Equal(targets.Count, record.Decisions.Count);
            Assert.All(record.Decisions, decision =>
            {
                ManualQaReviewTarget target = targets[decision.SourceTargetId];
                Assert.Equal(target.SourceLineageDigest, decision.SourceLineageDigest);
                Assert.Equal(target.OverlapGroupId, decision.OverlapGroupId);
            });
        }

        Assert.DoesNotContain("manual-qa-coding-ratio", templateJson);
        Assert.DoesNotContain("seed-rules", templateJson);
        Assert.DoesNotContain("30%", templateJson);
        Assert.DoesNotContain("40%", templateJson);
        Assert.DoesNotContain("50%", templateJson);
        Assert.Empty(ContractValidation.Validate(policy));
        Assert.Empty(ContractValidation.Validate(template));
    }

    [Fact]
    public void ManualQaDecisionOptionsRequireEveryDigestPinnedBoundary()
    {
        string[] boundary =
        [
            "--corpus", "corpus.json",
            "--review-policy", "review-policy.json",
            "--expected-review-policy-digest", ManualQaReviewPolicyDigest,
            "--review-manifest", "manifest.json",
            "--packets", "packets",
            "--compiler-policy", "compiler-policy.json",
            "--expected-compiler-policy-digest", ManualQaDecisionPolicyDigest,
        ];
        bool templateValid = ManualQaDecisionTemplateOptions.TryParse(
            [.. boundary, "--output", "template.json"],
            out ManualQaDecisionTemplateOptions? template,
            out string? templateError);
        bool compileValid = ManualQaDecisionCompileOptions.TryParse(
            [
                .. boundary,
                "--plan", "plan.json",
                "--expected-plan-digest", ManualQaDecisionTemplateDigest,
                "--output", "corpus.json",
            ],
            out ManualQaDecisionCompileOptions? compile,
            out string? compileError);
        bool missing = ManualQaDecisionCompileOptions.TryParse(
            [.. boundary, "--plan", "plan.json", "--output", "corpus.json"],
            out _,
            out string? missingError);

        Assert.True(templateValid, templateError);
        Assert.NotNull(template);
        Assert.True(compileValid, compileError);
        Assert.NotNull(compile);
        Assert.False(missing);
        Assert.Contains("--expected-plan-digest", missingError, StringComparison.Ordinal);
    }
}
