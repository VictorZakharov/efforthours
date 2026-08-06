using Fairbill.Calibration;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Tests;

public sealed class CalibrationTests
{
    [Fact]
    public void AuthoringScaffoldIsDeterministicSchemaValidAndExplicitlyUnreviewedInMemory()
    {
        EstimateReport estimate = CreateCandidate();

        CalibrationAuthoringPacket first = CalibrationAuthoring.Scaffold(estimate);
        CalibrationAuthoringPacket second = CalibrationAuthoring.Scaffold(estimate);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationAuthoringPacket,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Equal(CalibrationAuthoringStatus.Unreviewed, first.Status);
        Assert.Contains("UNREVIEWED", first.Warning, StringComparison.Ordinal);
        Assert.Equal(CalibrationCandidateVisibility.Reference, first.CandidateVisibility);
        Assert.Equal(estimate.TotalEffort, first.Candidate.TotalHours);
        Assert.Equal(CalibrationDigest.Compute(estimate), first.Candidate.EstimateDigest);

        CalibrationAuthoringTarget target = first.Targets[0];
        Assert.NotNull(target.Candidate.Hours);
        Assert.NotNull(target.Candidate.Confidence);
        Assert.Null(target.Review.Hours);
        Assert.Null(target.Review.Rationale);
    }

    [Fact]
    public void BlindAuthoringScaffoldHidesNumericCandidateGuidanceInMemory()
    {
        CalibrationAuthoringPacket packet = CalibrationAuthoring.Scaffold(
            CreateCandidate(),
            blind: true);
        string json = ContractJson.SerializeCompact(packet);
        CalibrationAuthoringPacket roundTrip =
            ContractJson.Deserialize<CalibrationAuthoringPacket>(json);

        Assert.Empty(ContractValidation.Validate(packet));
        Assert.Equal(CalibrationCandidateVisibility.Blind, packet.CandidateVisibility);
        Assert.Null(packet.Candidate.TotalHours);
        Assert.Empty(packet.Candidate.Categories);
        Assert.All(packet.Targets, target =>
        {
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Confidence);
            Assert.False(string.IsNullOrWhiteSpace(target.Candidate.Reason));
        });
        Assert.Equal(json, ContractJson.SerializeCompact(roundTrip));
        Assert.Contains("\"hours\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewPlanCompilesCompleteCapabilityDecisionsWithStableLineageInMemory()
    {
        EstimateReport estimate = CreateCandidate();
        CalibrationReviewPlan plan = CreateReviewPlan(estimate);

        string planJson = ContractJson.Serialize(plan);
        SchemaValidationResult planSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            planJson);
        CalibrationCorpus first = CalibrationReviewCompiler.Compile(plan, [estimate]);
        CalibrationCorpus second = CalibrationReviewCompiler.Compile(plan, [estimate]);

        Assert.True(planSchema.IsValid, string.Join(Environment.NewLine, planSchema.Errors));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));

        CalibrationRecord record = Assert.Single(first.Records);
        Assert.Equal(2, record.Targets.Count);
        Assert.Equal(
            estimate.WorkItems.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal),
            record.Targets.SelectMany(target => target.SourceWorkItemIds)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(record.Targets, target => Assert.NotEmpty(target.EvidenceIds));
    }

    [Fact]
    public void ReviewCompilerRejectsMissingCapabilitiesAndChangedEstimateDigestInMemory()
    {
        EstimateReport estimate = CreateCandidate();
        CalibrationReviewPlan plan = CreateReviewPlan(estimate);
        CalibrationReviewPlanRecord record = Assert.Single(plan.Records);

        CalibrationEvaluationException missing = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationReviewCompiler.Compile(
                plan with
                {
                    Records =
                    [
                        record with { Capabilities = [record.Capabilities[0]] },
                    ],
                },
                [estimate]));
        CalibrationEvaluationException digest = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationReviewCompiler.Compile(
                plan with
                {
                    Records =
                    [
                        record with { SourceEstimateDigest = "sha256:changed" },
                    ],
                },
                [estimate]));
        CalibrationEvaluationException compiler = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationReviewCompiler.Compile(
                plan with { CompilerVersion = "calibration-review-compiler/future" },
                [estimate]));

        Assert.Contains(missing.Errors, error => error.Contains("no decision", StringComparison.Ordinal));
        Assert.Contains(digest.Errors, error => error.Contains("expects estimate digest", StringComparison.Ordinal));
        Assert.Contains(compiler.Errors, error => error.Contains("provides", StringComparison.Ordinal));
    }

    [Fact]
    public void CorpusRoundTripsAndSatisfiesPublishedSchemaWithoutStorage()
    {
        CalibrationCorpus corpus = CreateCorpus();

        string json = ContractJson.Serialize(corpus);
        CalibrationCorpus roundTrip = ContractJson.Deserialize<CalibrationCorpus>(json);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(corpus));
        Assert.Equal(json, ContractJson.Serialize(roundTrip));

        CalibrationValidationSummary summary = CalibrationEvaluator.Summarize(corpus);
        string summaryJson = ContractJson.Serialize(summary);
        SchemaValidationResult summarySchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationValidation,
            summaryJson);
        Assert.True(summarySchema.IsValid, string.Join(Environment.NewLine, summarySchema.Errors));
        Assert.Equal(1, summary.RepositoryCount);
        Assert.Equal(CalibrationPartition.Test, Assert.Single(summary.Partitions).Partition);
    }

    [Fact]
    public void CorpusValidationRejectsRepositorySplitLeakageAndDuplicateMappings()
    {
        CalibrationCorpus original = CreateCorpus();
        CalibrationRecord first = Assert.Single(original.Records);
        CalibrationRecord leaked = first with
        {
            Id = "record:sample:recreation",
            Profile = EstimationProfile.Recreation,
            Partition = CalibrationPartition.Development,
        };
        CalibrationRecord aliased = first with
        {
            Id = "record:aliased:recreation",
            Repository = first.Repository with { Id = "repository:alias" },
            Profile = EstimationProfile.Recreation,
            Partition = CalibrationPartition.Development,
        };
        CalibrationTarget duplicateMapping = first.Targets[1] with
        {
            SourceWorkItemIds = [first.Targets[0].SourceWorkItemIds[0]],
        };
        CalibrationRecord duplicated = first with
        {
            Targets = [first.Targets[0], duplicateMapping],
        };

        IReadOnlyList<string> leakageErrors = ContractValidation.Validate(
            original with { Records = [first, leaked] });
        IReadOnlyList<string> mappingErrors = ContractValidation.Validate(
            original with { Records = [duplicated] });
        IReadOnlyList<string> aliasErrors = ContractValidation.Validate(
            original with { Records = [first, aliased] });

        Assert.Contains(leakageErrors, error => error.Contains("both", StringComparison.Ordinal));
        Assert.Contains(
            mappingErrors,
            error => error.Contains("mapped to more than one target", StringComparison.Ordinal));
        Assert.Contains(
            aliasErrors,
            error => error.Contains("conflicting repository identities", StringComparison.Ordinal));
    }

    [Fact]
    public void CorpusValidationRequiresSizeExceptionAndSafeDistributionMetadata()
    {
        CalibrationCorpus original = CreateCorpus();
        CalibrationRecord record = Assert.Single(original.Records);
        CalibrationTarget oversized = record.Targets[0] with
        {
            Hours = Hours(5m, 9m, 12m),
        };
        CalibrationSourceProvenance privateRedistributable = record.Source with
        {
            DataClassification = CalibrationDataClassification.Private,
            RedistributionAllowed = true,
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(original with
        {
            Records =
            [
                record with
                {
                    Source = privateRedistributable,
                    Targets = [oversized, record.Targets[1]],
                },
            ],
        });

        Assert.Contains(errors, error => error.Contains("sizeException", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("classified private", StringComparison.Ordinal));
    }

    [Fact]
    public void EvaluationComputesDeterministicRepositoryCategoryItemAndIntervalMetrics()
    {
        CalibrationCorpus corpus = CreateCorpus();
        EstimateReport candidate = CreateCandidate();

        CalibrationEvaluationReport first = CalibrationEvaluator.Evaluate(
            corpus,
            [candidate],
            CalibrationPartition.Test);
        CalibrationEvaluationReport second = CalibrationEvaluator.Evaluate(
            corpus,
            [candidate],
            CalibrationPartition.Test);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationEvaluation,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));

        Assert.Equal(2m, first.RepositoryTotals.Expected.MeanAbsoluteErrorHours);
        Assert.Equal(0.25m, first.RepositoryTotals.Expected.WeightedAbsolutePercentageError);
        Assert.Equal(-0.25m, first.RepositoryTotals.Expected.AggregateBiasRate);
        Assert.Equal(1m, first.RepositoryTotals.Interval.ReviewedExpectedCoverage);
        Assert.Equal(0m, first.RepositoryTotals.Interval.ReviewedRangeFullyCoveredRate);

        Assert.Equal(2, first.WorkItems.Expected.SampleCount);
        Assert.Equal(1m, first.WorkItems.Expected.MeanAbsoluteErrorHours);
        Assert.Equal(1m, first.WorkItems.Expected.RootMeanSquaredErrorHours);
        Assert.Equal(2, first.Categories.Count);
        Assert.Equal(1m, first.Match.TargetMatchRate);
        Assert.Equal(1m, first.Match.CandidateWorkItemMatchRate);

        CalibrationRepositoryEvaluation repository = Assert.Single(first.Repositories);
        Assert.Equal("sha256:fixture", repository.SourceDigest);
        Assert.StartsWith("sha256:", repository.CandidateEstimateDigest, StringComparison.Ordinal);
        Assert.Empty(repository.UnmatchedTargetIds);
    }

    [Fact]
    public void EvaluationDisclosesChangedItemIdsWithoutCorruptingTotalMetrics()
    {
        CalibrationCorpus corpus = CreateCorpus();
        EstimateReport candidate = CreateCandidate();
        WorkItem renamed = candidate.WorkItems[1] with { Id = "candidate:renamed-test-item" };
        EstimateReport changed = candidate with { WorkItems = [candidate.WorkItems[0], renamed] };

        CalibrationEvaluationReport report = CalibrationEvaluator.Evaluate(
            corpus,
            [changed],
            CalibrationPartition.Test);

        Assert.Equal(1, report.WorkItems.Expected.SampleCount);
        Assert.Equal(0.5m, report.Match.TargetMatchRate);
        Assert.Equal(2m, report.RepositoryTotals.Expected.MeanAbsoluteErrorHours);
        CalibrationRepositoryEvaluation repository = Assert.Single(report.Repositories);
        Assert.Equal(["target:tests"], repository.UnmatchedTargetIds);
        Assert.Contains("candidate:renamed-test-item", repository.UnmatchedCandidateWorkItemIds);
    }

    [Fact]
    public void EvaluationMetricsIgnorePricingWhileCandidateDigestRetainsTraceability()
    {
        CalibrationCorpus corpus = CreateCorpus();
        EstimateReport effortOnly = CreateCandidate();
        EstimateReport priced = effortOnly with
        {
            RateCard = new RateCard
            {
                Id = "test-rate/1.0.0",
                Name = "Synthetic test rate",
                Currency = "USD",
                HourlyRate = 100m,
                Methodology = "Test-only fixed rate.",
            },
            TotalCost = new CostRange
            {
                Low = 300m,
                Expected = 600m,
                High = 900m,
                Currency = "USD",
            },
        };

        CalibrationEvaluationReport effortReport = CalibrationEvaluator.Evaluate(
            corpus,
            [effortOnly],
            CalibrationPartition.Test);
        CalibrationEvaluationReport pricedReport = CalibrationEvaluator.Evaluate(
            corpus,
            [priced],
            CalibrationPartition.Test);

        Assert.Equal(effortReport.RepositoryTotals, pricedReport.RepositoryTotals);
        Assert.Equal(effortReport.Categories, pricedReport.Categories);
        Assert.Equal(effortReport.WorkItems, pricedReport.WorkItems);
        Assert.NotEqual(
            effortReport.Repositories[0].CandidateEstimateDigest,
            pricedReport.Repositories[0].CandidateEstimateDigest);
    }

    [Fact]
    public void EvaluationRequiresAnExplicitlyPopulatedPartitionAndMatchingCandidate()
    {
        CalibrationCorpus corpus = CreateCorpus();
        EstimateReport candidate = CreateCandidate() with
        {
            Repository = CreateCandidate().Repository with { SourceDigest = "sha256:different" },
        };

        CalibrationEvaluationException partitionError = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationEvaluator.Evaluate(
                corpus,
                [candidate],
                CalibrationPartition.Development));
        CalibrationEvaluationException matchError = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationEvaluator.Evaluate(
                corpus,
                [candidate],
                CalibrationPartition.Test));

        Assert.Contains(partitionError.Errors, error => error.Contains("no 'Development'", StringComparison.Ordinal));
        Assert.Contains(matchError.Errors, error => error.Contains("No candidate matches", StringComparison.Ordinal));
    }

    private static CalibrationCorpus CreateCorpus() => new()
    {
        Id = "synthetic-calibration",
        Version = "1.0.0",
        Description = "Memory-only calibration contract fixture.",
        Rubric = new CalibrationRubricReference
        {
            Id = "ehe-work-item",
            Version = "1.0.0",
        },
        Records =
        [
            new CalibrationRecord
            {
                Id = "record:sample:implementation",
                Repository = new CalibrationRepositoryReference
                {
                    Id = "repository:sample",
                    Name = "Synthetic sample",
                    SourceDigest = "sha256:fixture",
                },
                Profile = EstimationProfile.Implementation,
                BaselineId = "senior-contractor-2026-no-ai",
                Partition = CalibrationPartition.Test,
                SourceEstimatorVersion = "seed-rules/0.2.0",
                SourceEstimateDigest = "sha256:source-estimate",
                Source = new CalibrationSourceProvenance
                {
                    DataClassification = CalibrationDataClassification.Synthetic,
                    SourceReference = "synthetic:memory-only",
                    Revision = "1",
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
                            Id = "teacher:test-model",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Teacher,
                            ModelId = "logical-estimator",
                            ModelVersion = "test-1",
                        },
                    ],
                },
                Targets =
                [
                    new CalibrationTarget
                    {
                        Id = "target:implementation",
                        Category = EffortCategory.ProductionImplementation,
                        Title = "Implement behavior",
                        Scope = "src/Sample",
                        SourceWorkItemIds = ["work:implementation"],
                        EvidenceIds = ["evidence:implementation"],
                        Hours = Hours(3m, 5m, 7m),
                        Rationale = "One bounded implementation capability.",
                    },
                    new CalibrationTarget
                    {
                        Id = "target:tests",
                        Category = EffortCategory.UnitTesting,
                        Title = "Test behavior",
                        Scope = "tests/Sample.Tests",
                        SourceWorkItemIds = ["work:tests"],
                        EvidenceIds = ["evidence:tests"],
                        Hours = Hours(1m, 3m, 5m),
                        Rationale = "One bounded unit-test capability.",
                    },
                ],
            },
        ],
    };

    private static CalibrationReviewPlan CreateReviewPlan(EstimateReport estimate) => new()
    {
        CompilerVersion = CalibrationReviewCompiler.CompilerVersion,
        Id = "synthetic-review-plan",
        Version = "1.0.0",
        Description = "Memory-only completed review-plan fixture.",
        Rubric = new CalibrationRubricReference
        {
            Id = "ehe-work-item",
            Version = "1.0.0",
        },
        Records =
        [
            new CalibrationReviewPlanRecord
            {
                Id = "record:sample:implementation",
                Repository = new CalibrationRepositoryReference
                {
                    Id = "repository:sample",
                    Name = "Synthetic sample",
                    SourceDigest = estimate.Repository.SourceDigest!,
                },
                Profile = estimate.Profile,
                BaselineId = estimate.Baseline.Id,
                Partition = CalibrationPartition.Test,
                SourceEstimatorVersion = estimate.EstimatorVersion,
                SourceEstimateDigest = CalibrationDigest.Compute(estimate),
                Source = new CalibrationSourceProvenance
                {
                    DataClassification = CalibrationDataClassification.Synthetic,
                    SourceReference = "synthetic:memory-only",
                    Revision = "1",
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
                            Id = "teacher:test-model",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Teacher,
                            ModelId = "logical-estimator",
                            ModelVersion = "test-1",
                        },
                    ],
                },
                Capabilities = [.. estimate.WorkItems.Select(item =>
                    new CalibrationCapabilityReviewDecision
                    {
                        SourceCapabilityId = CalibrationAuthoring.GetSourceCapabilityId(item.Id),
                        Rationale = "Independently reviewed synthetic capability.",
                        Targets =
                        [
                            new CalibrationReviewTargetDecision
                            {
                                Hours = item.Hours,
                                UncertaintyReasons = item.UncertaintyReasons,
                            },
                        ],
                    })],
            },
        ],
    };

    private static EstimateReport CreateCandidate()
    {
        WorkItem implementation = CreateWorkItem(
            "work:implementation",
            EffortCategory.ProductionImplementation,
            "src/Sample",
            "evidence:implementation",
            Hours(2m, 4m, 6m));
        WorkItem tests = CreateWorkItem(
            "work:tests",
            EffortCategory.UnitTesting,
            "tests/Sample.Tests",
            "evidence:tests",
            Hours(1m, 2m, 3m));

        return new EstimateReport
        {
            EstimatorVersion = "candidate/1.0.0",
            Repository = new RepositoryDescriptor
            {
                Name = "Synthetic sample",
                SourceDigest = "sha256:fixture",
            },
            Profile = EstimationProfile.Implementation,
            Baseline = new EstimationBaseline
            {
                Id = "senior-contractor-2026-no-ai",
                WorkerProfile = "Competent senior contractor",
                TechnologyBaselineYear = 2026,
                BusinessDomainFamiliar = false,
                UsesAi = false,
                Description = "Synthetic test baseline.",
            },
            TotalEffort = Hours(3m, 6m, 9m),
            Categories =
            [
                new CategoryEstimate
                {
                    Category = EffortCategory.ProductionImplementation,
                    Hours = implementation.Hours,
                },
                new CategoryEstimate
                {
                    Category = EffortCategory.UnitTesting,
                    Hours = tests.Hours,
                },
            ],
            WorkItems = [implementation, tests],
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
            },
        };
    }

    private static WorkItem CreateWorkItem(
        string id,
        EffortCategory category,
        string scope,
        string evidenceId,
        EffortRange hours) => new()
        {
            Id = id,
            Category = category,
            Title = id,
            Scope = scope,
            EvidenceIds = [evidenceId],
            Complexity = ComplexityLevel.Moderate,
            Hours = hours,
            Confidence = 0.8m,
            Reason = "Synthetic calibration candidate.",
            Estimator = new EstimatorReference
            {
                Id = "candidate",
                Version = "1.0.0",
                Kind = EstimatorKind.Rule,
            },
            Profiles = [EstimationProfile.Implementation],
        };

    private static EffortRange Hours(decimal low, decimal expected, decimal high) => new()
    {
        Low = low,
        Expected = expected,
        High = high,
    };
}
