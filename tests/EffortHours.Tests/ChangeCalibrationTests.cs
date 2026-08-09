using EffortHours.Calibration;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeCalibrationTests
{
    [Fact]
    public async Task BlindScaffoldPinsFinalDeltaAndHidesCandidateHours()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(("src/Status.cs", "public static class Status { }\n")),
            State(("src/Status.cs", "public static class Status { public static bool Ready => true; }\n")));

        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:synthetic-dotnet-library-a",
            "change:dotnet-production-edit",
            ["production", "clean-disjoint"],
            blind: true);

        Assert.NotNull(packet.Change);
        Assert.Equal(packet.Repository.SourceDigest, packet.Change.FinalDeltaDigest);
        Assert.Equal(estimate.Selection.Base.ObjectId, packet.Change.BaseObjectId);
        Assert.Equal(estimate.Selection.Head.ObjectId, packet.Change.HeadObjectId);
        Assert.Null(packet.Candidate.TotalHours);
        Assert.Empty(packet.Candidate.Categories);
        Assert.All(packet.Targets, target =>
        {
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Confidence);
        });
        Assert.Empty(ContractValidation.Validate(packet));

        string json = ContractJson.Serialize(packet);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            json).IsValid);
    }

    [Fact]
    public async Task CompileAndEvaluatePreserveChangeLineageAndMetrics()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(("src/status.ts", "export const status = 'ok';\n")),
            State(
                ("src/status.ts", "export const status = 'ready';\n"),
                ("test/status.test.ts", "test('status', () => expect(status).toBe('ready'));\n")));
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:synthetic-typescript-service-c",
            "change:typescript-code-and-tests",
            ["production", "tests"],
            blind: false);
        CalibrationReviewPlan plan = Plan(packet, estimate, CalibrationPartition.Test);

        CalibrationCorpus corpus = ChangeCalibrationReviewCompiler.Compile(plan, [estimate]);
        CalibrationEvaluationReport evaluation = ChangeCalibrationEvaluator.Evaluate(
            corpus,
            [estimate],
            CalibrationPartition.Test);

        CalibrationRecord record = Assert.Single(corpus.Records);
        Assert.Equal(packet.Change, record.Change);
        Assert.Equal(0m, evaluation.RepositoryTotals.Expected.WeightedAbsolutePercentageError);
        Assert.Equal(evaluation.Match.TargetCount, evaluation.Match.MatchedTargetCount);
        Assert.Equal(estimate.WorkItems.Count, evaluation.Match.MatchedCandidateWorkItemCount);
        Assert.Empty(ContractValidation.Validate(corpus));
        Assert.Empty(ContractValidation.Validate(evaluation));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            ContractJson.Serialize(corpus)).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationEvaluation,
            ContractJson.Serialize(evaluation)).IsValid);

        CalibrationCorpusReviewPacket blindReview = CalibrationCorpusReviewAuthoring.Scaffold(
            corpus,
            blind: true);
        Assert.Equal(record.Change, Assert.Single(blindReview.Records).Change);
        Assert.Empty(ContractValidation.Validate(blindReview));
    }

    [Fact]
    public async Task CompilerRejectsMismatchedImmutableProvenance()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(),
            State(("README.md", "# Synthetic change\n")));
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:synthetic-docs-a",
            "change:docs",
            ["documentation"]);
        CalibrationReviewPlan plan = Plan(packet, estimate, CalibrationPartition.Validation);
        CalibrationReviewPlan tampered = plan with
        {
            Records =
            [
                plan.Records[0] with
                {
                    Change = plan.Records[0].Change! with { HeadObjectId = "different-object" },
                },
            ],
        };

        CalibrationEvaluationException exception = Assert.Throws<CalibrationEvaluationException>(() =>
            ChangeCalibrationReviewCompiler.Compile(tampered, [estimate]));

        Assert.Contains(
            exception.Errors,
            error => error.Contains("provenance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChangeRepositoryFamilyCannotLeakAcrossPartitions()
    {
        ChangeEstimateReport first = await EstimateAsync(
            State(),
            State(("src/a.js", "export const a = 1;\n")));
        ChangeEstimateReport second = await EstimateAsync(
            State(),
            State(("src/b.js", "export const b = 2;\n")));
        CalibrationRecord development = CompileRecord(
            first,
            "repository:one-family",
            "change:first",
            CalibrationPartition.Development);
        CalibrationRecord test = CompileRecord(
            second,
            "repository:one-family",
            "change:second",
            CalibrationPartition.Test);
        CalibrationCorpus corpus = new()
        {
            Id = "change-leakage",
            Version = "0.1.0",
            Description = "Invalid cross-partition family fixture.",
            Rubric = Rubric,
            Records = [development, test],
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(corpus);

        Assert.Contains(errors, error => error.Contains("both", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZeroFinalDeltaNeedsNoInventedCalibrationTarget()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(("src/Status.cs", "namespace Demo;\npublic sealed class Status { public bool Ready => true; }\n")),
            State(("src/Status.cs", "namespace Demo;\n\npublic sealed class Status\n{\n    public bool Ready => true;\n}\n")));
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:synthetic-zero-a",
            "change:formatting-only",
            ["formatting-only"]);
        CalibrationReviewPlan plan = Plan(packet, estimate, CalibrationPartition.Development);

        CalibrationCorpus corpus = ChangeCalibrationReviewCompiler.Compile(plan, [estimate]);
        CalibrationEvaluationReport evaluation = ChangeCalibrationEvaluator.Evaluate(
            corpus,
            [estimate],
            CalibrationPartition.Development);

        Assert.Equal(0m, estimate.TotalEffort.Expected);
        Assert.Empty(estimate.WorkItems);
        Assert.Empty(packet.Targets);
        Assert.Empty(Assert.Single(corpus.Records).Targets);
        Assert.Equal(1, evaluation.RepositoryTotals.Expected.SampleCount);
        Assert.Equal(0, evaluation.WorkItems.Expected.SampleCount);
        Assert.Empty(ContractValidation.Validate(corpus));

        CalibrationCorpusReviewPlan secondPass = new()
        {
            CompilerVersion = CalibrationCorpusReviewCompiler.CompilerVersion,
            SourceCorpus = new CalibrationCorpusReference
            {
                Id = corpus.Id,
                Version = corpus.Version,
                Digest = CalibrationDigest.Compute(corpus),
            },
            Id = "change-zero-reviewed",
            Version = "0.1.1",
            Description = "Independent acceptance of an empty final delta.",
            Records =
            [
                new CalibrationCorpusReviewPlanRecord
                {
                    SourceRecordId = corpus.Records[0].Id,
                    ResultStatus = CalibrationReviewStatus.Reviewed,
                    CompletedOn = new DateOnly(2026, 8, 7),
                    Reviewers =
                    [
                        new CalibrationReviewer
                        {
                            Id = "host-ai:independent-zero-reviewer",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Reviewer,
                            ModelId = "independent-test-model",
                            ModelVersion = "test-version",
                        },
                    ],
                    Notes = "Verified that formatting changes represent no final EHE.",
                    Targets = [],
                },
            ],
        };
        CalibrationCorpus reviewed = CalibrationCorpusReviewCompiler.Compile(secondPass, corpus);
        Assert.Empty(Assert.Single(reviewed.Records).Targets);
        Assert.NotNull(reviewed.Records[0].Change);
        Assert.Equal(CalibrationReviewStatus.Reviewed, reviewed.Records[0].Review.Status);
    }

    [Fact]
    public async Task ReviewCanPreserveFalsePositiveAsExplicitZeroTarget()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(),
            State(("src/status.js", "export const status = 'ready';\n")));
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:synthetic-exclusion-a",
            "change:false-positive-exclusion",
            ["javascript", "production"]);
        CalibrationReviewPlan plan = Plan(packet, estimate, CalibrationPartition.Development);
        string excludedCapabilityId = CalibrationAuthoring.GetSourceCapabilityId(
            estimate.WorkItems.First(item =>
                item.Category == EffortCategory.SpecificationComprehensionAndDomainLearning).Id);
        string[] excludedWorkItemIds = [.. estimate.WorkItems
            .Where(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id) == excludedCapabilityId)
            .Select(item => item.Id)];
        plan = plan with
        {
            Records =
            [
                plan.Records[0] with
                {
                    Capabilities = [.. plan.Records[0].Capabilities.Select(capability =>
                        capability.SourceCapabilityId == excludedCapabilityId
                            ? capability with
                            {
                                Rationale = "Reviewer rejected duplicate specification overhead.",
                                Targets =
                                [
                                    new CalibrationReviewTargetDecision
                                    {
                                        Hours = new EffortRange { Low = 0m, Expected = 0m, High = 0m },
                                        SizeException =
                                            "Explicit false-positive exclusion with retained source lineage.",
                                    },
                                ],
                            }
                            : capability)],
                },
            ],
        };

        Assert.Empty(ContractValidation.Validate(plan));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            ContractJson.Serialize(plan)).IsValid);

        CalibrationCorpus corpus = ChangeCalibrationReviewCompiler.Compile(plan, [estimate]);
        CalibrationEvaluationReport evaluation = ChangeCalibrationEvaluator.Evaluate(
            corpus,
            [estimate],
            CalibrationPartition.Development);

        CalibrationTarget excluded = Assert.Single(Assert.Single(corpus.Records).Targets, target =>
                target.SourceWorkItemIds.Count == excludedWorkItemIds.Length &&
                target.SourceWorkItemIds.All(excludedWorkItemIds.Contains));
        Assert.Equal(new EffortRange { Low = 0m, Expected = 0m, High = 0m }, excluded.Hours);
        Assert.NotNull(excluded.SizeException);
        Assert.Equal(evaluation.Match.CandidateWorkItemCount, evaluation.Match.MatchedCandidateWorkItemCount);
        Assert.Empty(Assert.Single(evaluation.Repositories).UnmatchedCandidateWorkItemIds);
    }

    private static CalibrationRecord CompileRecord(
        ChangeEstimateReport estimate,
        string familyId,
        string caseId,
        CalibrationPartition partition)
    {
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            familyId,
            caseId,
            ["production"]);
        return Assert.Single(ChangeCalibrationReviewCompiler.Compile(
            Plan(packet, estimate, partition),
            [estimate]).Records);
    }

    private static CalibrationReviewPlan Plan(
        CalibrationAuthoringPacket packet,
        ChangeEstimateReport estimate,
        CalibrationPartition partition) => new()
        {
            CompilerVersion = ChangeCalibrationReviewCompiler.CompilerVersion,
            Id = $"corpus:{packet.Change!.Id}",
            Version = "0.1.0",
            Description = "Synthetic Change EHE teacher fixture.",
            Rubric = packet.Rubric,
            Records =
            [
                new CalibrationReviewPlanRecord
                {
                    Id = $"record:{packet.Change.Id}",
                    Repository = packet.Repository,
                    Change = packet.Change,
                    Profile = packet.Profile,
                    BaselineId = packet.BaselineId,
                    Partition = partition,
                    SourceEstimatorVersion = estimate.EstimatorVersion,
                    SourceEstimateDigest = CalibrationDigest.Compute(estimate),
                    Source = new CalibrationSourceProvenance
                    {
                        DataClassification = CalibrationDataClassification.Synthetic,
                        SourceReference = "eh://synthetic/change-calibration",
                        Revision = $"{estimate.Selection.Base.ObjectId}..{estimate.Selection.Head.ObjectId}",
                        LicenseExpression = "MIT",
                        RedistributionAllowed = true,
                    },
                    Review = new CalibrationReviewProvenance
                    {
                        Status = CalibrationReviewStatus.TeacherEstimate,
                        CompletedOn = new DateOnly(2026, 8, 6),
                        Reviewers =
                        [
                            new CalibrationReviewer
                            {
                                Id = "host-ai:synthetic-teacher",
                                Kind = CalibrationReviewerKind.HostAi,
                                Role = CalibrationReviewerRole.Teacher,
                                ModelId = "test-model",
                                ModelVersion = "test-version",
                            },
                        ],
                    },
                    Capabilities = [.. estimate.WorkItems
                        .GroupBy(item => CalibrationAuthoring.GetSourceCapabilityId(item.Id))
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new CalibrationCapabilityReviewDecision
                        {
                            SourceCapabilityId = group.Key,
                            Rationale = "Synthetic logical review of represented final-delta work.",
                            Targets = [.. group
                                .OrderBy(item => item.Id, StringComparer.Ordinal)
                                .Select(item => new CalibrationReviewTargetDecision
                                {
                                    Hours = item.Hours,
                                    UncertaintyReasons = item.UncertaintyReasons,
                                })],
                        })],
                },
            ],
        };

    private static CalibrationRubricReference Rubric => new()
    {
        Id = ChangeCalibrationAuthoring.RubricId,
        Version = ChangeCalibrationAuthoring.RubricVersion,
    };

    private static async Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "synthetic-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.Evidence,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
