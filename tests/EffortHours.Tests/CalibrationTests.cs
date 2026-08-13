using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class CalibrationTests
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
        Assert.Equal("calibration-authoring/0.2.0", first.AuthoringVersion);
        Assert.Equal("1.1.0", first.Rubric.Version);
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
        CalibrationCorpus legacy = CalibrationReviewCompiler.Compile(
            plan with { CompilerVersion = "calibration-review-compiler/0.1.0" },
            [estimate]);

        Assert.True(planSchema.IsValid, string.Join(Environment.NewLine, planSchema.Errors));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(legacy));

        CalibrationRecord record = Assert.Single(first.Records);
        Assert.Equal(2, record.Targets.Count);
        Assert.Equal(
            estimate.WorkItems.Select(item => item.Id).OrderBy(id => id, StringComparer.Ordinal),
            record.Targets.SelectMany(target => target.SourceWorkItemIds)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(record.Targets, target => Assert.NotEmpty(target.EvidenceIds));
    }

    [Fact]
    public void ReviewPlanCompilesExplicitZeroExclusionAndMeasuresCandidateOverestimateInMemory()
    {
        EstimateReport estimate = CreateCandidate();
        CalibrationReviewPlan source = CreateReviewPlan(estimate);
        CalibrationReviewPlanRecord sourceRecord = Assert.Single(source.Records);
        string implementationCapabilityId = CalibrationAuthoring.GetSourceCapabilityId(
            estimate.WorkItems.Single(item =>
                item.Category == EffortCategory.ProductionImplementation).Id);
        CalibrationCapabilityReviewDecision implementation = sourceRecord.Capabilities.Single(
            capability => capability.SourceCapabilityId == implementationCapabilityId);
        CalibrationReviewPlan plan = source with
        {
            Records =
            [
                sourceRecord with
                {
                    Capabilities = [.. sourceRecord.Capabilities.Select(capability =>
                        capability.SourceCapabilityId == implementationCapabilityId
                            ? implementation with
                            {
                                Rationale =
                                    "The evidence is an explicit false positive already represented elsewhere.",
                                Targets =
                                [
                                    new CalibrationReviewTargetDecision
                                    {
                                        Hours = Hours(0m, 0m, 0m),
                                        SizeException =
                                            "Explicit reviewed exclusion; no represented EHE remains.",
                                    },
                                ],
                            }
                            : capability)],
                },
            ],
        };

        string planJson = ContractJson.Serialize(plan);
        SchemaValidationResult planSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationReviewPlan,
            planJson);
        CalibrationCorpus corpus = CalibrationReviewCompiler.Compile(plan, [estimate]);
        CalibrationEvaluationException legacy = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationReviewCompiler.Compile(
                plan with { CompilerVersion = "calibration-review-compiler/0.1.0" },
                [estimate]));
        string corpusJson = ContractJson.Serialize(corpus);
        SchemaValidationResult corpusSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpus,
            corpusJson);
        CalibrationCorpusReviewPacket handoff = CalibrationCorpusReviewAuthoring.Scaffold(corpus);
        SchemaValidationResult handoffSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPacket,
            ContractJson.Serialize(handoff));
        CalibrationEvaluationReport evaluation = CalibrationEvaluator.Evaluate(
            corpus,
            [estimate],
            CalibrationPartition.Test);
        CalibrationTarget excluded = Assert.Single(
            Assert.Single(corpus.Records).Targets,
            target => target.Category == EffortCategory.ProductionImplementation);
        CalibrationCategoryMetrics production = Assert.Single(
            evaluation.Categories,
            category => category.Category == EffortCategory.ProductionImplementation);

        Assert.True(planSchema.IsValid, string.Join(Environment.NewLine, planSchema.Errors));
        Assert.True(corpusSchema.IsValid, string.Join(Environment.NewLine, corpusSchema.Errors));
        Assert.True(handoffSchema.IsValid, string.Join(Environment.NewLine, handoffSchema.Errors));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(corpus));
        Assert.Equal(Hours(0m, 0m, 0m), excluded.Hours);
        Assert.False(string.IsNullOrWhiteSpace(excluded.SizeException));
        Assert.Equal(0m, production.Metrics.Expected.ReviewedHours);
        Assert.Equal(4m, production.Metrics.Expected.CandidateHours);
        Assert.Contains(
            legacy.Errors,
            error => error.Contains("zero-hour exclusions", StringComparison.Ordinal));
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
    public void CorpusReviewScaffoldIsDeterministicSchemaValidAndCanHidePriorJudgmentsInMemory()
    {
        CalibrationCorpus corpus = CreateCorpus();

        CalibrationCorpusReviewPacket reference = CalibrationCorpusReviewAuthoring.Scaffold(corpus);
        CalibrationCorpusReviewPacket second = CalibrationCorpusReviewAuthoring.Scaffold(corpus);
        CalibrationCorpusReviewPacket blind = CalibrationCorpusReviewAuthoring.Scaffold(
            corpus,
            blind: true);
        string json = ContractJson.Serialize(reference);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPacket,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(reference));
        Assert.Empty(ContractValidation.Validate(blind));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Equal(CalibrationDigest.Compute(corpus), reference.SourceCorpus.Digest);
        Assert.NotNull(Assert.Single(reference.Records).CandidateTotalHours);
        Assert.All(Assert.Single(reference.Records).Targets, target =>
        {
            Assert.NotNull(target.Candidate.Hours);
            Assert.NotNull(target.Candidate.Rationale);
            Assert.Null(target.Review.Action);
        });
        Assert.Null(Assert.Single(blind.Records).CandidateTotalHours);
        Assert.All(Assert.Single(blind.Records).Targets, target =>
        {
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Rationale);
            Assert.Empty(target.Candidate.UncertaintyReasons);
            Assert.Null(target.Candidate.SizeException);
        });
        Assert.Contains("\"action\": null", ContractJson.Serialize(blind), StringComparison.Ordinal);
    }

    [Fact]
    public void CorpusReviewCompilerAdvancesMaturityAndPreservesStructuralLineageInMemory()
    {
        CalibrationCorpus source = CreateCorpus();
        CalibrationCorpusReviewPlan plan = CreateCorpusReviewPlan(source);
        string planJson = ContractJson.Serialize(plan);
        SchemaValidationResult planSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPlan,
            planJson);

        CalibrationCorpus first = CalibrationCorpusReviewCompiler.Compile(plan, source);
        CalibrationCorpus second = CalibrationCorpusReviewCompiler.Compile(plan, source);
        CalibrationCorpus legacy = CalibrationCorpusReviewCompiler.Compile(
            plan with { CompilerVersion = "calibration-corpus-review-compiler/0.1.0" },
            source);
        CalibrationRecord sourceRecord = Assert.Single(source.Records);
        CalibrationRecord revised = Assert.Single(first.Records);

        Assert.True(planSchema.IsValid, string.Join(Environment.NewLine, planSchema.Errors));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(legacy));
        Assert.Equal(CalibrationReviewStatus.Reviewed, revised.Review.Status);
        Assert.Equal(2, revised.Review.Reviewers.Count);
        Assert.Contains(revised.Review.Reviewers, reviewer =>
            reviewer.Role == CalibrationReviewerRole.Reviewer &&
            reviewer.Id == "reviewer:independent-test");
        Assert.Equal(
            sourceRecord.Targets.Select(target => new
            {
                target.Id,
                target.Category,
                target.Scope,
                target.SourceWorkItemIds,
                target.EvidenceIds,
            }),
            revised.Targets.Select(target => new
            {
                target.Id,
                target.Category,
                target.Scope,
                target.SourceWorkItemIds,
                target.EvidenceIds,
            }));
        Assert.Equal(sourceRecord.Targets[0].Hours, revised.Targets[0].Hours);
        Assert.Equal(Hours(2m, 4m, 6m), revised.Targets[1].Hours);
        Assert.Equal("Independent correction of test depth.", revised.Targets[1].Rationale);
    }

    [Fact]
    public void CorpusReviewCanReplacePriorTargetWithExplicitZeroExclusionInMemory()
    {
        CalibrationCorpus source = CreateCorpus();
        CalibrationCorpusReviewPlan original = CreateCorpusReviewPlan(source);
        CalibrationCorpusReviewPlanRecord originalRecord = Assert.Single(original.Records);
        CalibrationRecord sourceRecord = Assert.Single(source.Records);
        CalibrationCorpusReviewTargetDecision zero = new()
        {
            SourceTargetId = sourceRecord.Targets[0].Id,
            Action = CalibrationCorpusReviewAction.Replace,
            Hours = Hours(0m, 0m, 0m),
            Rationale = "Independent review identified a false-positive represented capability.",
            SizeException = "Explicit reviewed exclusion; no represented EHE remains.",
        };
        CalibrationCorpusReviewPlan plan = original with
        {
            Records =
            [
                originalRecord with
                {
                    Targets = [zero, originalRecord.Targets[1]],
                },
            ],
        };

        string planJson = ContractJson.Serialize(plan);
        SchemaValidationResult planSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationCorpusReviewPlan,
            planJson);
        CalibrationCorpus revised = CalibrationCorpusReviewCompiler.Compile(plan, source);
        CalibrationEvaluationException legacy = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationCorpusReviewCompiler.Compile(
                plan with { CompilerVersion = "calibration-corpus-review-compiler/0.1.0" },
                source));
        CalibrationTarget excluded = Assert.Single(revised.Records).Targets[0];

        Assert.True(planSchema.IsValid, string.Join(Environment.NewLine, planSchema.Errors));
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.Empty(ContractValidation.Validate(revised));
        Assert.Equal(Hours(0m, 0m, 0m), excluded.Hours);
        Assert.Equal(zero.Rationale, excluded.Rationale);
        Assert.Equal(zero.SizeException, excluded.SizeException);
        Assert.Contains(
            legacy.Errors,
            error => error.Contains("zero-hour exclusions", StringComparison.Ordinal));
    }

    [Fact]
    public void CorpusReviewCompilerAdvancesReviewedCorpusToAdjudicatedInMemory()
    {
        CalibrationCorpus teacher = CreateCorpus();
        CalibrationCorpus reviewed = CalibrationCorpusReviewCompiler.Compile(
            CreateCorpusReviewPlan(teacher),
            teacher);
        CalibrationRecord sourceRecord = Assert.Single(reviewed.Records);
        CalibrationCorpusReviewPlan adjudication = new()
        {
            CompilerVersion = CalibrationCorpusReviewCompiler.CompilerVersion,
            SourceCorpus = new CalibrationCorpusReference
            {
                Id = reviewed.Id,
                Version = reviewed.Version,
                Digest = CalibrationDigest.Compute(reviewed),
            },
            Id = "synthetic-calibration-adjudicated",
            Version = "1.2.0",
            Description = "Memory-only adjudicated calibration fixture.",
            Records =
            [
                new CalibrationCorpusReviewPlanRecord
                {
                    SourceRecordId = sourceRecord.Id,
                    ResultStatus = CalibrationReviewStatus.Adjudicated,
                    CompletedOn = new DateOnly(2026, 8, 8),
                    Reviewers =
                    [
                        new CalibrationReviewer
                        {
                            Id = "adjudicator:independent-test",
                            Kind = CalibrationReviewerKind.Human,
                            Role = CalibrationReviewerRole.Adjudicator,
                        },
                    ],
                    Notes = "A distinct adjudicator resolved the reviewed fixture.",
                    Targets = [.. sourceRecord.Targets.Select(target =>
                        new CalibrationCorpusReviewTargetDecision
                        {
                            SourceTargetId = target.Id,
                            Action = CalibrationCorpusReviewAction.Accept,
                        })],
                },
            ],
        };

        CalibrationCorpus result = CalibrationCorpusReviewCompiler.Compile(adjudication, reviewed);
        CalibrationRecord record = Assert.Single(result.Records);

        Assert.Empty(ContractValidation.Validate(result));
        Assert.Equal(CalibrationReviewStatus.Adjudicated, record.Review.Status);
        Assert.Equal(3, record.Review.Reviewers.Count);
        Assert.Contains(record.Review.Reviewers, reviewer =>
            reviewer.Role == CalibrationReviewerRole.Adjudicator);
    }

    [Fact]
    public void CorpusReviewCompilerRejectsDigestDriftMissingDecisionsAndReusedTeacherIdentityInMemory()
    {
        CalibrationCorpus source = CreateCorpus();
        CalibrationCorpusReviewPlan plan = CreateCorpusReviewPlan(source);
        CalibrationCorpusReviewPlanRecord record = Assert.Single(plan.Records);

        CalibrationEvaluationException digest = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationCorpusReviewCompiler.Compile(
                plan with
                {
                    SourceCorpus = plan.SourceCorpus with { Digest = "sha256:changed" },
                },
                source));
        CalibrationEvaluationException missing = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationCorpusReviewCompiler.Compile(
                plan with
                {
                    Records =
                    [
                        record with { Targets = [record.Targets[0]] },
                    ],
                },
                source));
        CalibrationReviewer teacher = Assert.Single(Assert.Single(source.Records).Review.Reviewers);
        CalibrationEvaluationException identity = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationCorpusReviewCompiler.Compile(
                plan with
                {
                    Records =
                    [
                        record with
                        {
                            Reviewers =
                            [
                                teacher with { Role = CalibrationReviewerRole.Reviewer },
                            ],
                        },
                    ],
                },
                source));

        Assert.Contains(digest.Errors, error => error.Contains("expects source corpus", StringComparison.Ordinal));
        Assert.Contains(missing.Errors, error => error.Contains("no decision", StringComparison.Ordinal));
        Assert.Contains(identity.Errors, error => error.Contains("reuses prior reviewer", StringComparison.Ordinal));
    }

    [Fact]
    public void MutationEvaluationIsDeterministicSchemaValidAndReportsRelationalFailuresInMemory()
    {
        EstimateReport reference = CreateCandidate();
        EstimateReport subject = IncreaseProduction(reference, "sha256:mutated");
        CalibrationMutationSuite suite = CreateMutationSuite(reference, subject);

        CalibrationMutationReport first = CalibrationMutationEvaluator.Evaluate(
            suite,
            [subject, reference]);
        CalibrationMutationReport second = CalibrationMutationEvaluator.Evaluate(
            suite,
            [reference, subject]);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult suiteSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationMutationSuite,
            ContractJson.Serialize(suite));
        SchemaValidationResult reportSchema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationMutationReport,
            json);

        Assert.True(suiteSchema.IsValid, string.Join(Environment.NewLine, suiteSchema.Errors));
        Assert.True(reportSchema.IsValid, string.Join(Environment.NewLine, reportSchema.Errors));
        Assert.Empty(ContractValidation.Validate(suite));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.True(first.AllPassed);
        Assert.Equal(3, first.PassedCount);
        Assert.All(first.Assertions, assertion => Assert.True(assertion.Passed));

        CalibrationMutationAssertion total = suite.Assertions[0];
        CalibrationMutationSuite failingSuite = suite with
        {
            Assertions =
            [
                total with
                {
                    Id = "mutation:unexpected-equality",
                    MinimumDifferenceHours = 0m,
                    MaximumDifferenceHours = 0m,
                },
            ],
        };
        CalibrationMutationReport failed = CalibrationMutationEvaluator.Evaluate(
            failingSuite,
            [reference, subject]);
        Assert.False(failed.AllPassed);
        Assert.Equal(1, failed.FailedCount);
        Assert.False(Assert.Single(failed.Assertions).Passed);
    }

    [Fact]
    public void MutationEvaluationRejectsMissingCandidatesAndInvalidBoundsInMemory()
    {
        EstimateReport reference = CreateCandidate();
        EstimateReport subject = IncreaseProduction(reference, "sha256:mutated");
        CalibrationMutationSuite suite = CreateMutationSuite(reference, subject);
        CalibrationMutationAssertion assertion = suite.Assertions[0];

        CalibrationEvaluationException missing = Assert.Throws<CalibrationEvaluationException>(() =>
            CalibrationMutationEvaluator.Evaluate(suite, [reference]));
        IReadOnlyList<string> bounds = ContractValidation.Validate(suite with
        {
            Assertions =
            [
                assertion with
                {
                    MinimumDifferenceHours = 2m,
                    MaximumDifferenceHours = 1m,
                },
            ],
        });

        Assert.Contains(missing.Errors, error => error.Contains("No candidate matches", StringComparison.Ordinal));
        Assert.Contains(bounds, error => error.Contains("cannot exceed", StringComparison.Ordinal));
    }

    [Fact]
    public void MutationEvaluationReadsLowAndHighPointsAndTreatsMissingCategoriesAsZeroInMemory()
    {
        EstimateReport reference = CreateCandidate();
        EstimateReport subject = IncreaseProduction(reference, "sha256:mutated");
        CalibrationMutationSuite source = CreateMutationSuite(reference, subject);
        CalibrationMutationAssertion production = source.Assertions[1];
        CalibrationMutationSuite suite = source with
        {
            Assertions =
            [
                production with
                {
                    Id = "mutation:production-low",
                    Point = CalibrationMutationPoint.Low,
                    MinimumDifferenceHours = 1m,
                    MaximumDifferenceHours = 1m,
                },
                production with
                {
                    Id = "mutation:production-high",
                    Point = CalibrationMutationPoint.High,
                    MinimumDifferenceHours = 1m,
                    MaximumDifferenceHours = 1m,
                },
                production with
                {
                    Id = "mutation:missing-documentation",
                    Category = EffortCategory.Documentation,
                    MinimumDifferenceHours = 0m,
                    MaximumDifferenceHours = 0m,
                },
            ],
        };

        CalibrationMutationReport report = CalibrationMutationEvaluator.Evaluate(
            suite,
            [reference, subject]);
        CalibrationMutationAssertionResult missing = report.Assertions.Single(
            assertion => assertion.Id == "mutation:missing-documentation");

        Assert.True(report.AllPassed);
        Assert.Equal(3, report.PassedCount);
        Assert.Equal(0m, missing.SubjectHours);
        Assert.Equal(0m, missing.ReferenceHours);
        Assert.Equal(0m, missing.DifferenceHours);
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
    public void CorpusValidationRejectsAmbiguousOrUnexplainedZeroHourTargets()
    {
        CalibrationCorpus original = CreateCorpus();
        CalibrationRecord record = Assert.Single(original.Records);
        CalibrationTarget target = record.Targets[0];
        CalibrationTarget ambiguous = target with
        {
            Hours = Hours(0m, 0m, 1m),
            SizeException = "Attempted exclusion.",
        };
        CalibrationTarget unexplained = target with
        {
            Hours = Hours(0m, 0m, 0m),
            SizeException = null,
        };

        IReadOnlyList<string> ambiguousErrors = ContractValidation.Validate(original with
        {
            Records = [record with { Targets = [ambiguous, record.Targets[1]] }],
        });
        IReadOnlyList<string> unexplainedErrors = ContractValidation.Validate(original with
        {
            Records = [record with { Targets = [unexplained, record.Targets[1]] }],
        });

        Assert.Contains(
            ambiguousErrors,
            error => error.Contains("exactly 0/0/0", StringComparison.Ordinal));
        Assert.Contains(
            unexplainedErrors,
            error => error.Contains("sizeException", StringComparison.Ordinal));
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

    private static CalibrationCorpusReviewPlan CreateCorpusReviewPlan(CalibrationCorpus corpus)
    {
        CalibrationRecord record = Assert.Single(corpus.Records);
        return new CalibrationCorpusReviewPlan
        {
            CompilerVersion = CalibrationCorpusReviewCompiler.CompilerVersion,
            SourceCorpus = new CalibrationCorpusReference
            {
                Id = corpus.Id,
                Version = corpus.Version,
                Digest = CalibrationDigest.Compute(corpus),
            },
            Id = "synthetic-calibration-reviewed",
            Version = "1.1.0",
            Description = "Memory-only independently reviewed calibration fixture.",
            Records =
            [
                new CalibrationCorpusReviewPlanRecord
                {
                    SourceRecordId = record.Id,
                    ResultStatus = CalibrationReviewStatus.Reviewed,
                    CompletedOn = new DateOnly(2026, 8, 7),
                    Reviewers =
                    [
                        new CalibrationReviewer
                        {
                            Id = "reviewer:independent-test",
                            Kind = CalibrationReviewerKind.HostAi,
                            Role = CalibrationReviewerRole.Reviewer,
                            ModelId = "independent-logical-estimator",
                            ModelVersion = "test-2",
                        },
                    ],
                    Notes = "A distinct reviewer checked every target.",
                    Targets =
                    [
                        new CalibrationCorpusReviewTargetDecision
                        {
                            SourceTargetId = record.Targets[0].Id,
                            Action = CalibrationCorpusReviewAction.Accept,
                        },
                        new CalibrationCorpusReviewTargetDecision
                        {
                            SourceTargetId = record.Targets[1].Id,
                            Action = CalibrationCorpusReviewAction.Replace,
                            Hours = Hours(2m, 4m, 6m),
                            Rationale = "Independent correction of test depth.",
                        },
                    ],
                },
            ],
        };
    }

    private static CalibrationMutationSuite CreateMutationSuite(
        EstimateReport reference,
        EstimateReport subject) => new()
        {
            MetricVersion = CalibrationMutationEvaluator.MetricVersion,
            Id = "synthetic-mutations",
            Version = "1.0.0",
            Description = "Memory-only mutation relation fixture.",
            Cases =
            [
                new CalibrationMutationCase
                {
                    Id = "reference",
                    Description = "Reference estimate.",
                    SourceDigest = reference.Repository.SourceDigest!,
                    Profile = reference.Profile,
                    BaselineId = reference.Baseline.Id,
                },
                new CalibrationMutationCase
                {
                    Id = "subject",
                    Description = "Production behavior increased.",
                    SourceDigest = subject.Repository.SourceDigest!,
                    Profile = subject.Profile,
                    BaselineId = subject.Baseline.Id,
                },
            ],
            Assertions =
            [
                new CalibrationMutationAssertion
                {
                    Id = "mutation:total-increases",
                    Family = "meaningful-behavior",
                    SubjectCaseId = "subject",
                    ReferenceCaseId = "reference",
                    Point = CalibrationMutationPoint.Expected,
                    Scope = CalibrationMutationScope.RepositoryTotal,
                    MinimumDifferenceHours = 1m,
                    Rationale = "Meaningful production behavior must increase total expected EHE.",
                },
                new CalibrationMutationAssertion
                {
                    Id = "mutation:production-increases",
                    Family = "meaningful-behavior",
                    SubjectCaseId = "subject",
                    ReferenceCaseId = "reference",
                    Point = CalibrationMutationPoint.Expected,
                    Scope = CalibrationMutationScope.Category,
                    Category = EffortCategory.ProductionImplementation,
                    MinimumDifferenceHours = 1m,
                    Rationale = "The production category must receive the represented behavior.",
                },
                new CalibrationMutationAssertion
                {
                    Id = "mutation:tests-unchanged",
                    Family = "category-isolation",
                    SubjectCaseId = "subject",
                    ReferenceCaseId = "reference",
                    Point = CalibrationMutationPoint.Expected,
                    Scope = CalibrationMutationScope.Category,
                    Category = EffortCategory.UnitTesting,
                    MinimumDifferenceHours = 0m,
                    MaximumDifferenceHours = 0m,
                    Rationale = "Production-only mutation must not invent test effort.",
                },
            ],
        };

    private static EstimateReport IncreaseProduction(EstimateReport source, string digest)
    {
        WorkItem implementation = source.WorkItems[0] with { Hours = Hours(3m, 5m, 7m) };
        WorkItem tests = source.WorkItems[1];
        return source with
        {
            Repository = source.Repository with { SourceDigest = digest },
            TotalEffort = ContractValidation.Sum([implementation.Hours, tests.Hours]),
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
        };
    }

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
