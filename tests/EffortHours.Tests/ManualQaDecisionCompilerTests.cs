using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ManualQaDecisionCompilerTests
{
    private const string ReviewPolicyDigest =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CompilerPolicyDigest =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void TemplateIsDeterministicSchemaValidAndAnswerFree()
    {
        Fixture fixture = CreateFixture();

        ManualQaDecisionPlan first = CreateTemplate(fixture);
        ManualQaDecisionPlan second = CreateTemplate(fixture);
        string json = ContractJson.Serialize(first);

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
        Assert.Equal(ManualQaDecisionPlanStatus.Unreviewed, first.Status);
        Assert.Equal(2, first.DecisionCount);
        Assert.Null(Assert.Single(first.Records).Review);
        Assert.All(first.Records.SelectMany(record => record.Decisions), decision =>
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
        Assert.DoesNotContain("manual-qa-coding-ratio", json);
        Assert.DoesNotContain("seed-rules", json);
        Assert.Empty(ContractValidation.Validate(first));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaDecisionPlan,
            json).IsValid);
        Assert.Empty(ContractValidation.Validate(fixture.CompilerPolicy));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationManualQaDecisionPolicy,
            ContractJson.Serialize(fixture.CompilerPolicy)).IsValid);
    }

    [Fact]
    public void CompilerPreservesNonQaReplacesLegacyQaAndKeepsZeroDecisionsMeasurable()
    {
        Fixture fixture = CreateFixture();
        ManualQaDecisionPlan completed = Complete(CreateTemplate(fixture), fixture.Packets);

        CalibrationCorpus first = Compile(fixture, completed);
        CalibrationCorpus reordered = Compile(
            fixture,
            completed with
            {
                Records =
                [
                    .. completed.Records.Select(record => record with
                    {
                        Decisions = [.. record.Decisions.Reverse()],
                    }),
                ],
            });

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(reordered));
        CalibrationRecord source = Assert.Single(fixture.Corpus.Records);
        CalibrationRecord output = Assert.Single(first.Records);
        CalibrationTarget[] preserved =
        [
            .. source.Targets.Where(target =>
                target.Category != EffortCategory.ManualValidationDebuggingAndHardening),
        ];
        Assert.Equal(3, preserved.Length);
        Assert.All(preserved, target => Assert.Contains(output.Targets, candidate => candidate == target));
        Assert.DoesNotContain(output.Targets, target => target.Id == "target:legacy-qa");

        CalibrationTarget[] qa =
        [
            .. output.Targets.Where(target =>
                target.Category == EffortCategory.ManualValidationDebuggingAndHardening),
        ];
        Assert.Equal(2, qa.Length);
        Assert.Contains(qa, target => target.Hours.Expected == 2m);
        CalibrationTarget excluded = Assert.Single(qa, target => target.Hours.Expected == 0m);
        Assert.NotNull(excluded.SizeException);
        Assert.All(qa.SelectMany(target => target.SourceWorkItemIds), id =>
            Assert.StartsWith("work:manual-qa-coding-ratio:", id, StringComparison.Ordinal));
        Assert.Equal(5, output.Targets.Count);
        Assert.Equal(ManualQaDecisionVersions.OutputRubricId, first.Rubric.Id);
        Assert.Contains(ManualQaDecisionCompiler.CompilerVersion, output.Review.Notes);
        Assert.Empty(ContractValidation.Validate(first));
    }

    private static ManualQaDecisionPlan CreateTemplate(Fixture fixture) =>
        ManualQaDecisionAuthoring.CreateTemplate(
            fixture.Corpus,
            fixture.ReviewPolicy,
            ReviewPolicyDigest,
            fixture.Manifest,
            fixture.Packets,
            fixture.CompilerPolicy,
            CompilerPolicyDigest);

    private static CalibrationCorpus Compile(Fixture fixture, ManualQaDecisionPlan plan) =>
        ManualQaDecisionCompiler.Compile(
            fixture.Corpus,
            fixture.ReviewPolicy,
            ReviewPolicyDigest,
            fixture.Manifest,
            fixture.Packets,
            fixture.CompilerPolicy,
            CompilerPolicyDigest,
            plan);

    private static ManualQaDecisionPlan Complete(
        ManualQaDecisionPlan template,
        IReadOnlyList<ManualQaReviewPacket> packets)
    {
        Dictionary<string, ManualQaReviewTarget> targets = packets.SelectMany(packet => packet.Targets)
            .ToDictionary(target => target.SourceTargetId, StringComparer.Ordinal);
        return template with
        {
            Status = ManualQaDecisionPlanStatus.Completed,
            Records =
            [
                .. template.Records.Select(record => record with
                {
                    Review = new CalibrationReviewProvenance
                    {
                        Status = CalibrationReviewStatus.TeacherEstimate,
                        CompletedOn = new DateOnly(2026, 8, 16),
                        Reviewers =
                        [
                            new CalibrationReviewer
                            {
                                Id = "teacher:manual-qa-test",
                                Kind = CalibrationReviewerKind.HostAi,
                                Role = CalibrationReviewerRole.Teacher,
                                ModelId = "test-model",
                                ModelVersion = "1",
                            },
                        ],
                        Notes = "Synthetic candidate-blind manual-QA review.",
                    },
                    Decisions =
                    [
                        .. record.Decisions.Select((decision, index) => index == 0
                            ? decision with
                            {
                                Disposition = ManualQaDecisionDisposition.Estimate,
                                Hours = new EffortRange { Low = 1m, Expected = 2m, High = 3m },
                                Rationale = "Representative execution and focused debugging are required.",
                                EvidenceIds = [targets[decision.SourceTargetId].EvidenceIds[0]],
                                UncertaintyReasons = ["Integration behavior may require diagnosis."],
                                OverlapAllocation = "This target owns its distinct validation flow.",
                            }
                            : decision with
                            {
                                Disposition = ManualQaDecisionDisposition.Exclude,
                                Hours = new EffortRange { Low = 0m, Expected = 0m, High = 0m },
                                Rationale = "The signal is wholly excluded from represented manual QA.",
                                EvidenceIds = [targets[decision.SourceTargetId].EvidenceIds[0]],
                                OverlapAllocation = "No shared validation is allocated to an excluded target.",
                                SizeException = "Explicit reviewed exclusion; no represented manual QA remains.",
                            }),
                    ],
                }),
            ],
        };
    }

    private static Fixture CreateFixture()
    {
        CalibrationCorpus corpus = Corpus();
        ManualQaReviewPolicy reviewPolicy = ReviewPolicy(corpus);
        ManualQaReviewPacketSet packetSet = ManualQaReviewAuthoring.Scaffold(
            corpus,
            reviewPolicy,
            ReviewPolicyDigest);
        return new Fixture(
            corpus,
            reviewPolicy,
            packetSet.Manifest,
            packetSet.Packets,
            CompilerPolicy(corpus, reviewPolicy, packetSet.Manifest));
    }

    private static ManualQaReviewPolicy ReviewPolicy(CalibrationCorpus corpus) => new()
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
        SourceCorpus = CorpusReference(corpus),
        Partition = CalibrationPartition.Development,
        Profile = EstimationProfile.Implementation,
        BaselineId = "senior-contractor-2026-no-ai",
        CandidateVisibility = CalibrationCandidateVisibility.Blind,
        EligibleCategories = EligibleCodingEffortVersions.Categories,
        ExpectedRecordCount = 1,
        ExpectedTargetCount = 2,
        HiddenInputs = ["candidate-values"],
        RequiredReviewPractices = ["Review independently."],
        Limitations = ["Synthetic test policy."],
    };

    private static ManualQaDecisionCompilerPolicy CompilerPolicy(
        CalibrationCorpus corpus,
        ManualQaReviewPolicy reviewPolicy,
        ManualQaReviewManifest manifest) => new()
        {
            PolicyVersion = ManualQaDecisionVersions.PolicyV1,
            Id = ManualQaDecisionAuthoring.PolicyId,
            CompilerVersion = ManualQaDecisionVersions.CompilerV1,
            AuthoringVersion = ManualQaDecisionVersions.AuthoringV1,
            LicenseExpression = "MIT",
            Maturity = ManualQaDecisionAuthoring.Maturity,
            PlanId = "manual-qa-test-plan",
            PlanVersion = "1.0.0",
            PlanDescription = "Synthetic manual-QA compiler test plan.",
            SourceCorpus = CorpusReference(corpus),
            ReviewPolicy = new ManualQaReviewPolicyReference
            {
                Id = reviewPolicy.Id,
                Version = reviewPolicy.PolicyVersion,
                Digest = ReviewPolicyDigest,
            },
            ReviewManifest = new ManualQaReviewManifestReference
            {
                Version = manifest.ManifestVersion,
                Digest = CalibrationDigest.Compute(manifest),
            },
            ReviewRubric = reviewPolicy.Rubric,
            OutputCorpus = new ManualQaDecisionOutputCorpus
            {
                Id = "manual-qa-test-output",
                Version = "1.0.0",
                Description = "Synthetic compiled manual-QA corpus.",
                Rubric = new CalibrationRubricReference
                {
                    Id = ManualQaDecisionVersions.OutputRubricId,
                    Version = ManualQaDecisionVersions.OutputRubricV1,
                },
            },
            Partition = CalibrationPartition.Development,
            Profile = EstimationProfile.Implementation,
            BaselineId = "senior-contractor-2026-no-ai",
            ReplacedCategory = EffortCategory.ManualValidationDebuggingAndHardening,
            SourceWorkItemLineageVersion = ManualQaDecisionVersions.SourceWorkItemLineageV1,
            ExpectedRecordCount = 1,
            ExpectedDecisionCount = 2,
            ExpectedRemovedTargetCount = 1,
            ExpectedPreservedTargetCount = 3,
            ExpectedOutputTargetCount = 5,
            RequiredDecisionPractices = ["Complete every decision."],
            Limitations = ["Synthetic test policy."],
        };

    private static CalibrationCorpus Corpus() => new()
    {
        Id = "manual-qa-decision-test",
        Version = "1.0.0",
        Description = "Synthetic manual-QA decision compiler source corpus.",
        Rubric = new CalibrationRubricReference { Id = "ehe-work-item", Version = "1.1.0" },
        Records =
        [
            new CalibrationRecord
            {
                Id = "record:manual-qa-test:implementation",
                Repository = new CalibrationRepositoryReference
                {
                    Id = "repository:manual-qa-test",
                    Name = "Example/ManualQa",
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
                    SourceReference = "https://example.invalid/manual-qa",
                    Revision = new string('a', 40),
                    LicenseExpression = "MIT",
                    RedistributionAllowed = true,
                },
                Review = new CalibrationReviewProvenance
                {
                    Status = CalibrationReviewStatus.TeacherEstimate,
                    CompletedOn = new DateOnly(2026, 8, 13),
                    Reviewers =
                    [
                        new CalibrationReviewer
                        {
                            Id = "teacher:source",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Teacher,
                            ModelId = "test-model",
                            ModelVersion = "1",
                        },
                    ],
                    Notes = "Synthetic source review.",
                },
                Targets =
                [
                    Target("target:production", EffortCategory.ProductionImplementation, "work:production"),
                    Target("target:tests", EffortCategory.UnitTesting, "work:tests"),
                    Target("target:docs", EffortCategory.Documentation, "work:docs"),
                    Target(
                        "target:legacy-qa",
                        EffortCategory.ManualValidationDebuggingAndHardening,
                        "work:legacy-qa"),
                ],
            },
        ],
    };

    private static CalibrationTarget Target(string id, EffortCategory category, string workItemId) =>
        new()
        {
            Id = id,
            Category = category,
            Title = $"Review {id}",
            Scope = "src/example",
            SourceWorkItemIds = [workItemId],
            EvidenceIds = [$"evidence:{id}"],
            Hours = new EffortRange { Low = 3m, Expected = 4m, High = 6m },
            Rationale = "Synthetic source judgment.",
            UncertaintyReasons = ["Synthetic uncertainty."],
        };

    private static CalibrationCorpusReference CorpusReference(CalibrationCorpus corpus) => new()
    {
        Id = corpus.Id,
        Version = corpus.Version,
        Digest = CalibrationDigest.Compute(corpus),
    };

    private static string Digest(char character) => $"sha256:{new string(character, 64)}";

    private sealed record Fixture(
        CalibrationCorpus Corpus,
        ManualQaReviewPolicy ReviewPolicy,
        ManualQaReviewManifest Manifest,
        IReadOnlyList<ManualQaReviewPacket> Packets,
        ManualQaDecisionCompilerPolicy CompilerPolicy);
}
