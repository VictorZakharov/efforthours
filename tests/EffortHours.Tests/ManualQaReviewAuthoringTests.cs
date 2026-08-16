using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ManualQaReviewAuthoringTests
{
    private const string PolicyDigest =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void CandidateBlindPacketsExposeEligibleResponsibilitiesWithoutPriorJudgments()
    {
        CalibrationCorpus corpus = Corpus();
        ManualQaReviewPolicy policy = Policy(corpus);

        ManualQaReviewPacketSet result = ManualQaReviewAuthoring.Scaffold(
            corpus,
            policy,
            PolicyDigest);
        ManualQaReviewPacket packet = Assert.Single(result.Packets);
        string packetJson = ContractJson.Serialize(packet);

        Assert.Equal(2, packet.Targets.Count);
        Assert.Equal(
            packet.Targets[0].OverlapGroupId,
            packet.Targets[1].OverlapGroupId);
        Assert.NotEqual(
            packet.Targets[0].SourceLineageDigest,
            packet.Targets[1].SourceLineageDigest);
        Assert.All(packet.Targets, target =>
            Assert.Contains(target.SourceCategory, EligibleCodingEffortVersions.Categories));
        Assert.DoesNotContain("manual-validation-debugging-and-hardening", packetJson);
        Assert.DoesNotContain("SENTINEL-PRIOR-RATIONALE", packetJson);
        Assert.DoesNotContain("SENTINEL-PRIOR-UNCERTAINTY", packetJson);
        Assert.DoesNotContain("SENTINEL-PRIOR-SIZE", packetJson);
        Assert.DoesNotContain("\"hours\"", packetJson);
        Assert.DoesNotContain("\"rationale\"", packetJson);
        Assert.DoesNotContain("\"sourceWorkItemIds\"", packetJson);
        Assert.Equal(CalibrationCandidateVisibility.Blind, packet.CandidateVisibility);
        Assert.Empty(ContractValidation.Validate(packet));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaReviewPacket,
            packetJson).IsValid);

        Assert.Equal(1, result.Manifest.RecordCount);
        Assert.Equal(2, result.Manifest.TargetCount);
        Assert.Equal(CalibrationDigest.Compute(packet), Assert.Single(result.Manifest.Packets).PacketDigest);
        Assert.Empty(ContractValidation.Validate(result.Manifest));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaReviewManifest,
            ContractJson.Serialize(result.Manifest)).IsValid);
    }

    [Fact]
    public void PacketTargetProjectionDoesNotReadSourceHoursOrReviewText()
    {
        CalibrationCorpus original = Corpus();
        CalibrationRecord originalRecord = Assert.Single(original.Records);
        CalibrationTarget[] changedTargets =
        [
            .. originalRecord.Targets.Select(target => target with
            {
                Hours = target.Hours.Expected == 0m
                    ? target.Hours
                    : new EffortRange { Low = 1m, Expected = 99m, High = 123m },
                Rationale = "CHANGED-PRIOR-RATIONALE",
                UncertaintyReasons = ["CHANGED-PRIOR-UNCERTAINTY"],
                SizeException = target.Hours.Expected == 0m
                    ? target.SizeException
                    : "CHANGED-PRIOR-SIZE",
            }),
        ];
        CalibrationCorpus changed = original with
        {
            Records = [originalRecord with { Targets = changedTargets }],
        };

        ManualQaReviewPacket first = Assert.Single(ManualQaReviewAuthoring.Scaffold(
            original,
            Policy(original),
            PolicyDigest).Packets);
        ManualQaReviewPacket second = Assert.Single(ManualQaReviewAuthoring.Scaffold(
            changed,
            Policy(changed),
            PolicyDigest).Packets);

        Assert.Equal(
            ContractJson.Serialize(first.Targets),
            ContractJson.Serialize(second.Targets));
        Assert.DoesNotContain("CHANGED-PRIOR", ContractJson.Serialize(second));
    }

    [Fact]
    public void AuthoringRejectsHoldoutAndPolicyDrift()
    {
        CalibrationCorpus development = Corpus();
        CalibrationRecord source = Assert.Single(development.Records);
        CalibrationCorpus validation = development with
        {
            Records = [source with { Partition = CalibrationPartition.Validation }],
        };
        ManualQaReviewPolicy validationPolicy = Policy(validation) with
        {
            Partition = CalibrationPartition.Validation,
        };

        Assert.Throws<CalibrationEvaluationException>(() =>
            ManualQaReviewAuthoring.Scaffold(validation, validationPolicy, PolicyDigest));
        Assert.Throws<CalibrationEvaluationException>(() =>
            ManualQaReviewAuthoring.Scaffold(
                development,
                Policy(development) with { ExpectedTargetCount = 3 },
                PolicyDigest));
    }

    [Fact]
    public void PolicyContractPinsTheSharedEligibleCodingBoundary()
    {
        CalibrationCorpus corpus = Corpus();
        ManualQaReviewPolicy policy = Policy(corpus);
        string json = ContractJson.Serialize(policy);

        Assert.Empty(ContractValidation.Validate(policy));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaReviewPolicy,
            json).IsValid);
        Assert.Contains(
            "eligible-coding-effort/1.0.0",
            EligibleCodingEffortVersions.V1,
            StringComparison.Ordinal);
        Assert.NotEmpty(policy.HiddenInputs);
    }

    private static ManualQaReviewPolicy Policy(CalibrationCorpus corpus) => new()
    {
        PolicyVersion = ManualQaReviewVersions.PolicyV1,
        Id = ManualQaReviewAuthoring.PolicyId,
        AuthoringVersion = ManualQaReviewVersions.AuthoringV1,
        LicenseExpression = "MIT",
        Maturity = ManualQaReviewAuthoring.Maturity,
        Rubric = new CalibrationRubricReference
        {
            Id = ManualQaReviewVersions.RubricId,
            Version = ManualQaReviewVersions.RubricV1,
        },
        SourceCorpus = new CalibrationCorpusReference
        {
            Id = corpus.Id,
            Version = corpus.Version,
            Digest = CalibrationDigest.Compute(corpus),
        },
        Partition = Assert.Single(corpus.Records).Partition,
        Profile = EstimationProfile.Implementation,
        BaselineId = "senior-contractor-2026-no-ai",
        CandidateVisibility = CalibrationCandidateVisibility.Blind,
        EligibleCategories = EligibleCodingEffortVersions.Categories,
        ExpectedRecordCount = 1,
        ExpectedTargetCount = 2,
        HiddenInputs = ["candidate-values", "prior-values"],
        RequiredReviewPractices = ["Review independently."],
        Limitations = ["Synthetic test policy."],
    };

    private static CalibrationCorpus Corpus()
    {
        CalibrationTarget production = Target(
            "target:production",
            EffortCategory.ProductionImplementation,
            ["work:production:part-0001", "work:production:part-0002"]);
        CalibrationTarget tests = Target(
            "target:tests",
            EffortCategory.UnitTesting,
            ["work:tests:part-0001"]);
        CalibrationTarget priorQa = Target(
            "target:prior-qa",
            EffortCategory.ManualValidationDebuggingAndHardening,
            ["work:prior-qa:part-0001"]);
        CalibrationTarget documentation = Target(
            "target:docs",
            EffortCategory.Documentation,
            ["work:docs:part-0001"]);
        return new CalibrationCorpus
        {
            Id = "manual-qa-review-test",
            Version = "1.0.0",
            Description = "Synthetic manual-QA blind-authoring test corpus.",
            Rubric = new CalibrationRubricReference
            {
                Id = "ehe-work-item",
                Version = "1.1.0",
            },
            Records =
            [
                new CalibrationRecord
                {
                    Id = "record:example:implementation",
                    Repository = new CalibrationRepositoryReference
                    {
                        Id = "repository:example",
                        Name = "Example/Repository",
                        SourceDigest = Digest('1'),
                    },
                    Profile = EstimationProfile.Implementation,
                    BaselineId = "senior-contractor-2026-no-ai",
                    Partition = CalibrationPartition.Development,
                    SourceEstimatorVersion = "seed-rules/test",
                    SourceEstimateDigest = Digest('2'),
                    Source = new CalibrationSourceProvenance
                    {
                        DataClassification = CalibrationDataClassification.PublicRedistributable,
                        SourceReference = "https://example.invalid/repository",
                        Revision = new string('a', 40),
                        LicenseExpression = "MIT",
                        RedistributionAllowed = true,
                    },
                    Review = new CalibrationReviewProvenance
                    {
                        Status = CalibrationReviewStatus.TeacherEstimate,
                        CompletedOn = new DateOnly(2026, 8, 16),
                        Reviewers =
                        [
                            new CalibrationReviewer
                            {
                                Id = "teacher:test",
                                Kind = CalibrationReviewerKind.HostAi,
                                Role = CalibrationReviewerRole.Teacher,
                                ModelId = "test-model",
                                ModelVersion = "1",
                            },
                        ],
                    },
                    Targets = [production, tests, priorQa, documentation],
                },
            ],
        };
    }

    private static CalibrationTarget Target(
        string id,
        EffortCategory category,
        IReadOnlyList<string> sourceWorkItemIds) => new()
        {
            Id = id,
            Category = category,
            Title = $"Review {id}",
            Scope = "src/example",
            SourceWorkItemIds = sourceWorkItemIds,
            EvidenceIds = [$"evidence:{id}"],
            Hours = new EffortRange { Low = 3m, Expected = 4m, High = 6m },
            Rationale = "SENTINEL-PRIOR-RATIONALE",
            UncertaintyReasons = ["SENTINEL-PRIOR-UNCERTAINTY"],
            SizeException = "SENTINEL-PRIOR-SIZE",
        };

    private static string Digest(char character) => $"sha256:{new string(character, 64)}";
}
